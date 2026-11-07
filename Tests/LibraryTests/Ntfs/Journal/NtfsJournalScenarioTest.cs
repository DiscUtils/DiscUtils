using System;
using System.IO;
using System.Linq;
using DiscUtils.Ntfs;
using DiscUtils.Ntfs.Journal;
using DiscUtils.Streams;

namespace LibraryTests.Ntfs.Journal;

/// <summary>
/// Replays journals written by <see cref="TestJournalWriter"/> for changes DiscUtils made itself: the replay must
/// turn the "before" volume into the "after" one (redo) and the other way round (undo).
/// </summary>
public class NtfsJournalScenarioTest
{
    private static void AssertStatus(NtfsJournalStatus expected, NtfsJournalInfo info) =>
        Assert.True(info.Status == expected, $"expected {expected}, got {info}");

    private static void CreateFiles(NtfsFileSystem ntfs)
    {
        ntfs.CreateDirectory(@"dir\sub");
        for (var i = 0; i < 40; i++)
        {
            using var s = ntfs.OpenFile($@"dir\file{i:D3}.txt", FileMode.Create);
            var content = Enumerable.Repeat((byte)('a' + (i % 26)), 100 + (i * 97)).ToArray();
            s.Write(content, 0, content.Length);
        }

        using (var big = ntfs.OpenFile(@"dir\sub\big.bin", FileMode.Create))
        {
            big.Write(new byte[20000], 0, 20000);
        }

        ntfs.SetAttributes(@"dir\file001.txt", FileAttributes.Hidden);
    }

    private static void ChangeFiles(NtfsFileSystem ntfs)
    {
        ntfs.DeleteFile(@"dir\file002.txt");
        ntfs.MoveFile(@"dir\file003.txt", @"dir\sub\moved.txt");
        using (var s = ntfs.OpenFile(@"dir\file004.txt", FileMode.Append))
        {
            s.Write(new byte[5000], 0, 5000);
        }

        ntfs.CreateDirectory(@"dir\new");
        ntfs.DeleteFile(@"dir\sub\big.bin");
    }

    [Fact]
    public void Redo_ACommittedTransaction_BringsTheVolumeUpToDate()
    {
        var scenario = JournalScenario.Create(CreateFiles);
        Assert.NotEmpty(scenario.Records);
        using var volume = JournalScenario.Copy(scenario.Before);
        scenario.WriteJournal(volume, committed: true);

        var analysis = NtfsJournal.Analyze(volume);
        AssertStatus(NtfsJournalStatus.NeedsReplay, analysis);
        Assert.True(scenario.Records.Count == analysis.RecordsToRedo, $"{scenario.Records.Count} records, {analysis}");
        Assert.Equal(0, analysis.TransactionsToUndo);

        var replay = NtfsJournal.Replay(volume);
        Assert.True(replay.Replayed);
        scenario.AssertVolumeEquals(scenario.After, volume);
        AssertStatus(NtfsJournalStatus.Clean, NtfsJournal.Analyze(volume));

        using var ntfs = new NtfsFileSystem(volume);
        Assert.Equal(39, ntfs.GetFiles(@"dir").Count());
        Assert.Equal(20000, ntfs.GetFileLength(@"dir\sub\big.bin"));
    }

    [Fact]
    public void Undo_AnUnfinishedTransaction_RollsTheVolumeBack()
    {
        var scenario = JournalScenario.Create(ChangeFiles, setup: CreateFiles);
        using var volume = JournalScenario.Copy(scenario.After);
        scenario.WriteJournal(volume, committed: false);

        var analysis = NtfsJournal.Analyze(volume);
        AssertStatus(NtfsJournalStatus.NeedsReplay, analysis);
        Assert.Equal(1, analysis.TransactionsToUndo);
        Assert.Equal(scenario.Records.Count, analysis.RecordsToUndo);
        Assert.False(analysis.CanReplayPersistently);

        var before = volume.ToArray();
        Assert.Contains("metadata undo", Assert.Throws<IOException>(() => NtfsJournal.Replay(volume)).Message);
        Assert.Equal(before, volume.ToArray());

        NtfsJournal.ReplayInMemory(volume, out var overlay);
        using (overlay)
        {
            scenario.AssertVolumeEquals(scenario.Before, overlay);
        }

        using var ntfs = new NtfsFileSystem(new MemoryStream(before, writable: false));
        Assert.True(ntfs.JournalInfo.ReplayedInMemory);
        Assert.True(ntfs.FileExists(@"dir\file002.txt"));
        Assert.True(ntfs.FileExists(@"dir\sub\big.bin"));
        Assert.False(ntfs.DirectoryExists(@"dir\new"));
    }

    [Fact]
    public void Redo_RecordsThatWrapAroundTheEndOfTheLog()
    {
        var scenario = JournalScenario.Create(CreateFiles);
        using var volume = JournalScenario.Copy(scenario.Before);
        var size = new TestJournalWriter(volume).FileSize;
        var writer = scenario.WriteJournal(volume, committed: true, startOffset: size - (3 * TestJournalWriter.PageSize));
        Assert.True(writer.SequenceNumberBits > 0);
        Assert.True(scenario.Records.Count * 1024 > 3 * TestJournalWriter.PageSize, "the journal must be longer than the space left before the end");

        NtfsJournal.Replay(volume);
        scenario.AssertVolumeEquals(scenario.After, volume);
    }

    [Fact]
    public void Redo_LogFileServiceVersion1()
    {
        var scenario = JournalScenario.Create(CreateFiles);
        using var volume = JournalScenario.Copy(scenario.Before);
        scenario.WriteJournal(volume, committed: true, majorVersion: 1);

        Assert.Equal("1.1", NtfsJournal.Analyze(volume).LogVersion);
        NtfsJournal.Replay(volume);
        scenario.AssertVolumeEquals(scenario.After, volume);
    }

    [Fact]
    public void Replay_IsIdempotent_AndAnInterruptedReplayReplaysToTheSameResult()
    {
        var scenario = JournalScenario.Create(ChangeFiles, setup: CreateFiles);
        using var crashed = JournalScenario.Copy(scenario.Before);
        scenario.WriteJournal(crashed, committed: true);
        var crashedImage = crashed.ToArray();

        // An interruption after the pages were written but before the journal was reset: the pages are replayed,
        // the journal is still the crash journal.
        using var interrupted = JournalScenario.Copy(crashedImage);
        NtfsJournal.Replay(interrupted);
        var replayed = interrupted.ToArray();
        foreach (var lcn in scenario.LogClusters)
        {
            Array.Copy(crashedImage, lcn * scenario.ClusterSize, replayed, lcn * scenario.ClusterSize, scenario.ClusterSize);
        }

        using var again = JournalScenario.Copy(replayed);
        AssertStatus(NtfsJournalStatus.NothingToReplay, NtfsJournal.Analyze(again));
        NtfsJournal.Replay(again);
        scenario.AssertVolumeEquals(scenario.After, again);

        // And a replay of a replayed volume does nothing.
        var info = NtfsJournal.Replay(again);
        AssertStatus(NtfsJournalStatus.Clean, info);
        Assert.False(info.Replayed);
    }

    [Fact]
    public void ReadOnlyOpen_ShowsTheReplayedVolume_WithoutWritingIt()
    {
        var scenario = JournalScenario.Create(CreateFiles);
        using var volume = JournalScenario.Copy(scenario.Before);
        scenario.WriteJournal(volume, committed: true);
        var image = volume.ToArray();

        using var readOnly = new MemoryStream(image, writable: false);
        using (var ntfs = new NtfsFileSystem(readOnly))
        {
            AssertStatus(NtfsJournalStatus.NeedsReplay, ntfs.JournalInfo);
            Assert.False(ntfs.JournalInfo.Replayed);
            Assert.True(ntfs.JournalInfo.ReplayedInMemory);
            Assert.True(ntfs.FileExists(@"dir\file039.txt"));
            Assert.Equal(100 + (39 * 97), ntfs.GetFileLength(@"dir\file039.txt"));
        }

        Assert.True(image.AsSpan().SequenceEqual(volume.ToArray()));
    }

    [Fact]
    public void ExplicitWritableReplay_ReplaysAndResetsTheJournal()
    {
        var scenario = JournalScenario.Create(CreateFiles);
        using var volume = JournalScenario.Copy(scenario.Before);
        scenario.WriteJournal(volume, committed: true);

        using (var ntfs = new NtfsFileSystem(volume, NtfsJournalMode.Replay))
        {
            Assert.True(ntfs.JournalInfo.Replayed);
            Assert.True(ntfs.FileExists(@"dir\sub\big.bin"));
        }

        scenario.AssertVolumeEquals(scenario.After, volume);
        AssertStatus(NtfsJournalStatus.Clean, NtfsJournal.Analyze(volume));
    }

    [Fact]
    public void RefuseMode_ThrowsAndWritesNothing()
    {
        var scenario = JournalScenario.Create(CreateFiles);
        using var volume = JournalScenario.Copy(scenario.Before);
        scenario.WriteJournal(volume, committed: true);
        var image = volume.ToArray();

        Assert.Throws<IOException>(() => new NtfsFileSystem(volume, NtfsJournalMode.Refuse));
        Assert.True(image.AsSpan().SequenceEqual(volume.ToArray()));
    }

    [Fact]
    public void IgnoreMode_OpensTheVolumeAsItIs()
    {
        var scenario = JournalScenario.Create(CreateFiles);
        using var volume = JournalScenario.Copy(scenario.Before);
        scenario.WriteJournal(volume, committed: true);
        var image = volume.ToArray();

        using (var ntfs = new NtfsFileSystem(volume, NtfsJournalMode.Ignore))
        {
            Assert.Null(ntfs.JournalInfo);
            Assert.False(ntfs.DirectoryExists(@"dir"));
        }

        Assert.True(image.AsSpan().SequenceEqual(volume.ToArray()));
    }

    [Fact]
    public void ACommitThatNeverReachedTheDisk_IsRolledBack()
    {
        // The pages of the transaction are on disk, but the page holding its last records (and the commit) is torn.
        var scenario = JournalScenario.Create(ChangeFiles, setup: CreateFiles);
        using var volume = JournalScenario.Copy(scenario.After);
        var writer = scenario.WriteJournal(volume, committed: true);
        var lastPage = writer.LsnToOffsetForTest(writer.LastLsn) & ~(long)(TestJournalWriter.PageSize - 1);
        var page = new byte[TestJournalWriter.PageSize];
        writer.Storage.Read(lastPage, page, 0, page.Length);
        page[0x1FE] ^= 0xFF;
        writer.Storage.Write(lastPage, page, 0, page.Length);

        var analysis = NtfsJournal.Analyze(volume);
        Assert.Equal(1, analysis.TransactionsToUndo);
        Assert.False(analysis.CanReplayPersistently);

        using var readOnly = new MemoryStream(volume.ToArray(), writable: false);
        using var ntfs = new NtfsFileSystem(readOnly);
        Assert.True(ntfs.FileExists(@"dir\file002.txt"));
        Assert.False(ntfs.DirectoryExists(@"dir\new"));
    }

    [Fact]
    public void AnOperationDiscUtilsDoesNotKnow_IsRefused_AndNothingIsWritten()
    {
        var scenario = JournalScenario.Create(CreateFiles);
        using var volume = JournalScenario.Copy(scenario.Before);
        scenario.WriteJournal(volume, committed: true, adjust: r => r.TargetAttribute == JournalScenario.MftData
            ? new TestLogRecord
            {
                Redo = NtfsLogOperation.UpdateRelativeDataInIndex,
                Undo = NtfsLogOperation.Noop,
                TransactionId = r.TransactionId,
                RedoData = new byte[16],
                TargetAttribute = r.TargetAttribute,
                TargetVcn = r.TargetVcn,
                ClusterBlockOffset = r.ClusterBlockOffset,
                Lcns = r.Lcns,
            }
            : r);
        var image = volume.ToArray();

        var analysis = NtfsJournal.Analyze(volume);
        AssertStatus(NtfsJournalStatus.Unsupported, analysis);
        Assert.Contains("UpdateRelativeDataInIndex", analysis.Detail);
        Assert.Throws<IOException>(() => NtfsJournal.Replay(volume));
        Assert.Throws<IOException>(() => new NtfsFileSystem(volume));
        Assert.True(image.AsSpan().SequenceEqual(volume.ToArray()));
    }

    [Fact]
    public void Clean_EmptyAndChkdskJournals_NeedNothing()
    {
        var scenario = JournalScenario.Create(CreateFiles);

        // As formatted: $LogFile is all 0xFF.
        using (var volume = JournalScenario.Copy(scenario.After))
        {
            var info = NtfsJournal.Analyze(volume);
            AssertStatus(NtfsJournalStatus.Clean, info);
            Assert.Contains("empty", info.Detail);
        }

        // Shut down cleanly.
        using (var volume = JournalScenario.Copy(scenario.After))
        {
            var writer = new TestJournalWriter(volume);
            var restart = writer.Checkpoint([]);
            writer.Finish(restart, clean: true);
            AssertStatus(NtfsJournalStatus.Clean, NtfsJournal.Analyze(volume));
        }

        // chkdsk obsoleted the journal.
        using (var volume = JournalScenario.Copy(scenario.Before))
        {
            scenario.WriteJournal(volume, committed: true);
            var storage = new TestJournalWriter(volume).Storage;
            var page = new byte[TestJournalWriter.PageSize];
            storage.Read(0, page, 0, page.Length);
            EndianUtilities.WriteBytesLittleEndian(JournalUtilities.SignatureChkd, page, 0);
            storage.Write(0, page, 0, page.Length);
            var info = NtfsJournal.Analyze(volume);
            AssertStatus(NtfsJournalStatus.Clean, info);
            Assert.Contains("chkdsk", info.Detail);
        }
    }

    [Fact]
    public void NotShutDownCleanly_ButNothingPending_IsMarkedClean()
    {
        var scenario = JournalScenario.Create(CreateFiles);
        using var volume = JournalScenario.Copy(scenario.After);
        var writer = new TestJournalWriter(volume);
        var restart = writer.Checkpoint([(JournalScenario.MftData, 0x80, 0x0001000000000000UL, 0, "")]);
        writer.Append(new TestLogRecord { Redo = NtfsLogOperation.ForgetTransaction, Undo = NtfsLogOperation.CompensationLogRecord, TransactionId = 0x40 });
        writer.Finish(restart);

        var info = NtfsJournal.Analyze(volume);
        AssertStatus(NtfsJournalStatus.NothingToReplay, info);

        var image = volume.ToArray();
        info = NtfsJournal.Replay(volume);
        Assert.True(info.Replayed);
        scenario.AssertVolumeEquals(image, volume);
        AssertStatus(NtfsJournalStatus.Clean, NtfsJournal.Analyze(volume));
    }
}
