using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiscUtils.Ntfs;
using DiscUtils.Ntfs.Journal;
using static DiscUtils.Ntfs.Journal.JournalUtilities;

namespace LibraryTests.Ntfs.Journal;

public partial class NtfsJournalEdgeCaseTest
{
    // The fixture issues these events in order:
    // 1 mirror write, 2 mirror barrier, 3 primary MFT write, 4 raw metadata write, 5 metadata barrier,
    // 6 alternate restart write, 7 alternate barrier, 8 source restart write, 9 final barrier.
    [Theory]
    [InlineData(1, 0, false, NtfsJournalStatus.NeedsReplay)]
    [InlineData(1, 512, false, NtfsJournalStatus.NeedsReplay)]
    [InlineData(1, 4096, false, NtfsJournalStatus.NeedsReplay)]
    [InlineData(2, 0, false, NtfsJournalStatus.NeedsReplay)]
    [InlineData(2, 0, true, NtfsJournalStatus.NeedsReplay)]
    [InlineData(3, 0, false, NtfsJournalStatus.NeedsReplay)]
    [InlineData(3, 512, false, NtfsJournalStatus.Unsupported)]
    [InlineData(3, 4096, false, NtfsJournalStatus.NeedsReplay)]
    [InlineData(4, 0, false, NtfsJournalStatus.NeedsReplay)]
    [InlineData(4, 512, false, NtfsJournalStatus.NeedsReplay)]
    [InlineData(4, 4096, false, NtfsJournalStatus.NeedsReplay)]
    [InlineData(5, 0, false, NtfsJournalStatus.NeedsReplay)]
    [InlineData(5, 0, true, NtfsJournalStatus.NothingToReplay)]
    [InlineData(6, 0, false, NtfsJournalStatus.NothingToReplay)]
    [InlineData(6, 512, false, NtfsJournalStatus.NothingToReplay)]
    [InlineData(6, 4096, false, NtfsJournalStatus.NothingToReplay)]
    [InlineData(7, 0, false, NtfsJournalStatus.NothingToReplay)]
    [InlineData(7, 0, true, NtfsJournalStatus.NothingToReplay)]
    [InlineData(8, 0, false, NtfsJournalStatus.NothingToReplay)]
    [InlineData(8, 512, false, NtfsJournalStatus.Clean)]
    [InlineData(8, 4096, false, NtfsJournalStatus.Clean)]
    [InlineData(9, 0, false, NtfsJournalStatus.NothingToReplay)]
    [InlineData(9, 0, true, NtfsJournalStatus.Clean)]
    [InlineData(0, 0, false, NtfsJournalStatus.Clean)] // No fault. Reopen after the final successful barrier.
    public void RedoRetryUsesThePersistedImageAtEachWriteAndBarrier(int cut, int persistedPrefix, bool completedBarrier,
        NtfsJournalStatus expected)
    {
        using var dirty = Volume();
        var (writer, rawLcn, updateLsn, attribute) = WriteRedoInterruptionFixture(dirty);
        var mft = writer.Layout.MftCluster * 4096;
        var mirror = writer.Layout.MftMirrorCluster * 4096;
        using var fs = new NtfsFileSystem(dirty, NtfsJournalMode.Ignore);
        var logStart = fs.PathToClusters("$LogFile").First().Offset * 4096;
        var writes = new[]
        {
            (Step: 1, Barrier: 2, Position: mirror),
            (Step: 3, Barrier: 5, Position: mft),
            (Step: 4, Barrier: 5, Position: rawLcn * 4096),
            (Step: 6, Barrier: 7, Position: logStart + 4096),
            (Step: 8, Barrier: 9, Position: logStart),
        };

        var initial = dirty.ToArray();
        using var device = new CrashImageStream(initial, cut, persistedPrefix, completedBarrier);
        if (cut == 0)
        {
            Assert.True(NtfsJournal.Replay(device).Replayed);
        }
        else
        {
            Assert.Same(device.Failure, Assert.Throws<IOException>(() => NtfsJournal.Replay(device)));
        }

        // Each write is for a different cluster. Check the exact stable image, including untouched bytes.
        var events = new (long Position, int Count)[9]; // Default entry denotes a flush.
        using var completed = JournalScenario.Copy(initial);
        Assert.True(NtfsJournal.Replay(completed).Replayed);
        var expectedImage = (byte[])initial.Clone();
        foreach (var (step, barrier, position) in writes)
        {
            events[step - 1] = (position, 4096);
            var count = cut == 0 || barrier < cut || (barrier == cut && completedBarrier)
                ? 4096 : step == cut ? persistedPrefix : 0;
            Array.Copy(completed.GetBuffer(), position, expectedImage, position, count);
        }

        Assert.Equal(events.Take(cut == 0 ? events.Length : cut), device.Events);
        var crashImage = device.CrashImage;
        Assert.True(expectedImage.AsSpan().SequenceEqual(crashImage), $"Unexpected stable bytes at event {cut}");
        using var retry = JournalScenario.Copy(crashImage);
        var analysis = NtfsJournal.Analyze(retry);
        Assert.True(analysis.Status == expected, $"cut {cut}: {analysis}");

        if (expected == NtfsJournalStatus.Unsupported)
        {
            AssertReplayRefused(retry, "damaged");
            return;
        }

        if (cut == 3 && persistedPrefix == 4096)
        {
            // The primary page LSN advanced, while a different page never made it to storage.
            Assert.Equal(1, analysis.RecordsToRedo);
        }

        var storage = LogFileStorage.Open(retry, writer.Layout);
        if (cut == 6 && persistedPrefix == 512)
        {
            // The old alternate's USN was the value the source would normally increment to.
            Assert.Null(LfsRestartPage.TryRead(storage, TestJournalWriter.PageSize));
            Assert.True(LfsRestartPage.TryRead(storage, 0).RequiresRecovery);
        }

        var result = NtfsJournal.Replay(retry);
        Assert.Equal(expected != NtfsJournalStatus.Clean, result.Replayed);
        var mftBytes = Read(retry, mft, 1024, logical: true);
        Assert.Equal(updateLsn, U64(mftBytes, 8));
        Assert.Equal(Pattern.AsSpan(0, 8).ToArray(), mftBytes.AsSpan(attribute + 0x18, 8).ToArray());
        Assert.Equal(Read(retry, mft, 4096), Read(retry, mirror, 4096));
        Assert.Equal(Enumerable.Repeat(Pattern[0], 1024).ToArray(), Read(retry, rawLcn * 4096, 1024));
        var bytes = retry.ToArray();
        Assert.Equal(NtfsJournalStatus.Clean, NtfsJournal.Analyze(retry).Status);
        Assert.False(NtfsJournal.Replay(retry).Replayed);
        Assert.Equal(bytes, retry.ToArray());
    }

    [Theory]
    [InlineData(7)]
    [InlineData(9)]
    public void RetryOnTheSameStreamAfterAFailedRestartBarrier(int cut)
    {
        using var dirty = Volume();
        var (writer, _, _, _) = WriteRedoInterruptionFixture(dirty);
        using var device = new CrashImageStream(dirty.ToArray(), cut);
        Assert.Same(device.Failure, Assert.Throws<IOException>(() => NtfsJournal.Replay(device)));

        using var stable = JournalScenario.Copy(device.CrashImage);
        Assert.Equal(NtfsJournalStatus.NothingToReplay, NtfsJournal.Analyze(stable).Status);
        var result = NtfsJournal.Replay(device);
        if (cut == 7)
        {
            // The visible alternate may be buffered. The unconditional barrier runs before replacing the source.
            Assert.True(result.Replayed);
            using var fs = new NtfsFileSystem(dirty, NtfsJournalMode.Ignore);
            var logStart = fs.PathToClusters("$LogFile").First().Offset * 4096;
            Assert.Equal(new[] { (0L, 0), (logStart, 4096), (0L, 0) }, device.Events.Skip(cut));
            using var reopened = JournalScenario.Copy(device.CrashImage);
            var storage = LogFileStorage.Open(reopened, writer.Layout);
            Assert.True(LfsRestartPage.TryRead(storage, 0).IsClean);
            Assert.True(LfsRestartPage.TryRead(storage, TestJournalWriter.PageSize).IsClean);
        }
        else
        {
            // Both pages are visible, so replay is a no-op. The stable alternate already protects the journal.
            Assert.Equal(NtfsJournalStatus.Clean, result.Status);
            Assert.False(result.Replayed);
            Assert.Equal(cut, device.Events.Count);
            Assert.True(NtfsJournal.Replay(stable).Replayed);
        }
    }

    [Fact]
    public void AnotherInterruptionCanLeaveOneValidCleanRestartPage()
    {
        using var dirty = Volume();
        var (writer, _, _, _) = WriteRedoInterruptionFixture(dirty, sourceIsSecond: true);
        Assert.Equal(TestJournalWriter.PageSize, LfsLog.Open(writer.Storage, out _).Restart.Offset);

        // The alternate reset is durable, while the source on the second page is still dirty.
        using var first = new CrashImageStream(dirty.ToArray(), cut: 7, completedBarrier: true);
        Assert.Same(first.Failure, Assert.Throws<IOException>(() => NtfsJournal.Replay(first)));
        using var second = new CrashImageStream(first.CrashImage, cut: 2, persistedPrefix: 512);
        var analysis = NtfsJournal.Analyze(second);
        Assert.Equal(NtfsJournalStatus.NothingToReplay, analysis.Status);
        Assert.Contains("one restart page", analysis.Detail);
        Assert.Throws<IOException>(() => new NtfsFileSystem(second));
        Assert.Same(second.Failure, Assert.Throws<IOException>(() => NtfsJournal.Replay(second)));

        using var retry = JournalScenario.Copy(second.CrashImage);
        var storage = LogFileStorage.Open(retry, writer.Layout);
        Assert.True(LfsRestartPage.TryRead(storage, 0).IsClean);
        Assert.Null(LfsRestartPage.TryRead(storage, TestJournalWriter.PageSize));
        Assert.Equal(NtfsJournalStatus.Clean, NtfsJournal.Replay(retry).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AFlushThatDropsMetadataCanPublishAnUnsafeReset(bool publishBoth)
    {
        using var dirty = Volume();
        var (_, rawLcn, _, _) = WriteRedoInterruptionFixture(dirty);
        var rawPosition = rawLcn * 4096;
        using var device = new CrashImageStream(dirty.ToArray(), cut: publishBoth ? 0 : 7, completedBarrier: true,
            persistWrite: position => position != rawPosition);
        if (publishBoth)
        {
            Assert.True(NtfsJournal.Replay(device).Replayed);
        }
        else
        {
            Assert.Same(device.Failure, Assert.Throws<IOException>(() => NtfsJournal.Replay(device)));
        }

        using var retry = JournalScenario.Copy(device.CrashImage);
        Assert.Equal(Read(dirty, rawPosition, 4096), Read(retry, rawPosition, 4096));
        if (publishBoth)
        {
            // After publishing both resets, no dirty restart page remains to identify the lost metadata.
            Assert.Equal(NtfsJournalStatus.Clean, NtfsJournal.Analyze(retry).Status);
            var before = retry.ToArray();
            Assert.False(NtfsJournal.Replay(retry).Replayed);
            Assert.Equal(before, retry.ToArray());
        }
        else
        {
            AssertReplayRefused(retry, "partial $LogFile reset disagrees");
        }
    }

    [Theory]
    [InlineData(true, "disagree about recovery at the same LSN")]
    [InlineData(false, "disagree about the checkpoint at the same LSN")]
    public void RestartPagesWithConflictingStateAtTheSameLsnAreRefused(bool isClean, string reason)
    {
        using var volume = Volume();
        var (writer, _, _, _) = WriteRedoInterruptionFixture(volume);
        var conflict = writer.RestartPage(0, 0, clean: isClean, writer.LastLsn);
        writer.Storage.Write(0, conflict, 0, conflict.Length);
        AssertReplayRefused(volume, reason);
    }

    private static (TestJournalWriter Writer, long RawLcn, ulong UpdateLsn, int Attribute) WriteRedoInterruptionFixture(
        MemoryStream volume, bool sourceIsSecond = false)
    {
        var writer = new TestJournalWriter(volume, sequence: 1000);
        var record = Read(volume, writer.Layout.MftCluster * 4096, 1024, logical: true);
        var attribute = U16(record, 0x14);
        var target = FreeCluster(volume);
        var restart = writer.Checkpoint(Attributes);
        var lsn = writer.Append(FirstMftValueUpdate(writer, record, Pattern.AsSpan(0, 8).ToArray(), new byte[8]));
        writer.Append(Raw(target, Enumerable.Repeat(Pattern[0], 1024).ToArray()));
        writer.Append(Forget());
        var currentLsn = sourceIsSecond ? writer.LastLsn - 2 : writer.LastLsn;
        writer.Finish(restart, currentLsn: currentLsn);

        var other = writer.RestartPage(restart, 0, clean: false, sourceIsSecond ? currentLsn - 1 : currentLsn);
        Assert.True(RemoveFixups(other, 0, other.Length));
        ApplyFixups(other, 0, other.Length);
        writer.Storage.Write(sourceIsSecond ? 0 : TestJournalWriter.PageSize, other, 0, other.Length);
        return (writer, target, lsn, attribute);
    }

    [Fact]
    public void ATornFileImageWithAnAdvancedLsnMustStillBeRedone()
    {
        using var dirty = Volume();
        var writer = new TestJournalWriter(dirty, sequence: 1000);
        var mft = writer.Layout.MftCluster * 4096;
        var original = Read(dirty, mft, 1024);
        var image = (byte[])original.Clone();
        Assert.True(RemoveFixups(image, 0, image.Length));
        var usa = U16(image, 4);
        var oldUsn = U16(image, usa);
        // Incrementing the logged image's USA alone would reuse the destination's existing value.
        Put16(image, usa, (ushort)(oldUsn - 1));
        var attr = U16(image, 0x14);
        Array.Copy(Pattern, 0, image, attr + 0x18, 8);
        var restart = writer.Checkpoint(Attributes);
        var lsn = writer.Append(Op(NtfsLogOperation.InitializeFileRecordSegment, image, Mft, 0, [writer.Layout.MftCluster]));
        writer.Append(Forget());
        writer.Finish(restart);

        using var device = new CrashImageStream(dirty.ToArray(), cut: 3, persistedPrefix: 512);
        Assert.Same(device.Failure, Assert.Throws<IOException>(() => NtfsJournal.Replay(device)));
        using var retry = JournalScenario.Copy(device.CrashImage);
        var torn = Read(retry, mft, original.Length);
        Assert.Equal(lsn, U64(torn, 8));
        Assert.False(RemoveFixups(torn, 0, torn.Length));

        Assert.Equal(1, NtfsJournal.Replay(retry).RecordsToRedo);
        var result = Read(retry, mft, original.Length, logical: true);
        Assert.Equal(Pattern.AsSpan(0, 8).ToArray(), result.AsSpan(attr + 0x18, 8).ToArray());
    }

    [Fact]
    public void ATornFileRecordNeedsAReplacementOfAllUsedBytes()
    {
        using var volume = Volume();
        var layout = JournalVolumeLayout.Read(volume);
        var position = layout.MftCluster * layout.BytesPerCluster;
        var record = Read(volume, position, layout.MftRecordSize, logical: true);
        volume.Position = position + 0x3FE;
        var previous = volume.ReadByte();
        volume.Position--;
        volume.WriteByte((byte)(previous ^ 0xFF));
        using var dirty = WithRecordsOn(volume, Op(NtfsLogOperation.InitializeFileRecordSegment, record.AsSpan(0, 0x60).ToArray(),
            Mft, 0, [layout.MftCluster]), Forget());
        AssertReplayRefused(dirty, "complete replacement for the torn file record");
    }

    [Fact]
    public void ALoggedIndexImageCannotOverwriteAnIndexWithALaterLsn()
    {
        using var volume = Volume();
        var writer = new TestJournalWriter(volume);
        var lcn = FirstIndexBuffer(volume);
        var position = lcn * 4096;
        var index = Read(volume, position, 4096, logical: true);
        var restart = writer.Checkpoint(Attributes);
        writer.Append(Op(NtfsLogOperation.UpdateNonresidentValue, index, Index, 0, [lcn]));
        writer.Append(Forget());
        writer.Finish(restart);

        index[0x100] ^= 0x45;
        Put64(index, 8, writer.LastLsn);
        ApplyFixups(index, 0, index.Length);
        volume.Position = position;
        volume.Write(index, 0, index.Length);

        Assert.Equal(NtfsJournalStatus.NothingToReplay, NtfsJournal.Replay(volume).Status);
        Assert.Equal(index, Read(volume, position, index.Length));
    }

    [Theory]
    [InlineData(false, "partially replaces a torn index buffer")]
    [InlineData(true, "invalid replacement for the torn index buffer")]
    public void ATornIndexBufferNeedsAValidCompleteReplacement(bool fullLength, string reason)
    {
        using var volume = Volume();
        var lcn = FirstIndexBuffer(volume);
        volume.Position = lcn * 4096 + 0x3FE;
        var previous = volume.ReadByte();
        volume.Position--;
        volume.WriteByte((byte)(previous ^ 0xFF));
        using var dirty = WithRecordsOn(volume, Op(NtfsLogOperation.UpdateNonresidentValue,
            fullLength ? new byte[4096] : Pattern, Index, 0, [lcn], recordOffset: fullLength ? 0 : 0x100), Forget());
        AssertReplayRefused(dirty, reason);
    }

    // MemoryStream supplies the live read/write view only. This fixture explicitly models a second image
    // that receives pending writes at successful barriers and can retain a prefix of the failing write.
    private sealed class CrashImageStream : MemoryStream
    {
        private readonly byte[] _stable;
        private readonly List<(int Position, int Count)> _pending = [];
        private readonly int _cut;
        private readonly int _persistedPrefix;
        private readonly bool _completedBarrier;
        private readonly Func<int, bool>? _persistWrite;

        public CrashImageStream(byte[] image, int cut, int persistedPrefix = 0, bool completedBarrier = false,
            Func<int, bool>? persistWrite = null)
            : base((byte[])image.Clone(), 0, image.Length, writable: true, publiclyVisible: true)
        {
            _stable = (byte[])image.Clone();
            _cut = cut;
            _persistedPrefix = persistedPrefix;
            _completedBarrier = completedBarrier;
            _persistWrite = persistWrite;
        }

        public IOException Failure { get; } = new("Injected interruption");

        public List<(long Position, int Count)> Events { get; } = [];

        public byte[] CrashImage => (byte[])_stable.Clone();

        public override void Write(byte[] buffer, int offset, int count)
        {
            var position = checked((int)Position);
            Events.Add((position, count));
            if (Events.Count == _cut)
            {
                Array.Copy(buffer, offset, _stable, position, Math.Min(count, _persistedPrefix));
                throw Failure;
            }

            base.Write(buffer, offset, count);
            _pending.Add((position, count));
        }

        public override void Flush()
        {
            Events.Add(default);
            var cut = Events.Count == _cut;
            if (!cut || _completedBarrier)
            {
                var live = GetBuffer();
                foreach (var (position, count) in _pending)
                {
                    if (_persistWrite == null || _persistWrite(position))
                    {
                        Array.Copy(live, position, _stable, position, count);
                    }
                }

                _pending.Clear();
            }

            if (cut)
            {
                throw Failure;
            }
        }
    }
}
