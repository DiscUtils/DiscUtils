using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiscUtils.Ntfs;
using DiscUtils.Ntfs.Journal;
using File = System.IO.File;
using static DiscUtils.Ntfs.Journal.JournalUtilities;

namespace LibraryTests.Ntfs.Journal;

public partial class NtfsJournalEdgeCaseTest
{
    private static void AssertReplayRefused(MemoryStream volume, string reason)
    {
        var before = volume.ToArray();
        AssertRefused(volume, reason);
        Assert.Contains(reason, Assert.Throws<IOException>(() => NtfsJournal.Replay(volume)).Message);
        Assert.True(before.AsSpan().SequenceEqual(volume.ToArray()));
    }

    [Theory]
    [InlineData("short header", "non-resident data header")]
    [InlineData("attribute length overflow", "damaged attribute list")]
    [InlineData("runs in header", "mapping pairs offset")]
    [InlineData("runs past attribute", "mapping pairs offset")]
    [InlineData("starting VCN", "nonzero starting VCN")]
    [InlineData("last VCN", "declared allocation")]
    [InlineData("attribute list after data", "$ATTRIBUTE_LIST")]
    [InlineData("duplicate data", "split unnamed data")]
    [InlineData("physical run outside volume", "outside the volume")]
    [InlineData("physical run overlaps MFT", "overlaps $MFT")]
    public void InvalidLogFileLayouts_AreRefusedBeforeReadingOrResettingTheLog(string damage, string reason)
    {
        using var volume = Volume();
        var layout = JournalVolumeLayout.Read(volume);
        var position = layout.MftCluster * layout.BytesPerCluster + 2L * layout.MftRecordSize;
        var record = Read(volume, position, layout.MftRecordSize, logical: true);
        var data = (int)U16(record, 0x14);
        while (U32(record, data) != 0x80)
        {
            data += (int)U32(record, data + 4);
        }

        var attributeLength = (int)U32(record, data + 4);
        var end = data + attributeLength;
        Assert.Equal(0xFFFFFFFFu, U32(record, end));
        switch (damage)
        {
            case "short header":
                Put32(record, data + 4, 0x38);
                Put32(record, data + 0x38, 0xFFFFFFFF);
                break;
            case "attribute length overflow":
                Put32(record, data + 4, int.MaxValue - 7);
                break;
            case "runs in header":
                Put16(record, data + 0x20, 0x20);
                break;
            case "runs past attribute":
                Put16(record, data + 0x20, ushort.MaxValue);
                break;
            case "starting VCN":
                Put64(record, data + 0x10, 1);
                break;
            case "last VCN":
                Put64(record, data + 0x18, U64(record, data + 0x18) + 1);
                break;
            case "attribute list after data":
                Put32(record, end, 0x20);
                Put32(record, end + 4, 0x18);
                Put32(record, end + 0x18, 0xFFFFFFFF);
                Put32(record, 0x18, (uint)(end + 0x20));
                break;
            case "duplicate data":
                Array.Copy(record, data, record, end, attributeLength);
                Put32(record, end + attributeLength, 0xFFFFFFFF);
                Put32(record, 0x18, (uint)(end + attributeLength + 8));
                break;
            default:
                var run = data + U16(record, data + 0x20);
                record[run] = 0x22;
                Put16(record, run + 1, (ushort)(U64(record, data + 0x18) + 1));
                Put16(record, run + 3, (ushort)(damage == "physical run overlaps MFT" ? layout.MftCluster : layout.TotalClusters - 1));
                record[run + 5] = 0;
                // Give the backing stream room past the declared NTFS volume.
                volume.SetLength(volume.Length + (1024 * 1024) * 4);
                break;
        }

        ApplyFixups(record, 0, record.Length);
        volume.Position = position;
        volume.Write(record, 0, record.Length);
        AssertReplayRefused(volume, reason);
    }

    [Fact]
    public void ALogFileRunBeyondTheBackingStream_IsRefusedEvenWithAnIntactRestartArea()
    {
        using var volume = WithRecords(Forget());
        var layout = JournalVolumeLayout.Read(volume);
        var record = Read(volume, layout.MftCluster * layout.BytesPerCluster + 2L * layout.MftRecordSize,
            layout.MftRecordSize, logical: true);
        var (runs, _) = LogFileStorage.ReadDataAttribute(record, layout, "$LogFile");
        var endLcn = runs.Max(run => run.Lcn + run.Length);
        volume.SetLength((endLcn - 1) * layout.BytesPerCluster);
        AssertReplayRefused(volume, "outside the volume");
    }

    public static IEnumerable<object[]> InvalidLogRuns()
    {
        yield return [new byte[] { 0x11, 0, 10, 0 }, "invalid length"];
        yield return [new byte[] { 0x11, 1, 0, 0 }, "invalid or overflowing LCN"];
        yield return [new byte[] { 0x11, 1, 0xFF, 0 }, "invalid or overflowing LCN"];
        yield return [new byte[] { 0x18, 0, 0, 0, 0, 0, 0, 0, 0x80, 10, 0 }, "invalid length"];
        yield return [new byte[] { 0x81, 1, 0xFD, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F, 0x11, 1, 4, 0 }, "overflowing LCN"];
        yield return [new byte[] { 0x18, 0xFD, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F, 1, 0x11, 4, 0, 0 }, "invalid length"];
        yield return [new byte[] { 0x11, 8, 0x20, 0x11, 4, 1, 0 }, "overlapping physical runs"];
        yield return [new byte[] { 0x11, 1, 0x20 }, "no terminator"];
        yield return [new byte[] { 0 }, "empty"];
    }

    [Theory]
    [MemberData(nameof(InvalidLogRuns))]
    public void MalformedMappingPairs_AreRefused(byte[] runs, string reason)
    {
        var error = Assert.Throws<JournalException>(() => LogFileStorage.ParseRuns(runs, 0, runs.Length, long.MaxValue, "$LogFile"));
        Assert.Contains(reason, error.Message);
    }

    [Theory]
    [InlineData("boot cluster")]
    [InlineData("overflowing LCN")]
    [InlineData("overlaps MFT")]
    [InlineData("redirected within volume")]
    [InlineData("straddles volume end")]
    public void InvalidMirrorDestinations_AreRefusedBeforeAnyWrites(string damage)
    {
        using var volume = NtfsJournalWindowsSampleTest.LoadVolume("s512");
        NtfsJournal.Replay(volume);
        var layout = WriteFirstMftRecordUpdate(volume);
        Assert.Equal(NtfsJournalStatus.NeedsReplay, NtfsJournal.Analyze(volume).Status);

        var boot = Read(volume, 0, 512);
        var mirror = damage switch
        {
            "boot cluster" => 0,
            "overflowing LCN" => (1L << 55) + layout.MftMirrorCluster,
            "overlaps MFT" => layout.MftCluster + 1,
            "redirected within volume" => layout.MftMirrorCluster + 8,
            _ => layout.TotalClusters - 1,
        };
        Put64(boot, 0x38, (ulong)mirror);
        volume.Position = 0;
        volume.Write(boot, 0, boot.Length);
        AssertReplayRefused(volume, "$MFTMirr");
    }

    private static JournalVolumeLayout WriteFirstMftRecordUpdate(Stream volume)
    {
        var w = new TestJournalWriter(volume, sequence: 1000);
        var layout = w.Layout;
        var record = Read(volume, layout.MftCluster * layout.BytesPerCluster, layout.MftRecordSize, logical: true);
        var count = (layout.MftRecordSize + layout.BytesPerCluster - 1) / layout.BytesPerCluster;
        var lcns = Enumerable.Range(0, count).Select(i => layout.MftCluster + i).ToArray();
        var restart = w.Checkpoint(Attributes);
        w.Append(Op(NtfsLogOperation.UpdateResidentValue, Pattern, Mft, 0, lcns, recordOffset: U16(record, 0x14),
            attributeOffset: 0x18, undo: NtfsLogOperation.UpdateResidentValue, undoData: new byte[Pattern.Length]));
        w.Append(Forget());
        w.Finish(restart);
        return layout;
    }

    [Fact]
    public void MirroringAReplay_DoesNotOverwriteTheLogFileFallbackRecord()
    {
        using var volume = Volume();
        var layout = WriteFirstMftRecordUpdate(volume);
        var offset = 2L * layout.MftRecordSize;
        var primary = layout.MftCluster * layout.BytesPerCluster + offset;
        var mirror = layout.MftMirrorCluster * layout.BytesPerCluster + offset;
        var fallback = Read(volume, mirror, layout.MftRecordSize);
        volume.Position = primary + 0x1FE;
        var old = volume.ReadByte();
        volume.Position--;
        volume.WriteByte((byte)(old ^ 0xFF));

        Assert.Equal(NtfsJournalStatus.NeedsReplay, NtfsJournal.Replay(volume).Status);
        Assert.Equal(fallback, Read(volume, mirror, fallback.Length));
        Assert.Equal(NtfsJournalStatus.Clean, NtfsJournal.Analyze(volume).Status);
    }

    [Fact]
    public void ReplayCannotWriteMetadataIntoLogFileClusters()
    {
        using var volume = Volume();
        var layout = JournalVolumeLayout.Read(volume);
        var record = Read(volume, layout.MftCluster * layout.BytesPerCluster + 2L * layout.MftRecordSize,
            layout.MftRecordSize, logical: true);
        var (runs, _) = LogFileStorage.ReadDataAttribute(record, layout, "$LogFile");
        using var dirty = WithRecordsOn(volume, Raw(runs[0].Lcn, Pattern), Forget());
        AssertReplayRefused(dirty, "metadata replay write overlaps $LogFile");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ALogicalPageMustHaveTheSameCompleteClusterMapping(bool duplicateCluster)
    {
        using var volume = NtfsJournalWindowsSampleTest.LoadVolume("s512");
        NtfsJournal.Replay(volume);
        var w = new TestJournalWriter(volume, sequence: 1000);
        var restart = w.Checkpoint(Attributes);
        var cluster = w.Layout.TotalClusters - 16;
        w.Append(Op(NtfsLogOperation.InitializeFileRecordSegment, new byte[1024], Mft, 0, [cluster, cluster + 1]));
        w.Append(Op(NtfsLogOperation.InitializeFileRecordSegment, new byte[1024], Mft, 8,
            [cluster, duplicateCluster ? cluster : cluster + 2]));
        w.Append(Forget());
        w.Finish(restart);
        AssertReplayRefused(volume, duplicateCluster ? "more than one part of a page" : "conflicting cluster mappings");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void UnknownOperationsWithNoClusters_AreRefused(bool beforeCheckpoint, bool undo)
    {
        using var volume = Volume();
        var w = new TestJournalWriter(volume);
        var unknown = new TestLogRecord
        {
            Redo = undo ? NtfsLogOperation.Noop : (NtfsLogOperation)0xFFFF,
            Undo = undo ? (NtfsLogOperation)0xFFFF : NtfsLogOperation.Noop,
            TransactionId = 0x90,
        };

        ulong restart;
        if (beforeCheckpoint)
        {
            var lsn = w.Append(unknown);
            restart = w.Checkpoint(Attributes, transactions: [(0x90, TransactionState.Committed, lsn, lsn)]);
        }
        else
        {
            restart = w.Checkpoint(Attributes);
            w.Append(unknown);
        }

        w.Finish(restart);
        AssertReplayRefused(volume, "unsupported redo or undo operation");
    }

    [Theory]
    [InlineData("overflowing table size", "implausible header")]
    [InlineData("short dirty page entry", "dirty page table entry is too short")]
    [InlineData("overflowing cluster count", "more clusters than it holds")]
    [InlineData("overflowing dirty page VCN", "invalid VCN range")]
    [InlineData("short transaction entry", "transaction table entry is too short")]
    [InlineData("negative table length", "invalid table length")]
    public void CheckpointTables_AreBoundedBeforeReadingEntries(string damage, string reason)
    {
        using var volume = WithRestart((w, checkpoint) =>
        {
            var table = Table(damage is "overflowing cluster count" or "overflowing dirty page VCN" ? 0x28 : 8, 1);
            if (damage == "overflowing table size")
            {
                Put16(table, 0, ushort.MaxValue);
                Put16(table, 2, ushort.MaxValue);
            }
            else if (damage == "overflowing cluster count")
            {
                Put32(table, 0x18 + 0x0C, 0x20000000);
            }
            else if (damage == "overflowing dirty page VCN")
            {
                Put32(table, 0x18 + 0x0C, 1);
                Put64(table, 0x18 + 0x10, long.MaxValue);
            }

            if (damage == "short transaction entry")
            {
                var restart = RestartRecord(1, checkpoint);
                Put64(restart, 0x28, Dump(w, NtfsLogOperation.TransactionTableDump, table));
                Put32(restart, 0x3C, (uint)table.Length);
                return restart;
            }

            return RestartRecord(1, checkpoint, dirty: Dump(w, NtfsLogOperation.DirtyPageTableDump, table),
                dirtyLength: damage == "negative table length" ? -1 : table.Length);
        });
        AssertReplayRefused(volume, reason);
    }

    [Fact]
    public void AMissingRedoStart_CannotDiscardPendingChanges()
    {
        using var volume = Volume();
        var target = FreeCluster(volume);
        var w = new TestJournalWriter(volume);
        var missing = w.LsnOf(w.FirstPageOffset + 0x40, 0);
        var restart = w.Checkpoint(Attributes, dirtyPages: [(Data, target, missing, [target])]);
        w.Append(Raw(target, Pattern));
        w.Append(Forget());
        w.Finish(restart);
        AssertReplayRefused(volume, "redo start record");
    }

    [Theory]
    [InlineData(0x0D, 0)]
    [InlineData(0x0D, 0xE0)]
    [InlineData(0x40, 0xE1)]
    [InlineData(0x44, 0xE1)]
    public void InvalidBootGeometry_IsRefusedBeforeRecordAllocation(int offset, int value)
    {
        using var volume = Volume();
        volume.Position = offset;
        volume.WriteByte((byte)value);
        AssertReplayRefused(volume, "invalid volume or record sizes");
    }

    [Fact]
    public void InvalidFixupRanges_AreRejectedWithoutIndexingTheBuffer()
    {
        Assert.False(RemoveFixups(new byte[7], 0, 7));
        Assert.False(RemoveFixups(new byte[512], 1, 512));
        var page = new byte[512];
        Put16(page, 4, 508);
        Put16(page, 6, 2);
        Assert.False(HasFixupLayout(page, 0, page.Length));
    }

    [Theory]
    [InlineData(0x7FFFFFFFu)]
    [InlineData(0xFFFFFFFFu)]
    public void InvalidIndexBufferSizes_AreRefusedBeforePageAllocation(uint indexSize)
    {
        using var volume = Volume();
        var w = new TestJournalWriter(volume);
        var restart = w.Checkpoint([(Index, 0xA0, 5, indexSize, "$I30")]);
        w.Append(Op(NtfsLogOperation.UpdateNonresidentValue, Pattern, Index, 0, [FreeCluster(volume)]));
        w.Append(Forget());
        w.Finish(restart);
        AssertReplayRefused(volume, "invalid index buffer size");
    }

    [Fact]
    public void AnOversizedBitmapOperation_IsRefusedBeforeAllocation()
    {
        using var probe = Volume();
        var range = new byte[8];
        Put32(range, 4, uint.MaxValue);
        using var volume = WithRecords(Op(NtfsLogOperation.SetBitsInNonresidentBitMap, range, Data, 0, [FreeCluster(probe)]), Forget());
        AssertReplayRefused(volume, "oversized bit range");
    }

    [Theory]
    [InlineData(-1L, "invalid target VCN")]
    [InlineData(long.MaxValue, "invalid target VCN")]
    [InlineData(long.MaxValue / 2, "invalid file record VCN")]
    public void InvalidTargetVcns_AreRefusedBeforeReplay(long vcn, string reason)
    {
        using var probe = Volume();
        using var volume = WithRecords(Op(NtfsLogOperation.InitializeFileRecordSegment, new byte[1024], Mft, vcn, [FreeCluster(probe)]), Forget());
        AssertReplayRefused(volume, reason);
    }

    [Fact]
    public void ARangeOfFreedClustersMustNotOverflow()
    {
        var free = DeleteClusters(long.MaxValue);
        using var volume = WithRecords(free, Forget());
        AssertReplayRefused(volume, "invalid range of freed clusters");
    }

    [Theory]
    [InlineData(0x10, 60)]
    [InlineData(0x18, 0x20000)]
    [InlineData(0x26, 0xFF8)]
    public void InvalidRestartGeometry_IsRefusedBeforeReplay(int field, int value)
    {
        using var volume = WithRecords(Forget());
        PatchRestartPages(new TestJournalWriter(volume), page =>
        {
            if (field == 0x26)
            {
                Put16(page, 0x30 + field, (ushort)value);
            }
            else
            {
                Put32(page, 0x30 + field, (uint)value);
            }
        });
        AssertReplayRefused(volume, "implausible restart area");
    }

    [Fact]
    public void ALogSequenceThatWouldWrapPastItsMaximum_IsRefused()
    {
        using var volume = Volume();
        var geometry = new TestJournalWriter(volume);
        var writer = new TestJournalWriter(volume, startOffset: geometry.FileSize - TestJournalWriter.PageSize,
            sequence: (1UL << geometry.SequenceNumberBits) - 1);
        var restart = writer.Checkpoint([]);
        writer.AppendRaw(new byte[TestJournalWriter.PageSize], LfsRecord.TypeClientRestart, 0, 0);
        writer.Finish(restart);
        AssertReplayRefused(volume, "overflows the log sequence");
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(false, 3)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    public void ReplayFlushesMetadataBeforeTheResetAndPropagatesFlushFailures(bool alreadyApplied, int failFlush)
    {
        using var probe = Volume();
        var target = FreeCluster(probe);
        using var dirty = WithRecords(Raw(target, Pattern), Forget());
        if (alreadyApplied)
        {
            dirty.Position = target * 4096;
            dirty.Write(Pattern, 0, Pattern.Length);
        }

        var initial = dirty.ToArray();
        using var volume = new ReplayTrackingStream((byte[])initial.Clone(), failFlush);
        if (failFlush == 0)
        {
            var result = NtfsJournal.Replay(volume);
            Assert.Equal(alreadyApplied ? NtfsJournalStatus.NothingToReplay : NtfsJournalStatus.NeedsReplay, result.Status);
            Assert.True(result.Replayed);
        }
        else
        {
            Assert.Same(volume.Failure, Assert.Throws<IOException>(() => NtfsJournal.Replay(volume)));
        }

        var expected = new List<string>();
        if (!alreadyApplied)
        {
            expected.Add($"write {target * 4096}");
        }

        expected.Add("flush");
        if (failFlush == 1)
        {
            Assert.Equal(expected, volume.Events);
            var original = new TestJournalWriter(dirty).Storage;
            var result = new TestJournalWriter(volume).Storage;
            var first = new byte[2 * TestJournalWriter.PageSize];
            var second = new byte[first.Length];
            original.Read(0, first, 0, first.Length);
            result.Read(0, second, 0, second.Length);
            Assert.Equal(first, second);
            return;
        }

        Assert.Equal(expected, volume.Events.Take(expected.Count));
        var resetEvents = volume.Events.Skip(expected.Count).ToArray();
        Assert.Equal(failFlush == 2 ? 2 : 4, resetEvents.Length);
        for (var i = 0; i < resetEvents.Length; i += 2)
        {
            Assert.StartsWith("write ", resetEvents[i]);
            Assert.Equal("flush", resetEvents[i + 1]);
        }
    }

    [Fact]
    public void FileStreamReplay_RequestsDiskFlushesAtEveryBoundary()
    {
        using var dirty = WithRecords(Forget());
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, dirty.ToArray());
            using var file = new ReplayTrackingFile(path);
            Assert.True(NtfsJournal.Replay(file).Replayed);
            Assert.Equal(new[] { true, true, true }, file.DiskFlushes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AFailedMirrorWriteOrFlush_LeavesThePrimaryRecordAvailableForRetry(bool failFlush)
    {
        using var dirty = Volume();
        var layout = WriteFirstMftRecordUpdate(dirty);
        var mft = layout.MftCluster * layout.BytesPerCluster;
        var mirror = layout.MftMirrorCluster * layout.BytesPerCluster;
        Assert.True(mft < mirror);
        var original = Read(dirty, mft, layout.MftRecordSize);

        using var interrupted = new ReplayTrackingStream(dirty.ToArray(), failFlush ? 1 : 0, failFlush ? null : mirror);
        Assert.Same(interrupted.Failure, Assert.Throws<IOException>(() => NtfsJournal.Replay(interrupted)));
        Assert.Equal($"write {mirror}", interrupted.Events[0]);
        Assert.Equal(original, Read(interrupted, mft, layout.MftRecordSize));

        using var retry = JournalScenario.Copy(interrupted.ToArray());
        Assert.Equal(NtfsJournalStatus.NeedsReplay, NtfsJournal.Replay(retry).Status);
        Assert.Equal(Read(retry, mft, layout.MftRecordSize), Read(retry, mirror, layout.MftRecordSize));
        Assert.Equal(NtfsJournalStatus.Clean, NtfsJournal.Analyze(retry).Status);
    }

    private sealed class ReplayTrackingStream(byte[] image, int failFlush, long? failWrite = null) : MemoryStream(image, writable: true)
    {
        private int _flushes;

        public IOException Failure { get; } = new("Injected replay I/O failure");

        public List<string> Events { get; } = [];

        public override void Write(byte[] buffer, int offset, int count)
        {
            Events.Add($"write {Position}");
            if (Position == failWrite)
            {
                throw Failure;
            }

            base.Write(buffer, offset, count);
        }

        public override void Flush()
        {
            Events.Add("flush");
            if (++_flushes == failFlush)
            {
                throw Failure;
            }
        }
    }

    private sealed class ReplayTrackingFile(string path) : FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
    {
        public List<bool> DiskFlushes { get; } = [];

        public override void Flush(bool flushToDisk)
        {
            DiskFlushes.Add(flushToDisk);
            base.Flush(flushToDisk);
        }
    }
}
