using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiscUtils.Ntfs;
using DiscUtils.Ntfs.Journal;
using DiscUtils.Streams;

namespace LibraryTests.Ntfs.Journal;

/// <summary>
/// Logs that are unusual or damaged in one specific place: restart pages, records that are no NTFS records, the
/// checkpoint at the very end, hot fixes, and pages the log describes in ways DiscUtils must not trust.
/// </summary>
public partial class NtfsJournalEdgeCaseTest
{
    private static readonly byte[] Pattern = Enumerable.Repeat((byte)0x3C, 16).ToArray();

    private static TestLogRecord HotFix(long vcn, long[] lcns) =>
        new() { Redo = NtfsLogOperation.HotFix, Undo = NtfsLogOperation.Noop, TransactionId = Tx, TargetAttribute = Data, TargetVcn = vcn, Lcns = lcns };

    private static TestLogRecord Noop() =>
        new() { Redo = NtfsLogOperation.Noop, Undo = NtfsLogOperation.Noop, TransactionId = 0x18 };

    [Fact]
    public void ALogWithNoClientInUse_IsClean()
    {
        using var probe = Volume();
        using var volume = WithRecords(Raw(FreeCluster(probe), Pattern), Forget());
        PatchRestartPages(new TestJournalWriter(volume), page => EndianUtilities.WriteBytesLittleEndian((ushort)0xFFFF, page, 0x30 + 0x0C));

        var info = NtfsJournal.Analyze(volume);
        Assert.Equal(NtfsJournalStatus.Clean, info.Status);
        Assert.Contains("cleanly", info.Detail);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AChkdskMarkOnTheSecondRestartPageOnly_StillMeansChkdskRan(bool firstPageDamaged)
    {
        using var probe = Volume();
        using var volume = WithRecords(Raw(FreeCluster(probe), Pattern), Forget());
        var w = new TestJournalWriter(volume);
        w.Storage.Write(TestJournalWriter.PageSize, BitConverter.GetBytes(JournalUtilities.SignatureChkd), 0, 4);
        if (firstPageDamaged)
        {
            w.Storage.Write(0, [0x58, 0x58, 0x58, 0x58], 0, 4);
        }

        var info = NtfsJournal.Analyze(volume);
        Assert.Equal(NtfsJournalStatus.Clean, info.Status);
        Assert.Contains("chkdsk", info.Detail);
    }

    [Fact]
    public void ADamagedSecondRestartPage_LeavesTheFirst()
    {
        using var probe = Volume();
        var x = FreeCluster(probe);
        using var volume = WithRecords(Raw(x, Pattern), Forget());
        new TestJournalWriter(volume).Storage.Write(TestJournalWriter.PageSize, [0x58, 0x58, 0x58, 0x58], 0, 4);

        NtfsJournal.Replay(volume);
        Assert.Equal(0x3C, Read(volume, x * 4096, 1)[0]);
    }

    [Fact]
    public void ARecordThatIsNoNtfsRecord_IsRefused_AfterTheCheckpoint_WhereRedoStarts_AndInAnUndoChain()
    {
        // After the checkpoint: the analysis reads it.
        using (var volume = Volume())
        {
            var w = new TestJournalWriter(volume);
            var restart = w.Checkpoint(Attributes);
            w.AppendRaw(new byte[8], LfsRecord.TypeClientRecord, Tx, 0);
            w.Finish(restart);
            AssertRefused(volume, "too short");
        }

        // Before it, where the redo pass starts: a page has been dirty since then.
        using (var volume = Volume())
        {
            var x = FreeCluster(volume);
            var w = new TestJournalWriter(volume);
            var bad = w.AppendRaw(new byte[8], LfsRecord.TypeClientRecord, Tx, 0);
            var restart = w.Checkpoint(Attributes, dirtyPages: [(Data, x, bad, [x])]);
            w.Finish(restart);
            AssertRefused(volume, "too short");
        }

        // Before it, in the undo chain of a transaction the crash interrupted.
        using (var volume = Volume())
        {
            var w = new TestJournalWriter(volume);
            var bad = w.AppendRaw(new byte[8], LfsRecord.TypeClientRecord, 0x90, 0);
            var restart = w.Checkpoint(Attributes, transactions: [(0x90, TransactionState.Active, bad, bad)]);
            w.Finish(restart);
            AssertRefused(volume, "too short");
        }
    }

    [Theory]
    [InlineData(0x04, 4)]
    [InlineData(0x08, 4)]
    [InlineData(0x08, -0x20)]
    public void ARecordWithMisplacedRedoOrUndoData_IsNoRecord(int field, int shift)
    {
        var record = new TestLogRecord
        {
            Redo = NtfsLogOperation.UpdateResidentValue, Undo = NtfsLogOperation.UpdateResidentValue, RedoData = new byte[8], UndoData = new byte[8], Lcns = [1],
        };
        var data = record.ClientData();
        EndianUtilities.WriteBytesLittleEndian((ushort)(EndianUtilities.ToUInt16LittleEndian(data, field) + shift), data, field);

        Assert.Null(NtfsLogRecord.TryParse(new LfsRecord { Lsn = 1, ClientData = data }, out var error));
        Assert.Contains("misplaced", error);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ACheckpointThatIsTheLastRecord_StartsRedoAtTheOldestDirtyPage_OrTransaction(bool dirtyPages)
    {
        using var probe = Volume();
        var x = FreeCluster(probe);

        // The checkpoint LSN is written before the record it names, so the log is built twice: first to learn
        // where the record after the restart record lands.
        ulong Build(MemoryStream volume, ulong checkpoint, out ulong change)
        {
            var w = new TestJournalWriter(volume);
            change = w.Append(Raw(x, Pattern));
            var pages = new List<(uint, long, ulong, long[])>();
            var transactions = new List<(uint, TransactionState, ulong, ulong)>();
            if (dirtyPages)
            {
                pages.Add((Data, x, change, [x]));
                pages.Add((Data, x + 1, 0, [x + 1]));
            }
            else
            {
                transactions.Add((Tx, TransactionState.Committed, change, change));
                transactions.Add((0x68, TransactionState.Committed, 0, 0));
            }

            var restart = w.Checkpoint(Attributes, pages, transactions, checkpointLsn: checkpoint);
            var last = w.Append(Noop());
            w.Finish(restart);
            return last;
        }

        using var dry = Volume();
        var lastLsn = Build(dry, 0, out _);
        using var volume = Volume();
        Assert.Equal(lastLsn, Build(volume, lastLsn, out var change));

        var layout = JournalVolumeLayout.Read(volume);
        var log = LfsLog.Open(LogFileStorage.Open(volume, layout), out _);
        var analysis = NtfsLogAnalysis.Run(log, log.Restart.InUseClient, layout);
        Assert.Equal(0, analysis.RecordsAfterCheckpoint);
        Assert.Equal(change, analysis.RedoLsn);

        // Redo needs a dirty page; a committed transaction alone leaves nothing to do.
        var info = NtfsJournal.Replay(volume);
        Assert.Equal(dirtyPages ? NtfsJournalStatus.NeedsReplay : NtfsJournalStatus.NothingToReplay, info.Status);
        Assert.Equal(dirtyPages ? 0x3C : 0, Read(volume, x * 4096, 1)[0]);
    }

    [Fact]
    public void HotFixes_OfACleanPage_WithoutACluster_OrOfAFreedCluster_MoveNothing()
    {
        using var volume = Volume();
        var x = FreeCluster(volume);
        var y = FreeCluster(volume, 1);
        var z = FreeCluster(volume, 2);
        using var result = WithRecordsOn(volume,
            HotFix(9000, [y]),
            Raw(z, Pattern),
            HotFix(z, []),
            Raw(x, Pattern),
            DeleteClusters(x),
            HotFix(x, [y]),
            Forget());

        var info = NtfsJournal.Replay(result);
        Assert.Equal(NtfsJournalStatus.NeedsReplay, info.Status);
        Assert.Equal(0x3C, Read(result, z * 4096, 1)[0]);
        Assert.Equal(0, Read(result, x * 4096, 1)[0]);
        Assert.Equal(0, Read(result, y * 4096, 1)[0]);
    }

    [Theory]
    [InlineData(-5L)]
    [InlineData(1L << 40)]
    public void ARecordNamingAClusterOffTheVolume_IsRefused(long lcn)
    {
        using var volume = WithRecords(Op(NtfsLogOperation.UpdateNonresidentValue, new byte[8], Data, 3, [lcn]), Forget());
        AssertRefused(volume, "not on the volume");
    }

    [Fact]
    public void AnIndexBufferImageWithoutAFixupArray_IsWrittenAsLogged()
    {
        using var volume = Volume();
        var x = FreeCluster(volume);
        var image = new byte[4096];
        EndianUtilities.WriteBytesLittleEndian(JournalUtilities.SignatureIndx, image, 0);
        EndianUtilities.WriteBytesLittleEndian((ushort)0x28, image, 4);
        image[0x100] = 0x77;
        using var result = WithRecordsOn(volume, Op(NtfsLogOperation.UpdateNonresidentValue, image, Index, 0, [x]), Forget());

        NtfsJournal.Replay(result);
        Assert.True(Read(result, x * 4096, 4096).SequenceEqual(image));
    }

    [Fact]
    public void PartOfAnIndexBufferWrittenIntoAnEmptyCluster_IsWrittenAsLogged()
    {
        using var volume = Volume();
        var x = FreeCluster(volume);
        using var result = WithRecordsOn(volume, Op(NtfsLogOperation.UpdateNonresidentValue, Pattern, Index, 0, [x], recordOffset: 0x100), Forget());

        NtfsJournal.Replay(result);
        var cluster = Read(result, x * 4096, 4096);
        Assert.True(cluster.AsSpan(0x100, Pattern.Length).SequenceEqual(Pattern));
        Assert.Equal(4096 - Pattern.Length, cluster.Count(b => b == 0));
    }

    [Fact]
    public void AnIndexBufferOfAnAttributeThatIsNotOpen_HasTheVolumesBufferSize()
    {
        using var volume = Volume();
        var indexLcn = FirstIndexBuffer(volume);
        var index = Read(volume, indexLcn * 4096, 4096, logical: true);
        var entry = 0x18 + (int)EndianUtilities.ToUInt32LittleEndian(index, 0x18);
        using var result = WithRecordsOn(volume,
            Op(NtfsLogOperation.UpdateFileNameAllocation, Enumerable.Repeat((byte)0x44, 0x38).ToArray(), 0x90, 0, [indexLcn], attributeOffset: entry),
            Forget());

        NtfsJournal.Replay(result);
        Assert.Equal(0x44, Read(result, indexLcn * 4096, 4096, logical: true)[entry + 0x18]);
    }
}
