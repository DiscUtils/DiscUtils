using System;
using System.IO;
using System.Linq;
using System.Text;
using DiscUtils;
using DiscUtils.Ntfs;
using DiscUtils.Ntfs.Journal;
using DiscUtils.Streams;

namespace LibraryTests.Ntfs.Journal;

/// <summary>
/// Edge cases of reading and replaying a journal: damaged or unusual restart pages and checkpoint tables, records
/// at the very end of the log, torn pages, freed pages, and the $LogFile and $MFTMirr locations.
/// </summary>
public partial class NtfsJournalEdgeCaseTest
{
    private const ushort Mft = 0x18;
    private const ushort Index = 0x40;
    private const ushort Data = 0x68;
    private const uint Tx = 0x40;

    private static readonly (ushort, uint, ulong, uint, string)[] Attributes =
    [
        (Mft, 0x80, 0x0001000000000000UL, 0, ""),
        (Index, 0xA0, 0x0005000000000005UL, 4096, "$I30"),
        (Data, 0x80, 0x0001000000000006UL, 0, ""),
    ];

    private static readonly Lazy<byte[]> Base = new(() =>
    {
        var stream = new MemoryStream();
        var geometry = Geometry.FromCapacity(16 * 1024 * 1024);
        using (var ntfs = NtfsFileSystem.Format(stream, "EDGE", geometry, 0, geometry.TotalSectorsLong))
        {
            ntfs.CreateDirectory("big");
            for (var i = 0; i < 150; i++)
            {
                using var s = ntfs.OpenFile($@"big\file-with-a-long-name-{i:D4}.txt", FileMode.Create);
                s.WriteByte((byte)i);
            }
        }

        return stream.ToArray();
    });

    private static MemoryStream Volume() => JournalScenario.Copy(Base.Value);

    private static long FreeCluster(Stream volume, int skip = 0)
    {
        var bytes = ((MemoryStream)volume).ToArray();
        for (var lcn = (bytes.Length / 4096) - 2; lcn > 16; lcn--)
        {
            if (bytes.AsSpan(lcn * 4096, 4096).ToArray().All(b => b == 0) && skip-- == 0)
            {
                return lcn;
            }
        }

        throw new InvalidOperationException("no free cluster");
    }

    private static long FirstIndexBuffer(Stream volume)
    {
        var bytes = ((MemoryStream)volume).ToArray();
        for (var lcn = 16; lcn < bytes.Length / 4096; lcn++)
        {
            if (EndianUtilities.ToUInt32LittleEndian(bytes, lcn * 4096) == JournalUtilities.SignatureIndx)
            {
                return lcn;
            }
        }

        throw new InvalidOperationException("no index buffer");
    }

    private static byte[] Read(Stream volume, long position, int length, bool logical = false)
    {
        var bytes = new byte[length];
        JournalUtilities.ReadExactly(volume, position, bytes, 0, length);
        if (logical)
        {
            Assert.True(JournalUtilities.RemoveFixups(bytes, 0, length));
        }

        return bytes;
    }

    private static TestLogRecord Raw(long lcn, byte[] redo, byte[]? undo = null, ushort attribute = Data, uint tx = Tx) => new()
    {
        Redo = NtfsLogOperation.UpdateNonresidentValue,
        Undo = NtfsLogOperation.UpdateNonresidentValue,
        TransactionId = tx,
        RedoData = redo,
        UndoData = undo ?? new byte[redo.Length],
        TargetAttribute = attribute,
        TargetVcn = lcn,
        Lcns = [lcn],
    };

    private static TestLogRecord Op(NtfsLogOperation op, byte[] data, ushort attribute, long vcn, long[] lcns, int recordOffset = 0,
        int attributeOffset = 0, int blockOffset = 0, NtfsLogOperation undo = NtfsLogOperation.Noop, byte[]? undoData = null) => new()
    {
        Redo = op,
        Undo = undo,
        TransactionId = Tx,
        RedoData = data,
        UndoData = undoData ?? [],
        TargetAttribute = attribute,
        TargetVcn = vcn,
        Lcns = lcns,
        RecordOffset = recordOffset,
        AttributeOffset = attributeOffset,
        ClusterBlockOffset = blockOffset,
    };

    private static TestLogRecord Forget(uint tx = Tx) =>
        new() { Redo = NtfsLogOperation.ForgetTransaction, Undo = NtfsLogOperation.CompensationLogRecord, TransactionId = tx };

    private static TestLogRecord DeleteClusters(long lcn) =>
        new()
        {
            Redo = NtfsLogOperation.DeleteDirtyClusters,
            Undo = NtfsLogOperation.Noop,
            TransactionId = Tx,
            RedoData = BitConverter.GetBytes(lcn).Concat(BitConverter.GetBytes(1L)).ToArray(),
        };

    private static byte[] RestartRecord(uint major, ulong checkpoint, ulong oat = 0, int oatLength = 0, ulong names = 0,
        int namesLength = 0, ulong dirty = 0, int dirtyLength = 0)
    {
        var r = new byte[0x40];
        EndianUtilities.WriteBytesLittleEndian(major, r, 0x00);
        EndianUtilities.WriteBytesLittleEndian(checkpoint, r, 0x08);
        EndianUtilities.WriteBytesLittleEndian(oat, r, 0x10);
        EndianUtilities.WriteBytesLittleEndian(names, r, 0x18);
        EndianUtilities.WriteBytesLittleEndian(dirty, r, 0x20);
        EndianUtilities.WriteBytesLittleEndian((uint)oatLength, r, 0x30);
        EndianUtilities.WriteBytesLittleEndian((uint)namesLength, r, 0x34);
        EndianUtilities.WriteBytesLittleEndian((uint)dirtyLength, r, 0x38);
        return r;
    }

    private static byte[] Table(int entrySize, int entries, int allocated = -1)
    {
        var table = new byte[0x18 + (entrySize * entries)];
        EndianUtilities.WriteBytesLittleEndian((ushort)entrySize, table, 0);
        EndianUtilities.WriteBytesLittleEndian((ushort)entries, table, 2);
        for (var i = 0; i < (allocated < 0 ? entries : allocated); i++)
        {
            EndianUtilities.WriteBytesLittleEndian(0xFFFFFFFFu, table, 0x18 + (i * entrySize));
        }

        return table;
    }

    private static ulong Dump(TestJournalWriter w, NtfsLogOperation op, byte[] table) =>
        w.Append(new TestLogRecord { Redo = op, Undo = NtfsLogOperation.Noop, TransactionId = 0x18, RedoData = table });

    /// <summary>A journal with a hand-made restart record.</summary>
    private static MemoryStream WithRestart(Func<TestJournalWriter, ulong, byte[]> restart)
    {
        var volume = Volume();
        var w = new TestJournalWriter(volume);
        var checkpoint = w.Append(new TestLogRecord { Redo = NtfsLogOperation.Noop, Undo = NtfsLogOperation.Noop, TransactionId = 0x18 });
        var lsn = w.AppendRaw(restart(w, checkpoint), LfsRecord.TypeClientRestart, 0, 0);
        w.Finish(lsn);
        return volume;
    }

    private static MemoryStream WithRecords(params TestLogRecord[] records)
    {
        var volume = Volume();
        var w = new TestJournalWriter(volume);
        var restart = w.Checkpoint(Attributes);
        foreach (var r in records)
        {
            w.Append(r);
        }

        w.Finish(restart);
        return volume;
    }

    private static void AssertRefused(Stream volume, string reason)
    {
        var info = NtfsJournal.Analyze(volume);
        Assert.True(info.Status == NtfsJournalStatus.Unsupported, $"expected Unsupported ({reason}), got {info}");
        Assert.True(info.Detail.Contains(reason), $"expected '{reason}' in '{info.Detail}'");
    }

    private static void PatchRestartPages(TestJournalWriter w, Action<byte[]> patch)
    {
        foreach (var offset in new long[] { 0, TestJournalWriter.PageSize })
        {
            var page = new byte[TestJournalWriter.PageSize];
            w.Storage.Read(offset, page, 0, page.Length);
            Assert.True(JournalUtilities.RemoveFixups(page, 0, page.Length));
            patch(page);
            JournalUtilities.ApplyFixups(page, 0, page.Length);
            w.Storage.Write(offset, page, 0, page.Length);
        }
    }

    [Fact]
    public void CheckpointTables_ThatDoNotAddUp_AreRefused()
    {
        // A table dump shorter than the restart record says.
        using (var v = WithRestart((w, c) => RestartRecord(1, c, Dump(w, NtfsLogOperation.OpenAttributeTableDump, Table(0x28, 1)), 0x100)))
        {
            AssertRefused(v, "shorter than the restart record says");
        }

        // Open attribute entries of the wrong size for the version.
        using (var v = WithRestart((w, c) => RestartRecord(1, c, Dump(w, NtfsLogOperation.OpenAttributeTableDump, Table(0x30, 1)), 0x48)))
        {
            AssertRefused(v, "entries are 48 bytes");
        }

        // A table dump too short to hold a header, and one with an impossible entry size.
        using (var v = WithRestart((w, c) => RestartRecord(1, c, Dump(w, NtfsLogOperation.OpenAttributeTableDump, new byte[0x10]), 0x10)))
        {
            AssertRefused(v, "too short");
        }

        using (var v = WithRestart((w, c) => RestartRecord(1, c, Dump(w, NtfsLogOperation.OpenAttributeTableDump, Table(4, 2)), 0x20)))
        {
            AssertRefused(v, "implausible header");
        }

        // A dirty page that would cover three clusters.
        using (var v = WithRestart((w, c) => RestartRecord(1, c, dirty: Dump(w, NtfsLogOperation.DirtyPageTableDump, Table(0x20 + 24, 1)), dirtyLength: 0x50)))
        {
            AssertRefused(v, "not a power of two");
        }

        // A dirty page entry that lists more clusters than it holds.
        using (var v = WithRestart((w, c) =>
        {
            var table = Table(0x28, 1);
            EndianUtilities.WriteBytesLittleEndian(10u, table, 0x18 + 0x0C);
            return RestartRecord(1, c, dirty: Dump(w, NtfsLogOperation.DirtyPageTableDump, table), dirtyLength: table.Length);
        }))
        {
            AssertRefused(v, "more clusters than it holds");
        }

        // A negative cluster count, and entries too small to hold any cluster.
        using (var v = WithRestart((w, c) =>
        {
            var table = Table(0x28, 1);
            EndianUtilities.WriteBytesLittleEndian(0xFFFFFFFFu, table, 0x18 + 0x0C);
            return RestartRecord(1, c, dirty: Dump(w, NtfsLogOperation.DirtyPageTableDump, table), dirtyLength: table.Length);
        }))
        {
            AssertRefused(v, "more clusters than it holds");
        }

        using (var v = WithRestart((w, c) => RestartRecord(1, c, dirty: Dump(w, NtfsLogOperation.DirtyPageTableDump, Table(0x20, 1)), dirtyLength: 0x38)))
        {
            AssertRefused(v, "covers 0 clusters");
        }

        // A dump that is not in the log, and one that is no NTFS record.
        using (var v = WithRestart((w, c) => RestartRecord(1, c, w.LsnOf(0x100, 1), 0x40)))
        {
            AssertRefused(v, "not in the log");
        }

        using (var v = WithRestart((w, c) => RestartRecord(1, c, w.AppendRaw(new byte[8], LfsRecord.TypeClientRecord, 0x18, 0), 0x40)))
        {
            AssertRefused(v, "too short");
        }

        // A damaged attribute names dump.
        using (var v = WithRestart((w, c) =>
        {
            var names = new byte[8];
            EndianUtilities.WriteBytesLittleEndian((ushort)0x18, names, 0);
            EndianUtilities.WriteBytesLittleEndian((ushort)0x100, names, 2);
            return RestartRecord(1, c, Dump(w, NtfsLogOperation.OpenAttributeTableDump, Table(0x28, 1)), 0x40,
                Dump(w, NtfsLogOperation.AttributeNamesDump, names), names.Length);
        }))
        {
            AssertRefused(v, "names dump is damaged");
        }

        // The checkpoint record itself is missing.
        using (var v = WithRestart((w, c) => RestartRecord(1, c + 0x2000)))
        {
            AssertRefused(v, "checkpoint record");
        }

        // An NTFS restart version DiscUtils does not know.
        using (var v = WithRestart((w, c) => RestartRecord(2, c)))
        {
            AssertRefused(v, "version 2.0");
        }
    }

    [Fact]
    public void ARestartAreaThatPointsAtTheWrongRecord_IsRefused()
    {
        // At a client record instead of a restart record, and at nothing (LSN 0).
        foreach (var (target, reason) in new (Func<TestJournalWriter, ulong>, string)[]
                 {
                     (w => w.Append(new TestLogRecord { Redo = NtfsLogOperation.Noop, Undo = NtfsLogOperation.Noop, TransactionId = 0x18 }), "not a restart record"),
                     (_ => 0, "not in the log"),
                     (w => w.LsnOf(0x100, 1), "not in the log"),
                 })
        {
            using var volume = Volume();
            var w = new TestJournalWriter(volume);
            w.Checkpoint(Attributes);
            w.Finish(target(w));
            AssertRefused(volume, reason);
        }
    }

    [Fact]
    public void ACheckpointWithAnEmptyDirtyPageTable_StillSetsThePageSize()
    {
        // No allocated entries, but entries for two clusters per page: records may name two clusters.
        using var probe = Volume();
        var a = FreeCluster(probe);
        using var volume = WithRestart((w, c) =>
        {
            var oat = Table(0x28, 3);
            foreach (var (offset, type) in new[] { (0x18, 0x80u), (0x40, 0xA0u), (0x68, 0x80u) })
            {
                TestJournalWriter.OpenAttributeEntryBytes(type, 1, 4096).AsSpan(4).CopyTo(oat.AsSpan(offset + 4));
            }

            var oatLsn = Dump(w, NtfsLogOperation.OpenAttributeTableDump, oat);
            var dirty = Dump(w, NtfsLogOperation.DirtyPageTableDump, Table(0x30, 1, allocated: 0));
            var record = Raw(a, Enumerable.Repeat((byte)0x21, 16).ToArray());
            record.Lcns = [a, a - 1];
            w.Append(record);
            w.Append(Forget());
            return RestartRecord(1, c, oatLsn, oat.Length, dirty: dirty, dirtyLength: 0x48);
        });

        var info = NtfsJournal.Replay(volume);
        Assert.Equal(NtfsJournalStatus.NeedsReplay, info.Status);
        Assert.Equal(0x21, Read(volume, a * 4096, 1)[0]);
    }

    [Fact]
    public void PreparedAndCommittedTransactions_AreKept_AndATransactionOlderThanTheCheckpointStartsRedo()
    {
        using var probe = Volume();
        var x = FreeCluster(probe);
        var y = FreeCluster(probe, 1);
        var z = FreeCluster(probe, 2);
        using var volume = Volume();
        volume.Position = z * 4096;
        volume.Write(Enumerable.Repeat((byte)0x33, 8).ToArray(), 0, 8);
        var w = new TestJournalWriter(volume);

        // A transaction that started before the checkpoint (its change is on disk) and committed: it starts the
        // redo pass, and is not rolled back.
        var early = w.Append(Raw(z, Enumerable.Repeat((byte)0x33, 8).ToArray(), tx: 0x90));
        var restart = w.Checkpoint(Attributes, transactions: [(0x90, TransactionState.Committed, early, early)], checkpointLsn: null);
        w.Append(Raw(x, Enumerable.Repeat((byte)0x11, 8).ToArray()));
        w.Append(new TestLogRecord { Redo = NtfsLogOperation.PrepareTransaction, Undo = NtfsLogOperation.Noop, TransactionId = Tx });
        w.Append(Raw(y, Enumerable.Repeat((byte)0x22, 8).ToArray(), tx: 0x68));
        w.Append(new TestLogRecord { Redo = NtfsLogOperation.CommitTransaction, Undo = NtfsLogOperation.Noop, TransactionId = 0x68 });
        w.Finish(restart);

        var info = NtfsJournal.Replay(volume);
        Assert.Equal(0, info.TransactionsToUndo);
        Assert.Equal(0x11, Read(volume, x * 4096, 1)[0]);
        Assert.Equal(0x22, Read(volume, y * 4096, 1)[0]);
        Assert.Equal(0x33, Read(volume, z * 4096, 1)[0]);
    }

    [Fact]
    public void AnAttributeOpenedWithTooLittleData_IsRefused()
    {
        using var volume = WithRecords(new TestLogRecord
        {
            Redo = NtfsLogOperation.OpenNonresidentAttribute, Undo = NtfsLogOperation.Noop, TransactionId = Tx, RedoData = new byte[8], TargetAttribute = 0x90,
        });
        AssertRefused(volume, "too little data");
    }

    [Theory]
    [InlineData("page size")]
    [InlineData("restart area")]
    [InlineData("no clients")]
    [InlineData("client in use")]
    public void RestartPages_ThatAreNotPlausible_AreNotUsed(string damage)
    {
        using var volume = WithRecords(Forget());
        var w = new TestJournalWriter(volume);
        PatchRestartPages(w, page =>
        {
            switch (damage)
            {
                case "page size": EndianUtilities.WriteBytesLittleEndian(100u, page, 0x14); break;
                case "restart area": EndianUtilities.WriteBytesLittleEndian((ushort)0x2C, page, 0x18); break;
                case "no clients": EndianUtilities.WriteBytesLittleEndian((ushort)0, page, 0x30 + 0x08); break;
                case "client in use": EndianUtilities.WriteBytesLittleEndian((ushort)3, page, 0x30 + 0x0C); break;
            }
        });
        AssertRefused(volume, "no valid restart page");
    }

    [Fact]
    public void ARecordWithAnImpossibleLength_OrATornContinuationPage_EndsTheLog()
    {
        foreach (var damage in new[] { "length", "negative length", "continuation" })
        {
            var scenario = JournalScenario.Create(ntfs => ntfs.CreateDirectory(@"x\y"), setup: ntfs =>
            {
                for (var i = 0; i < 60; i++)
                {
                    ntfs.CreateDirectory($"pre{i}");
                }
            });
            using var volume = JournalScenario.Copy(scenario.After);
            var writer = scenario.WriteJournal(volume, committed: true);

            // Find the commit (the last record) or the first record that spans pages, and damage the log there.
            var lsn = writer.LastLsn;
            var offset = writer.LsnToOffsetForTest(lsn);
            var page = new byte[TestJournalWriter.PageSize];
            if (damage != "continuation")
            {
                var pageOffset = offset & ~(long)(TestJournalWriter.PageSize - 1);
                writer.Storage.Read(pageOffset, page, 0, page.Length);
                Assert.True(JournalUtilities.RemoveFixups(page, 0, page.Length));
                EndianUtilities.WriteBytesLittleEndian(damage == "length" ? 0x7FFFFFFFu : 0xFFFFFFFFu, page, (int)(offset - pageOffset) + 0x18);
                JournalUtilities.ApplyFixups(page, 0, page.Length);
                writer.Storage.Write(pageOffset, page, 0, page.Length);
            }
            else
            {
                // The page a big record (an index buffer image) continues on, after some records of the transaction.
                var log = LfsLog.Open(writer.Storage, out _);
                var big = log.ReadForward(writer.LsnOf(writer.FirstPageOffset + 0x40, 1))
                    .Where(r => r.TransactionId == JournalScenario.Transaction)
                    .Skip(1)
                    .First(r => r.ClientData.Length > TestJournalWriter.PageSize);
                var continuation = (writer.LsnToOffsetForTest(big.Lsn) & ~(long)(TestJournalWriter.PageSize - 1)) + TestJournalWriter.PageSize;
                writer.Storage.Read(continuation, page, 0, page.Length);
                page[0x3FE] ^= 0xFF;
                writer.Storage.Write(continuation, page, 0, page.Length);
            }

            // The transaction lost its end: it is rolled back.
            var info = NtfsJournal.Analyze(volume);
            Assert.True(info.TransactionsToUndo == 1, $"{damage}: {info}");
        }
    }

    [Fact]
    public void ARecordHeaderThatDoesNotFitAtTheEndOfTheLog_ContinuesOnTheFirstPage()
    {
        using var volume = Volume();
        var target = FreeCluster(volume);
        var size = new TestJournalWriter(volume).FileSize;
        var w = new TestJournalWriter(volume, startOffset: size - (2 * TestJournalWriter.PageSize));
        var restart = w.Checkpoint(Attributes);

        // A filler that ends 16 bytes before the end of the last page: the next header cannot fit there.
        var fillerEnd = size - 0x10;
        var pagesCrossed = (fillerEnd / TestJournalWriter.PageSize) - (w.Position / TestJournalWriter.PageSize);
        var filler = (int)(fillerEnd - w.Position - 0x30 - 0x20 - (0x40 * pagesCrossed));
        w.Append(new TestLogRecord { Redo = NtfsLogOperation.Noop, Undo = NtfsLogOperation.Noop, TransactionId = 0x18, RedoData = new byte[filler & ~7] });
        Assert.Equal(fillerEnd, w.Position);
        var change = w.Append(Raw(target, Enumerable.Repeat((byte)0x5A, 32).ToArray()));
        Assert.Equal(w.FirstPageOffset + 0x40, w.LsnToOffsetForTest(change));
        w.Append(Forget());
        w.Finish(restart);

        NtfsJournal.Replay(volume);
        Assert.Equal(0x5A, Read(volume, target * 4096, 1)[0]);
    }

    [Fact]
    public void FreedPages_AreNotTouched_ByRedoOrUndo()
    {
        using var volume = Volume();
        var indexLcn = FirstIndexBuffer(volume);
        var index = Read(volume, indexLcn * 4096, 4096, logical: true);
        var entry = 0x18 + (int)EndianUtilities.ToUInt32LittleEndian(index, 0x18);
        var rawLcn = FreeCluster(volume);
        var bitsLcn = FreeCluster(volume, 1);
        var original = ((MemoryStream)volume).ToArray();
        var mftLcn = 0L;
        using (var ntfs = new NtfsFileSystem(JournalScenario.Copy(original), NtfsJournalMode.Ignore))
        {
            mftLcn = ntfs.PathToClusters("$MFT").First().Offset + 4;
        }

        var w = new TestJournalWriter(volume);
        var restart = w.Checkpoint(Attributes);
        w.Append(Op(NtfsLogOperation.UpdateResidentValue, new byte[8], Mft, 4, [mftLcn], 0x38, 0x18, undo: NtfsLogOperation.UpdateResidentValue, undoData: new byte[8]));
        w.Append(Op(NtfsLogOperation.UpdateFileNameAllocation, new byte[0x38], Index, 0, [indexLcn], attributeOffset: entry));
        w.Append(Op(NtfsLogOperation.UpdateNonresidentValue, new byte[4096], Index, 9999, [indexLcn + 1000]));
        w.Append(Op(NtfsLogOperation.SetBitsInNonresidentBitMap, BitConverter.GetBytes(1L << 32), Data, 77, [bitsLcn]));
        w.Append(Raw(rawLcn, Enumerable.Repeat((byte)0x77, 16).ToArray()));
        foreach (var lcn in new[] { mftLcn, indexLcn, indexLcn + 1000, bitsLcn, rawLcn })
        {
            w.Append(DeleteClusters(lcn));
        }

        w.Finish(restart);
        var info = NtfsJournal.Replay(volume);
        Assert.Equal(NtfsJournalStatus.NothingToReplay, info.Status);
        Assert.True(original.AsSpan(0, (int)(mftLcn * 4096)).SequenceEqual(((MemoryStream)volume).ToArray().AsSpan(0, (int)(mftLcn * 4096))));
    }

    [Fact]
    public void PagesThatAreUpToDate_EmptyWrites_AndBitsAlreadySet_ChangeNothing()
    {
        using var volume = Volume();
        var indexLcn = FirstIndexBuffer(volume);
        var bitsLcn = FreeCluster(volume);
        volume.Position = bitsLcn * 4096;
        volume.WriteByte(0xFF);

        // The index buffer already carries an LSN newer than the record.
        var index = Read(volume, indexLcn * 4096, 4096, logical: true);
        EndianUtilities.WriteBytesLittleEndian(ulong.MaxValue - 1, index, 8);
        var protectedIndex = (byte[])index.Clone();
        JournalUtilities.ApplyFixups(protectedIndex, 0, protectedIndex.Length);
        volume.Position = indexLcn * 4096;
        volume.Write(protectedIndex, 0, protectedIndex.Length);
        var entry = 0x18 + (int)EndianUtilities.ToUInt32LittleEndian(index, 0x18);

        using var result = WithRecordsOn(volume,
            Op(NtfsLogOperation.UpdateFileNameAllocation, new byte[0x38], Index, 0, [indexLcn], attributeOffset: entry),
            Op(NtfsLogOperation.UpdateNonresidentValue, [], Data, 5, [bitsLcn]),
            Op(NtfsLogOperation.SetBitsInNonresidentBitMap, BitConverter.GetBytes(0x0000000400000000L), Data, 6, [bitsLcn]),
            Forget());
        Assert.Equal(NtfsJournalStatus.NothingToReplay, NtfsJournal.Analyze(result).Status);
    }

    private static MemoryStream WithRecordsOn(MemoryStream volume, params TestLogRecord[] records)
    {
        var w = new TestJournalWriter(volume);
        var restart = w.Checkpoint(Attributes);
        foreach (var r in records)
        {
            w.Append(r);
        }

        w.Finish(restart);
        return volume;
    }

    [Fact]
    public void AnIndexAllocationWithoutABufferSize_UsesTheVolumes()
    {
        using var volume = Volume();
        var indexLcn = FirstIndexBuffer(volume);
        var index = Read(volume, indexLcn * 4096, 4096, logical: true);
        var entry = 0x18 + (int)EndianUtilities.ToUInt32LittleEndian(index, 0x18);
        var w = new TestJournalWriter(volume);
        var restart = w.Checkpoint([(Index, 0xA0, 0x0005000000000005UL, 0, "$I30")]);
        w.Append(Op(NtfsLogOperation.UpdateFileNameAllocation, Enumerable.Repeat((byte)0x44, 0x38).ToArray(), Index, 0, [indexLcn], attributeOffset: entry));
        w.Append(Forget());
        w.Finish(restart);

        NtfsJournal.Replay(volume);
        Assert.Equal(0x44, Read(volume, indexLcn * 4096, 4096, logical: true)[entry + 0x18]);
    }

    [Fact]
    public void PagesBeyondTheirClusters_OrPartlyFreed_AreRefused()
    {
        using var probe = Volume();
        long mftLcn;
        using (var ntfs = new NtfsFileSystem(JournalScenario.Copy(Base.Value), NtfsJournalMode.Ignore))
        {
            mftLcn = ntfs.PathToClusters("$MFT").First().Offset;
        }

        // A file record that would run past the cluster the record names.
        using (var volume = WithRecords(Op(NtfsLogOperation.UpdateResidentValue, new byte[8], Mft, 0, [mftLcn], 0x38, 0x18, blockOffset: 7)))
        {
            AssertRefused(volume, "beyond the clusters it names");
        }

        // An 8K index buffer whose second cluster was freed.
        var a = FreeCluster(probe);
        var b = a - 1;
        using (var volume = Volume())
        {
            var w = new TestJournalWriter(volume);
            var restart = w.Checkpoint(
                [(Index, 0xA0, 0x0005000000000005UL, 8192, "$I30")],
                [(Index, 0, 0, [a, b])],
                clustersPerPage: 2);
            w.Append(Op(NtfsLogOperation.UpdateFileNameAllocation, new byte[0x38], Index, 0, [a, b], attributeOffset: 0x40));
            w.Append(DeleteClusters(b));
            w.Append(Forget());
            w.Finish(restart);
            AssertRefused(volume, "partly freed");
        }
    }

    [Fact]
    public void FileRecordsZeroToThree_AreMirrored_AlsoWhenTheySpanSmallClusters()
    {
        // 512-byte clusters: the first four records span eight clusters, most of which the replay does not change.
        using var volume = NtfsJournalWindowsSampleTest.LoadVolume("s512");
        NtfsJournal.Replay(volume);
        var boot = Read(volume, 0, 512);
        var mft = EndianUtilities.ToInt64LittleEndian(boot, 0x30);
        var mirror = EndianUtilities.ToInt64LittleEndian(boot, 0x38);
        var record1 = Read(volume, (mft * 512) + 1024, 1024, logical: true);
        var changed = (byte[])record1.Clone();
        var used = (int)EndianUtilities.ToUInt32LittleEndian(changed, 0x18);

        // Continue with LSNs past the ones Windows left on the pages.
        var w = new TestJournalWriter(volume, sequence: 1000);
        var restart = w.Checkpoint([(Mft, 0x80, 0x0001000000000000UL, 0, "")]);
        w.Append(Op(NtfsLogOperation.UpdateResidentValue, BitConverter.GetBytes(0x01D1111111111111L), Mft, 2, [mft + 2, mft + 3],
            EndianUtilities.ToUInt16LittleEndian(changed, 0x14), 0x18, undo: NtfsLogOperation.UpdateResidentValue, undoData: changed.AsSpan(0x50, 8).ToArray()));
        w.Append(Forget());
        w.Finish(restart);

        NtfsJournal.Replay(volume);
        Assert.True(used > 0);
        Assert.True(Read(volume, mft * 512, 4096).AsSpan().SequenceEqual(Read(volume, mirror * 512, 4096)));
        Assert.Equal(0x01D1111111111111L, EndianUtilities.ToInt64LittleEndian(Read(volume, (mft * 512) + 1024, 1024, logical: true), 0x50));
    }

    [Fact]
    public void LogFileRuns_AreRead_AndAccessOutsideThemIsRefused()
    {
        // Two runs, the second before the first (a negative offset), then the end.
        var runs = new byte[] { 0x21, 0x10, 0x00, 0x01, 0x11, 0x08, 0xF0, 0x00 };
        var extents = LogFileStorage.ParseRuns(runs, 0, runs.Length);
        Assert.Equal([(0L, 0x100L, 0x10L), (0x10L, 0xF0L, 8L)], extents);

        // A run list that fills its space without the end marker, and an eight-byte offset (nothing to extend).
        Assert.Equal([(0L, 0x10L, 8L)], LogFileStorage.ParseRuns([0x11, 0x08, 0x10], 0, 3));
        byte[] wide = [0x81, 0x02, 0x20, 0, 0, 0, 0, 0, 0, 0, 0];
        Assert.Equal([(0L, 0x20L, 2L)], LogFileStorage.ParseRuns(wide, 0, wide.Length));

        using var volume = Volume();
        var layout = JournalVolumeLayout.Read(volume);
        var storage = LogFileStorage.Open(volume, layout);
        Assert.Throws<JournalException>(() => storage.Read(storage.Length, new byte[16], 0, 16));
        Assert.Throws<JournalException>(() => storage.Read(-1, new byte[16], 0, 16));

        // $LogFile's data longer than its clusters: the end cannot be read.
        long position;
        using (var ntfs = new NtfsFileSystem(JournalScenario.Copy(Base.Value), NtfsJournalMode.Ignore))
        {
            position = (ntfs.PathToClusters("$MFT").First().Offset * 4096) + 2048;
        }

        var record = Read(volume, position, 1024, logical: true);
        var attr = (int)EndianUtilities.ToUInt16LittleEndian(record, 0x14);
        while (EndianUtilities.ToUInt32LittleEndian(record, attr) != 0x80)
        {
            attr += (int)EndianUtilities.ToUInt32LittleEndian(record, attr + 4);
        }

        EndianUtilities.WriteBytesLittleEndian(EndianUtilities.ToUInt64LittleEndian(record, attr + 0x30) * 2, record, attr + 0x30);
        JournalUtilities.ApplyFixups(record, 0, record.Length);
        volume.Position = position;
        volume.Write(record, 0, record.Length);
        var longer = LogFileStorage.Open(volume, layout);
        var ex = Assert.Throws<JournalException>(() => longer.Read(longer.Length - 4096, new byte[16], 0, 16));
        Assert.Contains("no cluster", ex.Message);
    }

    [Theory]
    [InlineData(0x7FFFFFFFL)]
    [InlineData(-1L)]
    public void AMirrorClusterOutsideTheVolume_IsNoFallback(long mirrorCluster)
    {
        using var volume = Volume();
        var boot = Read(volume, 0, 512);
        EndianUtilities.WriteBytesLittleEndian(mirrorCluster, boot, 0x38);
        volume.Position = 0;
        volume.Write(boot, 0, boot.Length);
        long position;
        using (var ntfs = new NtfsFileSystem(JournalScenario.Copy(Base.Value), NtfsJournalMode.Ignore))
        {
            position = (ntfs.PathToClusters("$MFT").First().Offset * 4096) + 2048;
        }

        volume.Position = position + 0x1FE;
        volume.WriteByte(0x55);
        AssertRefused(volume, "Neither");
    }

    [Fact]
    public void SmallThings_NotAVolume_FixupLayout_AndARecordOffsetPastTheEnd()
    {
        Assert.Throws<InvalidFileSystemException>(() => NtfsJournal.Analyze(new MemoryStream(new byte[64 * 1024])));

        var record = new byte[1000];
        EndianUtilities.WriteBytesLittleEndian((ushort)0x30, record, 4);
        EndianUtilities.WriteBytesLittleEndian((ushort)3, record, 6);
        Assert.False(JournalUtilities.HasFixupLayout(record, 0, 1000));
        Assert.False(JournalUtilities.RemoveFixups(record, 0, 1000));

        // An attribute offset past the end marker of a file record.
        var page = new byte[1024];
        EndianUtilities.WriteBytesLittleEndian(JournalUtilities.SignatureFile, page, 0);
        EndianUtilities.WriteBytesLittleEndian((ushort)0x38, page, 0x14);
        EndianUtilities.WriteBytesLittleEndian(0xFFFFFFFFu, page, 0x38);
        EndianUtilities.WriteBytesLittleEndian(0x40u, page, 0x18);
        var log = new TestLogRecord { Redo = NtfsLogOperation.DeleteAttribute, RecordOffset = 0x60, Lcns = [1] };
        var parsed = NtfsLogRecord.TryParse(new LfsRecord { Lsn = 1, ClientData = log.ClientData() }, out _);
        Assert.Throws<JournalException>(() => NtfsLogActions.ApplyToFileRecord(parsed.RedoOperation, parsed, page, parsed.RedoData));

        // Data stored only partly: the rest counts as zeros.
        var partial = new TestLogRecord { Redo = NtfsLogOperation.UpdateResidentValue, RedoData = [1, 2, 3, 4], RedoLength = 12, Lcns = [1] };
        var p2 = NtfsLogRecord.TryParse(new LfsRecord { Lsn = 1, ClientData = partial.ClientData() }, out _);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 0, 0, 0, 0, 0, 0, 0, 0 }, p2.RedoData.ToArray());
    }

    [Fact]
    public void AClusterUsedAsTwoKindsOfPage_IsRefused()
    {
        using var probe = Volume();
        var x = FreeCluster(probe);

        // The same bytes as a file record and as an index buffer.
        using (var volume = WithRecords(
                   Op(NtfsLogOperation.InitializeFileRecordSegment, new byte[0x30], Mft, 0, [x]),
                   Op(NtfsLogOperation.UpdateFileNameAllocation, new byte[0x38], Index, 0, [x], attributeOffset: 0x40)))
        {
            AssertRefused(volume, "both as FileRecord and as IndexBuffer");
        }

        // A cluster written raw and as a file record.
        using (var volume = WithRecords(
                   Raw(x, new byte[8]),
                   Op(NtfsLogOperation.InitializeFileRecordSegment, new byte[0x30], Mft, 0, [x])))
        {
            AssertRefused(volume, "both as Raw and as FileRecord");
        }
    }

    [Fact]
    public void ATornFileRecord_IsOnlyRebuilt_NeverChanged()
    {
        using var volume = Volume();
        var x = FreeCluster(volume);
        var torn = new byte[1024];
        EndianUtilities.WriteBytesLittleEndian(JournalUtilities.SignatureFile, torn, 0);
        EndianUtilities.WriteBytesLittleEndian((ushort)0x30, torn, 4);
        EndianUtilities.WriteBytesLittleEndian((ushort)3, torn, 6);
        EndianUtilities.WriteBytesLittleEndian((ushort)5, torn, 0x30);
        EndianUtilities.WriteBytesLittleEndian((ushort)5, torn, 0x1FE);
        EndianUtilities.WriteBytesLittleEndian((ushort)4, torn, 0x3FE);
        volume.Position = x * 4096;
        volume.Write(torn, 0, torn.Length);
        using var result = WithRecordsOn(volume, Op(NtfsLogOperation.UpdateResidentValue, new byte[8], Mft, 0, [x], 0x38, 0x18), Forget());
        AssertRefused(result, "damaged");
    }

    [Fact]
    public void ANamesDumpWithoutItsTerminator_IsRead()
    {
        using var volume = WithRestart((w, c) =>
        {
            var names = new byte[4 + 8];
            EndianUtilities.WriteBytesLittleEndian((ushort)0x18, names, 0);
            EndianUtilities.WriteBytesLittleEndian((ushort)8, names, 2);
            Encoding.Unicode.GetBytes("$I30").CopyTo(names, 4);
            return RestartRecord(1, c, Dump(w, NtfsLogOperation.OpenAttributeTableDump, Table(0x28, 1)), 0x40,
                Dump(w, NtfsLogOperation.AttributeNamesDump, names), names.Length);
        });
        Assert.Equal(NtfsJournalStatus.NothingToReplay, NtfsJournal.Analyze(volume).Status);
    }

    [Fact]
    public void AnUndoOnAFreedCluster_IsSkipped()
    {
        using var volume = Volume();
        var x = FreeCluster(volume);
        var after = Enumerable.Repeat((byte)0x66, 16).ToArray();
        volume.Position = x * 4096;
        volume.Write(after, 0, after.Length);
        var w = new TestJournalWriter(volume);
        var restart = w.Checkpoint(Attributes);
        var change = w.Append(Raw(x, after));
        var free = DeleteClusters(x);
        free.UndoNextLsn = change;
        w.Append(free);
        w.Finish(restart);

        var info = NtfsJournal.Replay(volume);
        Assert.Equal(NtfsJournalStatus.NothingToReplay, info.Status);
        Assert.True(Read(volume, x * 4096, 16).SequenceEqual(after));
    }

    [Fact]
    public void AFixupArrayThatDoesNotDivideTheRecord_IsNoLayout()
    {
        var record = new byte[1000];
        EndianUtilities.WriteBytesLittleEndian((ushort)0x30, record, 4);
        EndianUtilities.WriteBytesLittleEndian((ushort)4, record, 6);
        Assert.False(JournalUtilities.HasFixupLayout(record, 0, 1000));
    }
}
