using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiscUtils;
using DiscUtils.Ntfs;
using DiscUtils.Ntfs.Journal;
using DiscUtils.Streams;

namespace LibraryTests.Ntfs.Journal;

/// <summary>
/// Builds a volume that "Windows crashed on" from two DiscUtils images: <see cref="Before"/> and <see cref="After"/>
/// a set of changes. Every MFT record, index buffer and data cluster that differs becomes a log record (redo: the
/// after bytes, undo: the before bytes) in one transaction, the way NTFS logs page images.
/// </summary>
internal sealed class JournalScenario
{
    public const ushort MftData = 0x18;
    public const ushort IndexAllocation = 0x40;
    public const ushort PlainData = 0x68;
    public const uint Transaction = 0x40;

    private JournalScenario(byte[] before, byte[] after, int clusterSize)
    {
        Before = before;
        After = after;
        ClusterSize = clusterSize;
    }

    public byte[] Before { get; }

    public byte[] After { get; }

    public int ClusterSize { get; }

    /// <summary>Clusters of $LogFile, which the journal itself occupies (not compared).</summary>
    public HashSet<long> LogClusters { get; } = [];

    public List<TestLogRecord> Records { get; } = [];

    /// <summary>
    /// Formats a volume, runs <paramref name="change"/> on it with DiscUtils and turns the difference into log records.
    /// </summary>
    public static JournalScenario Create(Action<NtfsFileSystem> change, int sizeMb = 16, Action<NtfsFileSystem> setup = null)
    {
        var volume = new MemoryStream();
        var geometry = Geometry.FromCapacity(sizeMb * 1024L * 1024);
        using (var ntfs = NtfsFileSystem.Format(volume, "JOURNAL", geometry, 0, geometry.TotalSectorsLong))
        {
            setup?.Invoke(ntfs);
        }

        var before = volume.ToArray();
        using (var ntfs = new NtfsFileSystem(volume, NtfsJournalMode.Ignore))
        {
            change(ntfs);
        }

        var after = volume.ToArray();
        var scenario = new JournalScenario(before, after, 4096);
        scenario.Diff();
        return scenario;
    }

    /// <summary>Gets a writable copy of an image.</summary>
    public static MemoryStream Copy(byte[] image)
    {
        var copy = new MemoryStream();
        copy.Write(image, 0, image.Length);
        copy.Position = 0;
        return copy;
    }

    /// <summary>
    /// Writes the journal for the scenario into <paramref name="volume"/>: a checkpoint, the records (each chained
    /// to the previous one of the transaction for undo), and, when <paramref name="committed"/>, the end of the
    /// transaction. Returns the writer (for the LSNs).
    /// </summary>
    public TestJournalWriter WriteJournal(Stream volume, bool committed, int majorVersion = 2, long? startOffset = null,
        Action<TestJournalWriter> beforeRecords = null, Func<TestLogRecord, TestLogRecord> adjust = null)
    {
        var writer = new TestJournalWriter(volume, majorVersion, startOffset);
        var restart = writer.Checkpoint(
        [
            (MftData, 0x80, 0x0001000000000000UL, 0, ""),
            (IndexAllocation, 0xA0, 0x0005000000000005UL, 4096, "$I30"),
            (PlainData, 0x80, 0x0001000000000006UL, 0, ""),
        ]);
        beforeRecords?.Invoke(writer);
        ulong previous = 0;
        foreach (var record in Records)
        {
            var r = adjust != null ? adjust(record) : record;
            r.UndoNextLsn = previous;
            previous = writer.Append(r);
        }

        if (committed)
        {
            writer.Append(new TestLogRecord
            {
                Redo = NtfsLogOperation.ForgetTransaction,
                Undo = NtfsLogOperation.CompensationLogRecord,
                TransactionId = Transaction,
            });
        }

        writer.Finish(restart);
        return writer;
    }

    /// <summary>
    /// Asserts that <paramref name="volume"/> equals <paramref name="expected"/> outside $LogFile, ignoring what a
    /// replay legitimately changes in file records and index buffers: the LSN and the update sequence number.
    /// </summary>
    public void AssertVolumeEquals(byte[] expected, Stream volume)
    {
        var actual = ((MemoryStream)volume).ToArray();
        Assert.Equal(expected.Length, actual.Length);
        for (long lcn = 0; lcn < expected.Length / ClusterSize; lcn++)
        {
            if (LogClusters.Contains(lcn))
            {
                continue;
            }

            var offset = (int)(lcn * ClusterSize);
            var e = Normalize(expected, offset);
            var a = Normalize(actual, offset);
            if (!e.AsSpan().SequenceEqual(a))
            {
                var first = 0;
                while (e[first] == a[first])
                {
                    first++;
                }

                Assert.Fail($"Cluster {lcn} differs at 0x{first:X}: expected {e[first]:X2}, got {a[first]:X2}");
            }
        }
    }

    private byte[] Normalize(byte[] image, int offset)
    {
        var cluster = image.AsSpan(offset, ClusterSize).ToArray();
        var unit = EndianUtilities.ToUInt32LittleEndian(cluster, 0) == JournalUtilities.SignatureIndx ? ClusterSize : 1024;
        for (var start = 0; start + unit <= ClusterSize; start += unit)
        {
            var signature = EndianUtilities.ToUInt32LittleEndian(cluster, start);
            if (signature == JournalUtilities.SignatureFile || signature == JournalUtilities.SignatureIndx)
            {
                JournalUtilities.RemoveFixups(cluster, start, unit);
                var usaOffset = EndianUtilities.ToUInt16LittleEndian(cluster, start + 4);
                var usaCount = EndianUtilities.ToUInt16LittleEndian(cluster, start + 6);
                Array.Clear(cluster, start + 8, 8);
                Array.Clear(cluster, start + usaOffset, 2 * usaCount);
            }
        }

        return cluster;
    }

    private void Diff()
    {
        using var afterVolume = Copy(After);
        using var ntfs = new NtfsFileSystem(afterVolume, NtfsJournalMode.Ignore);
        foreach (var range in ntfs.PathToClusters("$LogFile"))
        {
            for (var lcn = range.Offset; lcn < range.Offset + range.Count; lcn++)
            {
                LogClusters.Add(lcn);
            }
        }

        var mftVcn = new Dictionary<long, long>();
        long vcn = 0;
        foreach (var range in ntfs.PathToClusters("$MFT"))
        {
            for (var lcn = range.Offset; lcn < range.Offset + range.Count; lcn++)
            {
                mftVcn[lcn] = vcn++;
            }
        }

        for (long lcn = 0; lcn < After.Length / ClusterSize; lcn++)
        {
            var offset = (int)(lcn * ClusterSize);
            if (LogClusters.Contains(lcn) || Before.AsSpan(offset, ClusterSize).SequenceEqual(After.AsSpan(offset, ClusterSize)))
            {
                continue;
            }

            if (mftVcn.TryGetValue(lcn, out var recordVcn))
            {
                for (var r = 0; r < ClusterSize / 1024; r++)
                {
                    var start = offset + (r * 1024);
                    if (Before.AsSpan(start, 1024).SequenceEqual(After.AsSpan(start, 1024)))
                    {
                        continue;
                    }

                    Records.Add(new TestLogRecord
                    {
                        Redo = NtfsLogOperation.InitializeFileRecordSegment,
                        Undo = NtfsLogOperation.InitializeFileRecordSegment,
                        TransactionId = Transaction,
                        RedoData = Logical(After, start, 1024),
                        UndoData = Logical(Before, start, 1024),
                        TargetAttribute = MftData,
                        TargetVcn = recordVcn,
                        ClusterBlockOffset = r * 2,
                        Lcns = [lcn],
                    });
                }
            }
            else if (EndianUtilities.ToUInt32LittleEndian(After, offset) == JournalUtilities.SignatureIndx)
            {
                Records.Add(new TestLogRecord
                {
                    Redo = NtfsLogOperation.UpdateNonresidentValue,
                    Undo = NtfsLogOperation.UpdateNonresidentValue,
                    TransactionId = Transaction,
                    RedoData = Logical(After, offset, ClusterSize),
                    UndoData = Logical(Before, offset, ClusterSize),
                    TargetAttribute = IndexAllocation,
                    TargetVcn = lcn,
                    Lcns = [lcn],
                });
            }
            else
            {
                Records.Add(new TestLogRecord
                {
                    Redo = NtfsLogOperation.UpdateNonresidentValue,
                    Undo = NtfsLogOperation.UpdateNonresidentValue,
                    TransactionId = Transaction,
                    RedoData = After.AsSpan(offset, ClusterSize).ToArray(),
                    UndoData = Before.AsSpan(offset, ClusterSize).ToArray(),
                    TargetAttribute = PlainData,
                    TargetVcn = lcn,
                    Lcns = [lcn],
                });
            }
        }
    }

    /// <summary>The bytes of a file record or index buffer with the update sequence array removed.</summary>
    private static byte[] Logical(byte[] image, int offset, int length)
    {
        var bytes = image.AsSpan(offset, length).ToArray();
        var signature = EndianUtilities.ToUInt32LittleEndian(bytes, 0);
        if (signature == JournalUtilities.SignatureFile || signature == JournalUtilities.SignatureIndx)
        {
            Assert.True(JournalUtilities.RemoveFixups(bytes, 0, length));
        }

        return bytes;
    }
}
