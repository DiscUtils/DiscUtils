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
    [InlineData(2, 0, false, NtfsJournalStatus.NeedsReplay)]
    [InlineData(2, 0, true, NtfsJournalStatus.NeedsReplay)]
    [InlineData(3, 0, false, NtfsJournalStatus.NeedsReplay)]
    [InlineData(3, 512, false, NtfsJournalStatus.Unsupported)]
    [InlineData(3, 4096, false, NtfsJournalStatus.NeedsReplay)]
    [InlineData(4, 512, false, NtfsJournalStatus.NeedsReplay)]
    [InlineData(5, 0, false, NtfsJournalStatus.NeedsReplay)]
    [InlineData(5, 0, true, NtfsJournalStatus.NothingToReplay)]
    [InlineData(6, 512, false, NtfsJournalStatus.NothingToReplay)]
    [InlineData(6, 4096, false, NtfsJournalStatus.NothingToReplay)]
    [InlineData(7, 0, false, NtfsJournalStatus.NothingToReplay)]
    [InlineData(7, 0, true, NtfsJournalStatus.NothingToReplay)]
    [InlineData(8, 0, false, NtfsJournalStatus.NothingToReplay)]
    [InlineData(8, 512, false, NtfsJournalStatus.Clean)]
    [InlineData(9, 0, false, NtfsJournalStatus.NothingToReplay)]
    [InlineData(9, 0, true, NtfsJournalStatus.Clean)]
    public void RedoRetryUsesThePersistedImageAtEachWriteAndBarrier(int cut, int persistedPrefix, bool completedBarrier,
        NtfsJournalStatus expected)
    {
        using var dirty = Volume();
        var (writer, rawLcn, updateLsn, attribute) = WriteRedoInterruptionFixture(dirty);
        var mft = writer.Layout.MftCluster * 4096;
        var mirror = writer.Layout.MftMirrorCluster * 4096;

        using var device = new CrashImageStream(dirty.ToArray(), cut, persistedPrefix, completedBarrier);
        Assert.Same(device.Failure, Assert.Throws<IOException>(() => NtfsJournal.Replay(device)));
        using var retry = JournalScenario.Copy(device.CrashImage);
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

    [Fact]
    public void APartialResetWithMissingMetadataIsRefused()
    {
        using var dirty = Volume();
        var (_, rawLcn, _, _) = WriteRedoInterruptionFixture(dirty);
        using var device = new CrashImageStream(dirty.ToArray(), cut: 7, completedBarrier: true);
        Assert.Same(device.Failure, Assert.Throws<IOException>(() => NtfsJournal.Replay(device)));
        using var retry = JournalScenario.Copy(device.CrashImage);
        // Inject an image that an ordered metadata barrier could not have produced.
        retry.Position = rawLcn * 4096;
        retry.WriteByte(0);
        AssertReplayRefused(retry, "partial $LogFile reset disagrees");
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
        private int _events;

        public CrashImageStream(byte[] image, int cut, int persistedPrefix = 0, bool completedBarrier = false)
            : base((byte[])image.Clone(), 0, image.Length, writable: true, publiclyVisible: true)
        {
            _stable = (byte[])image.Clone();
            _cut = cut;
            _persistedPrefix = persistedPrefix;
            _completedBarrier = completedBarrier;
        }

        public IOException Failure { get; } = new("Injected interruption");

        public byte[] CrashImage => (byte[])_stable.Clone();

        public override void Write(byte[] buffer, int offset, int count)
        {
            var position = checked((int)Position);
            if (++_events == _cut)
            {
                Array.Copy(buffer, offset, _stable, position, Math.Min(count, _persistedPrefix));
                throw Failure;
            }

            base.Write(buffer, offset, count);
            _pending.Add((position, count));
        }

        public override void Flush()
        {
            var cut = ++_events == _cut;
            if (!cut || _completedBarrier)
            {
                var live = GetBuffer();
                foreach (var (position, count) in _pending)
                {
                    Array.Copy(live, position, _stable, position, count);
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
