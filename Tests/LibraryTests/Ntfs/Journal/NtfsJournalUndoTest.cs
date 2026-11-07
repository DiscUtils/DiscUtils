using System;
using System.IO;
using System.Linq;
using DiscUtils.Ntfs;
using DiscUtils.Ntfs.Journal;
using static DiscUtils.Ntfs.Journal.JournalUtilities;

namespace LibraryTests.Ntfs.Journal;

public partial class NtfsJournalEdgeCaseTest
{
    [Fact]
    public void UndoAppliesEveryInverseOnTheSameFileRecord()
    {
        using var volume = Volume();
        var writer = new TestJournalWriter(volume);
        var position = writer.Layout.MftCluster * writer.Layout.BytesPerCluster;
        var original = Read(volume, position, writer.Layout.MftRecordSize, logical: true);
        var attribute = U16(original, 0x14);
        var originalValue = original.AsSpan(attribute + 0x18, 8).ToArray();
        var middle = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var final = new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 };
        var restart = writer.Checkpoint(Attributes);
        var first = writer.Append(FirstMftValueUpdate(writer, original, middle, originalValue));
        var last = FirstMftValueUpdate(writer, original, final, middle);
        last.UndoNextLsn = first;
        var lastLsn = writer.Append(last);
        writer.Finish(restart);
        var before = volume.ToArray();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var info = NtfsJournal.ReplayInMemory(volume, out var overlay);
            using var recovered = overlay;
            Assert.Equal(NtfsJournalStatus.NeedsReplay, info.Status);
            Assert.False(info.CanReplayPersistently);
            Assert.True(info.ReplayedInMemory);
            Assert.Equal(2, info.RecordsToUndo);
            var restored = Read(recovered, position, original.Length, logical: true);
            Assert.Equal(originalValue, restored.AsSpan(attribute + 0x18, 8).ToArray());
            Assert.Equal(lastLsn, U64(restored, 8));
        }

        Assert.Contains("metadata undo", Assert.Throws<IOException>(() => NtfsJournal.Replay(volume)).Message);
        Assert.Equal(before, volume.ToArray());
    }

    private static TestLogRecord FirstMftValueUpdate(TestJournalWriter writer, byte[] record, byte[] redo, byte[] undo) =>
        Op(NtfsLogOperation.UpdateResidentValue, redo, Mft, 0, [writer.Layout.MftCluster],
            recordOffset: U16(record, 0x14), attributeOffset: 0x18, undo: NtfsLogOperation.UpdateResidentValue, undoData: undo);

    [Fact]
    public void PendingUndoCannotResetTheJournalEvenWhenTheNetChangeIsZero()
    {
        using var volume = Volume();
        using var dirty = WithRecordsOn(volume, Raw(FreeCluster(volume), Pattern));
        var before = dirty.ToArray();
        var info = NtfsJournal.Analyze(dirty);
        Assert.Equal(NtfsJournalStatus.NothingToReplay, info.Status);
        Assert.Equal(1, info.TransactionsToUndo);
        Assert.False(info.CanReplayPersistently);
        Assert.Contains("metadata undo", Assert.Throws<IOException>(() => NtfsJournal.Replay(dirty)).Message);
        Assert.Equal(before, dirty.ToArray());
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(0x100UL)]
    public void APageLsnBeyondTheLogCannotStandInForUndoProgress(ulong beyondEnd)
    {
        using var volume = Volume();
        var writer = new TestJournalWriter(volume);
        var position = writer.Layout.MftCluster * writer.Layout.BytesPerCluster;
        var record = Read(volume, position, writer.Layout.MftRecordSize, logical: true);
        var attribute = U16(record, 0x14);
        var old = record.AsSpan(attribute + 0x18, 8).ToArray();
        var restart = writer.Checkpoint(Attributes);
        writer.Append(FirstMftValueUpdate(writer, record, Pattern.AsSpan(0, 8).ToArray(), old));
        writer.Finish(restart);
        Put64(record, 8, writer.LastLsn + beyondEnd);
        ApplyFixups(record, 0, record.Length);
        volume.Position = position;
        volume.Write(record, 0, record.Length);

        AssertReplayRefused(volume, "cannot establish the undo history");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActiveTransactionsCanBeUndoneOnlyOnDisjointClusters(bool overlaps)
    {
        using var volume = Volume();
        var x = FreeCluster(volume);
        var y = overlaps ? x : FreeCluster(volume, 1);
        var after = Enumerable.Repeat((byte)0x75, Pattern.Length).ToArray();
        using var dirty = WithRecordsOn(volume, Raw(x, Pattern), Raw(y, after, tx: 0x68));
        if (overlaps)
        {
            AssertReplayRefused(dirty, "overlaps cluster");
        }
        else
        {
            var info = NtfsJournal.Analyze(dirty);
            Assert.Equal(NtfsJournalStatus.NothingToReplay, info.Status);
            Assert.Equal(2, info.TransactionsToUndo);
            Assert.Equal(2, info.RecordsToUndo);
            Assert.False(info.CanReplayPersistently);
        }
    }

    [Fact]
    public void ARecordedCompensationCanBeRedoneWithoutInventingUndoProgress()
    {
        using var volume = Volume();
        var writer = new TestJournalWriter(volume);
        var target = FreeCluster(volume);
        var restart = writer.Checkpoint(Attributes);
        writer.Append(Raw(target, Pattern));
        writer.Append(Op(NtfsLogOperation.UpdateNonresidentValue, new byte[Pattern.Length], Data, target, [target],
            undo: NtfsLogOperation.CompensationLogRecord));
        writer.Finish(restart);
        volume.Position = target * 4096;
        volume.Write(Pattern, 0, Pattern.Length);

        var info = NtfsJournal.Replay(volume);
        Assert.Equal(0, info.TransactionsToUndo);
        Assert.True(info.CanReplayPersistently);
        Assert.True(info.Replayed);
        Assert.Equal(new byte[Pattern.Length], Read(volume, target * 4096, Pattern.Length));
        Assert.Equal(NtfsJournalStatus.Clean, NtfsJournal.Analyze(volume).Status);
    }

    [Fact]
    public void ACompensationWithoutAMetadataMappingCannotDiscardUndo()
    {
        using var volume = Volume();
        var target = FreeCluster(volume);
        using var dirty = WithRecordsOn(volume, Raw(target, Pattern),
            Op(NtfsLogOperation.UpdateNonresidentValue, new byte[Pattern.Length], Data, target, [],
                undo: NtfsLogOperation.CompensationLogRecord));
        AssertReplayRefused(dirty, "changes metadata without a cluster mapping");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void APreparedTransactionWithoutAnOutcomeIsRefused(bool inCheckpoint)
    {
        using var volume = Volume();
        var writer = new TestJournalWriter(volume);
        var record = Raw(FreeCluster(volume), Pattern);
        ulong restart;
        if (inCheckpoint)
        {
            var lsn = writer.Append(record);
            restart = writer.Checkpoint(Attributes, transactions: [(Tx, TransactionState.Prepared, lsn, lsn)]);
        }
        else
        {
            restart = writer.Checkpoint(Attributes);
            writer.Append(record);
            writer.Append(new TestLogRecord { Redo = NtfsLogOperation.PrepareTransaction, TransactionId = Tx });
        }

        writer.Finish(restart);
        AssertReplayRefused(volume, "has no recorded outcome");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    public void AnAllocatedCheckpointEntryMustHaveARecognizedState(int state)
    {
        using var volume = Volume();
        var writer = new TestJournalWriter(volume);
        var lsn = writer.Append(Raw(FreeCluster(volume), Pattern));
        var restart = writer.Checkpoint(Attributes, transactions: [(Tx, (TransactionState)state, lsn, lsn)]);
        writer.Finish(restart);
        AssertReplayRefused(volume, "invalid checkpoint state");
    }

    [Theory]
    [InlineData(LfsRecord.TypeClientRecord, 0, 1)]
    [InlineData(LfsRecord.TypeClientRecord, 1, 0)]
    [InlineData(LfsRecord.TypeClientRestart, 0, 0)]
    public void UndoPointersMustNameRecordsOfTheActiveClient(uint recordType, int clientIndex, int clientSequence)
    {
        using var volume = Volume();
        var writer = new TestJournalWriter(volume);
        var record = Raw(FreeCluster(volume), Pattern);
        var lsn = writer.AppendRaw(record.ClientData(), recordType, Tx, 0, clientSequence: (ushort)clientSequence, clientIndex: (ushort)clientIndex);
        var restart = writer.Checkpoint(Attributes, transactions: [(Tx, TransactionState.Active, lsn, lsn)]);
        writer.Finish(restart);
        AssertReplayRefused(volume, "does not belong to the active NTFS client");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnUndoChainCannotGoBackToAnEarlierUseOfItsTransactionSlot(bool missing)
    {
        using var volume = Volume();
        var writer = new TestJournalWriter(volume);
        var target = FreeCluster(volume);
        var restart = writer.Checkpoint(Attributes);
        var prior = writer.Append(Raw(target, Pattern));
        writer.Append(Forget());
        var current = Raw(target, Pattern);
        current.UndoNextLsn = missing ? writer.LsnOf(writer.FirstPageOffset - 8, 1) : prior;
        writer.Append(current);
        writer.Finish(restart);
        AssertReplayRefused(volume, missing ? "not in the log" : "outside the undo history");
    }

    [Fact]
    public void MetadataUndoRequiresAKnownAndUnchangedClusterMapping()
    {
        using (var volume = Volume())
        {
            var missing = Raw(FreeCluster(volume), Pattern);
            missing.Lcns = [];
            using var dirty = WithRecordsOn(volume, missing);
            AssertReplayRefused(dirty, "metadata undo without a cluster mapping");
        }

        using (var volume = Volume())
        {
            var target = FreeCluster(volume);
            var writer = new TestJournalWriter(volume);
            var restart = writer.Checkpoint(Attributes);
            var change = writer.Append(Raw(target, Pattern));
            var hotFix = HotFix(target, [FreeCluster(volume, 1)]);
            hotFix.UndoNextLsn = change;
            writer.Append(hotFix);
            writer.Finish(restart);
            AssertReplayRefused(volume, "remapped cluster in its undo target");
        }
    }
}
