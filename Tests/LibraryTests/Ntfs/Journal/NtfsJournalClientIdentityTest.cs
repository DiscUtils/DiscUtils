using System;
using System.IO;
using System.Text;
using DiscUtils.Ntfs;
using DiscUtils.Ntfs.Journal;
using static DiscUtils.Ntfs.Journal.JournalUtilities;

namespace LibraryTests.Ntfs.Journal;

public partial class NtfsJournalEdgeCaseTest
{
    private const int RestartArea = 0x30;
    private const int FirstClient = RestartArea + 0x40;
    private const int SecondClient = FirstClient + 0xA0;
    private const ushort ActiveClientSequence = 7;

    [Fact]
    public void AClientRecordWithBothIdentityFieldsMatchingIsReplayed()
    {
        using var volume = Volume();
        var target = FreeCluster(volume);
        var writer = new TestJournalWriter(volume) { ClientSequenceNumber = ActiveClientSequence };
        var restart = writer.Checkpoint(Attributes);
        writer.Append(Raw(target, Pattern));
        writer.Append(Forget());
        writer.Finish(restart);

        var info = NtfsJournal.Replay(volume);
        Assert.True(info.Replayed);
        Assert.Equal(1, info.RecordsToRedo);
        Assert.Equal(Pattern, Read(volume, target * writer.Layout.BytesPerCluster, Pattern.Length));
        Assert.Equal(NtfsJournalStatus.Clean, NtfsJournal.Analyze(volume).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnUnrelatedForgetCannotFinishAnNtfsTransaction(bool anotherSlot)
    {
        using var volume = Volume();
        var writer = new TestJournalWriter(volume) { ClientSequenceNumber = ActiveClientSequence };
        var restart = writer.Checkpoint(Attributes);
        writer.Append(Raw(FreeCluster(volume), Pattern));
        AppendUnrelated(writer, Forget(), anotherSlot);
        writer.Finish(restart);
        if (anotherSlot)
        {
            AddFreeClientSlot(writer);
        }

        var info = NtfsJournal.Analyze(volume);
        Assert.Equal(NtfsJournalStatus.NothingToReplay, info.Status);
        Assert.Equal(1, info.RecordsToUndo);
        Assert.Equal(1, info.TransactionsToUndo);
        Assert.False(info.CanReplayPersistently);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnUnrelatedRecordCannotFreeAnNtfsDirtyPage(bool anotherSlot)
    {
        using var volume = Volume();
        var target = FreeCluster(volume);
        var writer = new TestJournalWriter(volume) { ClientSequenceNumber = ActiveClientSequence };
        var restart = writer.Checkpoint(Attributes);
        writer.Append(Raw(target, Pattern));
        writer.Append(Forget());
        var last = AppendUnrelated(writer, DeleteClusters(target), anotherSlot);
        writer.Finish(restart);
        if (anotherSlot)
        {
            AddFreeClientSlot(writer);
        }

        var info = NtfsJournal.ReplayInMemory(volume, out var overlay);
        using var recovered = overlay;
        Assert.Equal(NtfsJournalStatus.NeedsReplay, info.Status);
        Assert.Equal(last, info.LastLsn);
        Assert.Equal(Pattern, Read(recovered, target * writer.Layout.BytesPerCluster, Pattern.Length));

        Assert.Equal(!anotherSlot, info.CanReplayPersistently);
        if (anotherSlot)
        {
            AssertMultiSlotResetRefused(volume);
        }
        else
        {
            Assert.True(NtfsJournal.Replay(volume).Replayed);
            Assert.Equal(Pattern, Read(volume, target * writer.Layout.BytesPerCluster, Pattern.Length));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RedoSkipsUnrelatedRecordsBeforeTheCheckpoint(bool anotherSlot)
    {
        using var volume = Volume();
        var target = FreeCluster(volume);
        var writer = new TestJournalWriter(volume) { ClientSequenceNumber = ActiveClientSequence };
        var first = writer.Append(Raw(target, Pattern));
        AppendUnrelated(writer, Raw(target, [0xA5]), anotherSlot);
        AppendUnrelated(writer, new TestLogRecord { Redo = (NtfsLogOperation)0xFFFF }, anotherSlot);
        var restart = writer.Checkpoint(Attributes, dirtyPages: [(Data, target, first, [target])]);
        writer.Finish(restart);
        if (anotherSlot)
        {
            AddFreeClientSlot(writer);
        }

        var info = NtfsJournal.ReplayInMemory(volume, out var overlay);
        using var recovered = overlay;
        Assert.Equal(NtfsJournalStatus.NeedsReplay, info.Status);
        Assert.Equal(1, info.RecordsToRedo);
        Assert.Equal(Pattern, Read(recovered, target * writer.Layout.BytesPerCluster, Pattern.Length));
        Assert.Equal(new byte[Pattern.Length], Read(volume, target * writer.Layout.BytesPerCluster, Pattern.Length));
    }

    [Theory]
    [InlineData(-1, "NTFS restart record")]
    [InlineData(0x08, "checkpoint record")]
    [InlineData(0x10, "open attribute table dump")]
    [InlineData(0x18, "attribute names dump")]
    [InlineData(0x20, "dirty page table dump")]
    [InlineData(0x28, "transaction table dump")]
    public void CheckpointReferencesCannotNameAnotherClient(int restartField, string role)
    {
        using var volume = Volume();
        var target = FreeCluster(volume);
        var writer = new TestJournalWriter(volume);
        var first = writer.Append(Raw(target, Pattern));
        var restart = writer.Checkpoint(Attributes, dirtyPages: [(Data, target, first, [target])],
            transactions: [(Tx, TransactionState.Committed, first, first)]);
        writer.Finish(restart);
        AddFreeClientSlot(writer);
        var log = LfsLog.Open(writer.Storage, out _);
        var lsn = restartField < 0 ? restart : U64(log.ReadRecord(restart).ClientData, restartField);
        Assert.NotEqual(0UL, lsn);
        PatchJournalRecord(writer, lsn, (page, offset) => Put16(page, offset + 0x1E, 1));

        AssertReplayRefused(volume, $"{role} at {lsn:X} does not belong to the active NTFS client");
    }

    [Theory]
    [InlineData(false, "not in the log")]
    [InlineData(true, "wrong LFS record type")]
    public void ADeclaredTableDumpMustReferenceAnLfsClientRecord(bool wrongType, string reason)
    {
        using var volume = WithRestart((writer, checkpoint) =>
        {
            var dump = new TestLogRecord { Redo = NtfsLogOperation.OpenAttributeTableDump, RedoData = Table(0x28, 1) };
            var lsn = wrongType ? writer.AppendRaw(dump.ClientData(), LfsRecord.TypeClientRestart, 0, 0) : 0;
            return RestartRecord(1, checkpoint, oat: lsn, oatLength: dump.RedoData.Length);
        });

        AssertReplayRefused(volume, reason);
    }

    [Theory]
    [InlineData("dirty page")]
    [InlineData("first record")]
    [InlineData("previous record")]
    public void CheckpointHistoryCannotNameAnObsoleteClient(string reference)
    {
        using var volume = Volume();
        var target = FreeCluster(volume);
        var writer = new TestJournalWriter(volume) { ClientSequenceNumber = ActiveClientSequence };
        var first = writer.Append(Raw(target, Pattern));
        var obsolete = AppendUnrelated(writer, Raw(target, [0xA5]), anotherSlot: false);
        var restart = writer.Checkpoint(Attributes,
            dirtyPages: [(Data, target, first, [target]), (Data, target + 1, reference == "dirty page" ? obsolete : 0, [target + 1])],
            transactions: [(Tx, TransactionState.Committed, reference == "first record" ? obsolete : first,
                reference == "previous record" ? obsolete : first)]);
        writer.Finish(restart);

        AssertReplayRefused(volume, "does not belong to the active NTFS client");
    }

    [Fact]
    public void ALogRecordCannotNameAClientIndexOutsideTheRestartArray()
    {
        using var volume = Volume();
        var writer = new TestJournalWriter(volume);
        var restart = writer.Checkpoint(Attributes);
        writer.Append(Raw(FreeCluster(volume), Pattern));
        writer.Append(Forget());
        AppendUnrelated(writer, Forget(), anotherSlot: true);
        writer.Finish(restart);

        AssertReplayRefused(volume, "client index 1 outside the restart client array");
    }

    [Theory]
    [InlineData(FirstClient + 0x12, 0)]
    [InlineData(FirstClient + 0x12, 2)]
    [InlineData(FirstClient + 0x10, 0)]
    [InlineData(RestartArea + 0x0A, 0)]
    public void MalformedRestartClientListsAreRefused(int offset, int value)
    {
        using var volume = WithRecords();
        PatchRestartPages(new TestJournalWriter(volume), page => Put16(page, offset, (ushort)value));

        AssertReplayRefused(volume, "invalid LFS client list");
    }

    [Theory]
    [InlineData("Other", "no NTFS client in use")]
    [InlineData("NTFS", "more than one NTFS client in use")]
    public void TheInUseListMustIdentifyExactlyOneNtfsClient(string name, string reason)
    {
        using var volume = WithRecords();
        PatchRestartPages(new TestJournalWriter(volume), page =>
        {
            if (name == "NTFS")
            {
                PutOtherClientAtHead(page, name);
            }
            else
            {
                PutClientName(page, FirstClient, name);
            }
        });

        AssertReplayRefused(volume, reason);
    }

    [Fact]
    public void AnActiveNtfsClientCanFollowAnUnrelatedClientInTheInUseList()
    {
        using var volume = Volume();
        var target = FreeCluster(volume);
        var writer = new TestJournalWriter(volume);
        var restart = writer.Checkpoint(Attributes);
        writer.Append(Raw(target, Pattern));
        writer.Append(Forget());
        writer.AppendRaw([0x01], LfsRecord.TypeClientRecord, Tx, 0, clientIndex: 1);
        writer.Finish(restart);
        PatchRestartPages(writer, page => PutOtherClientAtHead(page, "Other"));

        var info = NtfsJournal.ReplayInMemory(volume, out var overlay);
        using var recovered = overlay;
        Assert.Equal(NtfsJournalStatus.NeedsReplay, info.Status);
        Assert.False(info.CanReplayPersistently);
        Assert.Equal(Pattern, Read(recovered, target * writer.Layout.BytesPerCluster, Pattern.Length));

        AssertMultiSlotResetRefused(volume);
    }

    [Fact]
    public void EqualLsnRestartPagesMustAgreeOnTheActiveNtfsClientSequence()
    {
        using var volume = WithRecords();
        var writer = new TestJournalWriter(volume);
        PatchRestartPages(writer, page => PutOtherClientAtHead(page, "Other"));
        var second = new byte[TestJournalWriter.PageSize];
        writer.Storage.Read(TestJournalWriter.PageSize, second, 0, second.Length);
        Assert.True(RemoveFixups(second, 0, second.Length));
        Put16(second, FirstClient + 0x14, 1);
        ApplyFixups(second, 0, second.Length);
        writer.Storage.Write(TestJournalWriter.PageSize, second, 0, second.Length);

        AssertReplayRefused(volume, "disagree about the checkpoint at the same LSN");
    }

    private static ulong AppendUnrelated(TestJournalWriter writer, TestLogRecord record, bool anotherSlot) =>
        writer.AppendRaw(record.ClientData(), LfsRecord.TypeClientRecord, record.TransactionId, record.UndoNextLsn,
            clientIndex: (ushort)(anotherSlot ? 1 : 0),
            clientSequence: anotherSlot ? writer.ClientSequenceNumber : (ushort)(writer.ClientSequenceNumber - 1));

    private static void AddFreeClientSlot(TestJournalWriter writer) => PatchRestartPages(writer, page =>
    {
        Put16(page, RestartArea + 0x08, 2);
        Put16(page, RestartArea + 0x0A, 1);
        Put16(page, SecondClient + 0x10, LfsRestartPage.NoClient);
        Put16(page, SecondClient + 0x12, LfsRestartPage.NoClient);
    });

    private static void PutOtherClientAtHead(byte[] page, string name)
    {
        Put16(page, RestartArea + 0x08, 2);
        Put16(page, RestartArea + 0x0C, 1);
        Put16(page, FirstClient + 0x10, 1);
        Put16(page, SecondClient + 0x10, LfsRestartPage.NoClient);
        Put16(page, SecondClient + 0x12, 0);
        PutClientName(page, SecondClient, name);
    }

    private static void PutClientName(byte[] page, int offset, string name)
    {
        var bytes = Encoding.Unicode.GetBytes(name);
        Put32(page, offset + 0x1C, (uint)bytes.Length);
        bytes.CopyTo(page, offset + 0x20);
    }

    private static void AssertMultiSlotResetRefused(MemoryStream volume)
    {
        var before = volume.ToArray();
        Assert.Contains("single LFS client slot", Assert.Throws<IOException>(() => NtfsJournal.Replay(volume)).Message);
        Assert.True(before.AsSpan().SequenceEqual(volume.ToArray()));
    }

    private static void PatchJournalRecord(TestJournalWriter writer, ulong lsn, Action<byte[], int> patch)
    {
        var offset = writer.LsnToOffsetForTest(lsn);
        var inPage = (int)(offset % TestJournalWriter.PageSize);
        var page = new byte[TestJournalWriter.PageSize];
        writer.Storage.Read(offset - inPage, page, 0, page.Length);
        Assert.True(RemoveFixups(page, 0, page.Length));
        patch(page, inPage);
        ApplyFixups(page, 0, page.Length);
        writer.Storage.Write(offset - inPage, page, 0, page.Length);
    }
}
