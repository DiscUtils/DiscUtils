//
// Copyright (c) 2026, DiscUtils contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiscUtils.Ntfs.Journal;
using DiscUtils.Streams;
using static DiscUtils.Ntfs.Journal.JournalUtilities;

namespace DiscUtils.Ntfs;

/// <summary>
/// The state of an NTFS volume's journal ($LogFile).
/// </summary>
public enum NtfsJournalStatus
{
    /// <summary>
    /// Windows shut the volume down cleanly, or the journal is empty or was reset (also by chkdsk).
    /// </summary>
    Clean,

    /// <summary>
    /// The journal was left open, but replay would make no metadata changes. An explicit replay resets it.
    /// </summary>
    NothingToReplay,

    /// <summary>
    /// The journal holds changes that are missing on disk, or unfinished transactions that must be rolled back.
    /// Until the journal is replayed, the volume's metadata is out of date and later recovery may overwrite
    /// intervening writes.
    /// </summary>
    NeedsReplay,

    /// <summary>
    /// The journal cannot be replayed by DiscUtils (unsupported version or operation, or damage); see
    /// <see cref="NtfsJournalInfo.Detail"/>.
    /// </summary>
    Unsupported,
}

/// <summary>
/// What to do with the journal when an <see cref="NtfsFileSystem"/> is opened.
/// </summary>
public enum NtfsJournalMode
{
    /// <summary>
    /// Replay supported journal changes. On a writable stream, replay writes metadata and resets the journal
    /// only when <see cref="NtfsJournalInfo.CanReplayPersistently"/> is true. On a read-only stream, replay uses
    /// an in-memory overlay. A journal DiscUtils cannot replay makes opening a writable stream fail with an
    /// <see cref="IOException"/>; a read-only stream is opened as it is.
    /// </summary>
    Replay,

    /// <summary>
    /// Require a clean journal for a writable open. A read-only open also accepts
    /// <see cref="NtfsJournalStatus.NothingToReplay"/>. Other states cause an <see cref="IOException"/>.
    /// </summary>
    Refuse,

    /// <summary>
    /// Do not look at the journal (the behavior of DiscUtils before journal support). A volume that needs a
    /// replay then shows out-of-date metadata, and changes to it can be overwritten by Windows' own replay.
    /// </summary>
    Ignore,
}

/// <summary>
/// The result of looking at (and possibly replaying) an NTFS journal.
/// </summary>
public sealed class NtfsJournalInfo
{
    internal NtfsJournalInfo(NtfsJournalStatus status, string detail)
    {
        Status = status;
        Detail = detail;
    }

    /// <summary>Gets the state found during analysis, before any replay or journal reset.</summary>
    public NtfsJournalStatus Status { get; internal set; }

    /// <summary>Gets a human-readable explanation (for <see cref="NtfsJournalStatus.Unsupported"/>, why).</summary>
    public string Detail { get; internal set; }

    /// <summary>Gets the log file service version, e.g. "2.0" (empty when there is no log).</summary>
    public string LogVersion { get; internal set; } = "";

    /// <summary>Gets the LSN of the last record in the log (0 when there is no log to read).</summary>
    public ulong LastLsn { get; internal set; }

    /// <summary>Gets the number of log records after the last checkpoint.</summary>
    public int RecordsAfterCheckpoint { get; internal set; }

    /// <summary>Gets the number of log records the redo pass applies (0 when replaying changes nothing).</summary>
    public int RecordsToRedo { get; internal set; }

    /// <summary>Gets the number of unfinished transactions with undo actions targeting metadata.</summary>
    public int TransactionsToUndo { get; internal set; }

    /// <summary>Gets the number of log records the undo pass applies.</summary>
    public int RecordsToUndo { get; internal set; }

    /// <summary>Gets the number of file records, index buffers and other pages the replay changes.</summary>
    public int PagesChanged { get; internal set; }

    /// <summary>Gets the MFT record numbers the replay changes.</summary>
    public IReadOnlyList<long> FileRecordsChanged { get; internal set; } = [];

    /// <summary>
    /// Gets a value indicating whether replay wrote to the volume, reset the journal and completed its flush calls.
    /// This is also true for a reset with no metadata changes (<see cref="NtfsJournalStatus.NothingToReplay"/>).
    /// </summary>
    public bool Replayed { get; internal set; }

    /// <summary>Gets a value indicating whether the read-only mount uses an in-memory replay overlay.</summary>
    public bool ReplayedInMemory { get; internal set; }

    /// <summary>
    /// Gets whether this journal is eligible for persistent replay. Metadata undo requires compensation records
    /// for recoverable on-disk replay and is available only in memory. Resetting a log with multiple client slots
    /// is unsupported. This does not describe stream capabilities.
    /// </summary>
    public bool CanReplayPersistently => Status != NtfsJournalStatus.Unsupported && TransactionsToUndo == 0 && !HasMultipleClientSlots;

    internal bool HasMultipleClientSlots { get; set; }

    /// <inheritdoc />
    public override string ToString() =>
        $"{Status}: {Detail} (LFS {LogVersion}, {RecordsAfterCheckpoint} records after the checkpoint, redo {RecordsToRedo}, " +
        $"undo {RecordsToUndo} in {TransactionsToUndo} transactions, {PagesChanged} pages)";
}

/// <summary>
/// Reads and replays supported journal ($LogFile) records on an NTFS volume after an unclean shutdown.
/// </summary>
/// <remarks>
/// The volume of a hibernated Windows (also after a shutdown with Fast Startup) is not shut down cleanly either,
/// but Windows does not replay its journal: it resumes with the metadata it kept in memory. Replaying the journal
/// of such a volume, like any other write to it, changes the volume behind Windows' back.
/// Use an offline volume and exclusive access to its stream for analysis and replay.
/// </remarks>
public static class NtfsJournal
{
    /// <summary>
    /// Looks at the journal without writing anything: whether the volume needs a replay, and what it would change.
    /// </summary>
    /// <param name="volume">The NTFS volume (the partition, not the whole disk).</param>
    public static NtfsJournalInfo Analyze(Stream volume) => Run(volume, write: false, out _, out _);

    /// <summary>
    /// Replays the journal onto the volume and resets it. Does nothing if the journal is clean.
    /// </summary>
    /// <param name="volume">The NTFS volume (the partition, not the whole disk); must be writable.</param>
    /// <remarks>
    /// Replay prepares the changes before writing. If the MFT mirror changes, it is written and flushed before
    /// the primary records. Replay flushes metadata before resetting the restart pages. It writes and flushes
    /// the alternate restart page before replacing and flushing the source page.
    /// A <see cref="FileStream"/> uses <c>Flush(true)</c>. For other streams, durability and ordering after a crash
    /// depend on whether <see cref="Stream.Flush()"/> reaches all backing storage layers.
    /// Replay is not atomic. An I/O error during the writes or flushes can leave a partially updated volume.
    /// Journals that require metadata undo cannot be written by this API.
    /// </remarks>
    /// <exception cref="IOException">The stream is read-only, the journal is unsupported (before writing), or an I/O error occurred.</exception>
    public static NtfsJournalInfo Replay(Stream volume)
    {
        var info = Run(volume, write: true, out _, out _);
        if (info.Status == NtfsJournalStatus.Unsupported)
        {
            throw new IOException($"The NTFS journal cannot be replayed: {info.Detail}");
        }

        return info;
    }

    /// <summary>
    /// Replays the journal into memory only: the result is a stream over <paramref name="volume"/> that shows the
    /// replayed metadata, while <paramref name="volume"/> itself is never written.
    /// </summary>
    internal static NtfsJournalInfo ReplayInMemory(Stream volume, out SparseStream replayed)
    {
        var info = Run(volume, write: false, out var clusters, out var clusterSize);
        replayed = null;
        if (info.Status == NtfsJournalStatus.NeedsReplay && clusters != null)
        {
            var snapshot = new SnapshotStream(volume, Ownership.None);
            snapshot.Snapshot();
            WriteClusters(snapshot, clusters, clusterSize);
            snapshot.Freeze();
            replayed = snapshot;
            info.ReplayedInMemory = true;
        }

        return info;
    }

    private static NtfsJournalInfo Run(Stream volume, bool write, out Dictionary<long, byte[]> changedClusters, out int clusterSize)
    {
        if (volume == null)
        {
            throw new ArgumentNullException(nameof(volume));
        }

        if (!volume.CanRead || !volume.CanSeek)
        {
            throw new ArgumentException("Reading the NTFS journal needs a readable, seekable stream", nameof(volume));
        }

        if (write && !volume.CanWrite)
        {
            throw new IOException("Replaying the NTFS journal needs a writable stream");
        }

        changedClusters = null;
        clusterSize = 0;
        JournalVolumeLayout layout;
        LfsLog log;
        LfsLog.Kind kind;
        LogFileStorage storage;
        try
        {
            layout = JournalVolumeLayout.Read(volume);
            clusterSize = layout.BytesPerCluster;
            storage = LogFileStorage.Open(volume, layout);
            log = LfsLog.Open(storage, out kind);
        }
        catch (JournalException e)
        {
            return new NtfsJournalInfo(NtfsJournalStatus.Unsupported, e.Message);
        }

        if (kind == LfsLog.Kind.Empty)
        {
            return new NtfsJournalInfo(NtfsJournalStatus.Clean, "the journal is empty (freshly formatted or reset)");
        }

        if (kind == LfsLog.Kind.CheckedByChkdsk)
        {
            return new NtfsJournalInfo(NtfsJournalStatus.Clean, "chkdsk repaired the volume and obsoleted the journal");
        }

        var info = new NtfsJournalInfo(NtfsJournalStatus.Clean, "")
        {
            LogVersion = $"{log.Restart.MajorVersion}.{log.Restart.MinorVersion}",
        };

        if (!log.Restart.RequiresRecovery)
        {
            info.Detail = "Windows shut the volume down cleanly";
            return info;
        }

        Dictionary<long, byte[]> clusters;
        byte[] alternateRestart = null;
        byte[] sourceRestart = null;
        var sourceOffset = log.Restart.Offset;
        var alternateOffset = log.Restart.SystemPageSize - sourceOffset;
        long mirrorClusterCount;
        try
        {
            var analysis = NtfsLogAnalysis.Run(log, layout);
            info.RecordsAfterCheckpoint = analysis.RecordsAfterCheckpoint;
            info.LastLsn = analysis.LastLsn;
            var replayer = new NtfsLogReplayer(volume, layout, analysis);
            replayer.Redo();
            replayer.Undo();
            info.RecordsToRedo = replayer.RedoneRecords;
            info.RecordsToUndo = replayer.UndoneRecords;
            info.TransactionsToUndo = replayer.UndoneTransactions;
            info.FileRecordsChanged = [.. replayer.ChangedFileRecords];
            info.HasMultipleClientSlots = log.Restart.LogClients != 1;
            if (write && !info.CanReplayPersistently)
            {
                // The reset image closes the log and puts slot zero on the free list.
                throw new JournalException(info.HasMultipleClientSlots ? "Persistent replay can only reset a log with a single LFS client slot" :
                    "Persistent replay requires metadata undo, which DiscUtils supports only in memory");
            }

            clusters = replayer.Pages.Commit();
            mirrorClusterCount = AddMftMirror(volume, layout, clusters);
            if (clusters.Keys.Any(storage.ContainsCluster))
            {
                throw new JournalException("A metadata replay write overlaps $LogFile");
            }

            if (log.PublishedReset is { } published &&
                (published.CurrentLsn != Math.Max(analysis.LastLsn, log.Restart.CurrentLsn) || clusters.Count != 0 || !info.CanReplayPersistently))
            {
                throw new JournalException("A partial $LogFile reset disagrees with recovery from its remaining dirty page");
            }

            if (write)
            {
                if (log.PublishedReset == null)
                {
                    alternateRestart = PrepareLogReset(log, analysis.LastLsn, alternateOffset);
                }

                sourceRestart = PrepareLogReset(log, analysis.LastLsn, sourceOffset);
            }

            info.PagesChanged = replayer.Pages.ChangedPageCount;
            if (clusters.Count == 0)
            {
                // Replaying changes nothing (bitmap updates, for instance, end where the disk already is).
                info.Status = NtfsJournalStatus.NothingToReplay;
                info.Detail = log.PublishedReset == null ? "the journal is still open, but replay would not change metadata" :
                    "one restart page still needs to be reset";
                if (info.TransactionsToUndo == 0)
                {
                    info.RecordsToRedo = 0;
                    info.RecordsToUndo = 0;
                }

                info.FileRecordsChanged = [];
            }
            else
            {
                info.Status = NtfsJournalStatus.NeedsReplay;
                info.Detail = $"{info.PagesChanged} pages are out of date on disk; {info.TransactionsToUndo} unfinished transactions to roll back";
            }
        }
        catch (JournalException e)
        {
            info.Status = NtfsJournalStatus.Unsupported;
            info.Detail = e.Message;
            return info;
        }

        changedClusters = clusters;
        if (write)
        {
            bool IsMirrorCluster(KeyValuePair<long, byte[]> cluster) => mirrorClusterCount > 0 &&
                cluster.Key >= layout.MftMirrorCluster && cluster.Key - layout.MftMirrorCluster < mirrorClusterCount;

            if (mirrorClusterCount > 0)
            {
                // A retry can skip primary records whose LSN already advanced. Their copies must be written first.
                WriteClusters(volume, clusters.Where(IsMirrorCluster), clusterSize);
                FlushReplayWrites(volume);
            }

            WriteClusters(volume, clusters.Where(cluster => !IsMirrorCluster(cluster)), clusterSize);
            // Flush even when no pages changed. A previous attempt may have left visible but buffered metadata.
            FlushReplayWrites(volume);
            if (alternateRestart != null)
            {
                storage.Write(alternateOffset, alternateRestart, 0, alternateRestart.Length);
                FlushReplayWrites(volume);
            }

            storage.Write(sourceOffset, sourceRestart, 0, sourceRestart.Length);
            FlushReplayWrites(volume);
            info.Replayed = true;
        }

        return info;
    }

    private static void FlushReplayWrites(Stream volume)
    {
        if (volume is FileStream file)
        {
            file.Flush(flushToDisk: true);
        }
        else
        {
            volume.Flush();
        }
    }

    private static void WriteClusters(Stream stream, IEnumerable<KeyValuePair<long, byte[]>> clusters, int clusterSize)
    {
        foreach (var cluster in clusters.OrderBy(c => c.Key))
        {
            stream.Position = cluster.Key * clusterSize;
            stream.Write(cluster.Value, 0, cluster.Value.Length);
        }
    }

    /// <summary>
    /// $MFTMirr holds a copy of the first MFT records; when the replay changes them, the copy follows.
    /// </summary>
    private static long AddMftMirror(Stream volume, JournalVolumeLayout layout, Dictionary<long, byte[]> clusters)
    {
        var mirrorBytes = 4 * layout.MftRecordSize;
        var clusterSize = layout.BytesPerCluster;
        var mirrorClusters = (mirrorBytes + (long)clusterSize - 1) / clusterSize;
        var touched = clusters.Keys.Any(lcn => lcn >= layout.MftCluster && lcn - layout.MftCluster < mirrorClusters);
        if (!touched)
        {
            return 0;
        }

        if (!layout.ContainsClusters(layout.MftCluster, mirrorClusters) ||
            !layout.ContainsClusters(layout.MftMirrorCluster, mirrorClusters) ||
            Math.Abs(layout.MftCluster - layout.MftMirrorCluster) < mirrorClusters)
        {
            throw new JournalException("$MFTMirr is outside the volume or overlaps $MFT");
        }

        var mftStart = layout.MftCluster * clusterSize;
        var bytes = new byte[mirrorBytes];
        for (var done = 0; done < mirrorBytes;)
        {
            var position = mftStart + done;
            var lcn = position / clusterSize;
            var inCluster = (int)(position % clusterSize);
            var chunk = Math.Min(mirrorBytes - done, clusterSize - inCluster);
            if (clusters.TryGetValue(lcn, out var cluster))
            {
                Array.Copy(cluster, inCluster, bytes, done, chunk);
            }
            else
            {
                ReadExactly(volume, position, bytes, done, chunk);
            }

            done += chunk;
        }

        // The boot sector locates the mirror, but its data attribute must confirm the destination.
        var mirrorRecord = new byte[layout.MftRecordSize];
        Array.Copy(bytes, layout.MftRecordSize, mirrorRecord, 0, mirrorRecord.Length);
        if (U32(mirrorRecord, 0) != SignatureFile || !RemoveFixups(mirrorRecord, 0, mirrorRecord.Length) || (U16(mirrorRecord, 0x16) & 1) == 0)
        {
            throw new JournalException("$MFTMirr has no valid base file record");
        }

        var (extents, length) = LogFileStorage.ReadDataAttribute(mirrorRecord, layout, "$MFTMirr");
        if (length < mirrorBytes || extents[0].Lcn != layout.MftMirrorCluster || extents[0].Length < mirrorClusters)
        {
            throw new JournalException("$MFTMirr data does not match the contiguous mirror location in the boot sector");
        }

        var mirrorStart = layout.MftMirrorCluster * clusterSize;
        for (var recordOffset = 0; recordOffset < mirrorBytes; recordOffset += mirrorRecord.Length)
        {
            Array.Copy(bytes, recordOffset, mirrorRecord, 0, mirrorRecord.Length);
            if (U32(mirrorRecord, 0) == SignatureFile && RemoveFixups(mirrorRecord, 0, mirrorRecord.Length))
            {
                continue;
            }

            // Keep the mirror's copy of a damaged, unmodified primary record. It may be the only valid copy.
            ReadExactly(volume, mftStart + recordOffset, mirrorRecord, 0, mirrorRecord.Length);
            if (!mirrorRecord.AsSpan().SequenceEqual(bytes.AsSpan(recordOffset, mirrorRecord.Length)))
            {
                throw new JournalException("A changed file record has no valid image for $MFTMirr");
            }

            ReadExactly(volume, mirrorStart + recordOffset, bytes, recordOffset, mirrorRecord.Length);
        }

        for (var done = 0; done < mirrorBytes;)
        {
            var position = mirrorStart + done;
            var lcn = position / clusterSize;
            var inCluster = (int)(position % clusterSize);
            var chunk = Math.Min(mirrorBytes - done, clusterSize - inCluster);
            if (!clusters.TryGetValue(lcn, out var cluster))
            {
                cluster = new byte[clusterSize];
                ReadExactly(volume, lcn * clusterSize, cluster, 0, clusterSize);
                clusters[lcn] = cluster;
            }

            Array.Copy(bytes, done, cluster, inCluster, chunk);
            done += chunk;
        }

        return mirrorClusters;
    }

    /// <summary>
    /// Leaves an empty journal: both restart pages say log file service 1.1, shut down cleanly, no client in use,
    /// with the end of the log as current LSN. Windows starts a new log from there at the next mount.
    /// </summary>
    private static byte[] PrepareLogReset(LfsLog log, ulong lastLsn, long offset)
    {
        var page = log.Restart.CreateResetImage(lastLsn);
        var replaced = new byte[page.Length];
        log.Storage.Read(offset, replaced, 0, replaced.Length);
        ApplyFixups(page, 0, page.Length, replaced);
        return page;
    }
}
