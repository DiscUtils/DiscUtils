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
    /// Windows shut the volume down cleanly, or the journal is empty or was reset (also by chkdsk). The volume
    /// can be read and written as it is.
    /// </summary>
    Clean,

    /// <summary>
    /// Windows did not shut the volume down cleanly, but every change it logged is already on disk and no
    /// transaction needs rolling back. The volume can be read and written as it is.
    /// </summary>
    NothingToReplay,

    /// <summary>
    /// The journal holds changes that are missing on disk, or unfinished transactions that must be rolled back.
    /// Until the journal is replayed, the volume's metadata is out of date, and Windows replays the journal over
    /// any change made to it.
    /// </summary>
    NeedsReplay,

    /// <summary>
    /// The journal cannot be replayed by DiscUtils (unsupported version or operation, or damage); see
    /// <see cref="NtfsJournalInfo.Detail"/>. Only Windows (or chkdsk) can repair the volume.
    /// </summary>
    Unsupported,
}

/// <summary>
/// What to do with the journal when an <see cref="NtfsFileSystem"/> is opened.
/// </summary>
public enum NtfsJournalMode
{
    /// <summary>
    /// Look at the journal and replay it like Windows does at mount: on a writable stream the replay is written
    /// to the volume and the journal is reset; on a read-only stream it is kept in memory, so the file system
    /// shows the up-to-date metadata without writing anything. A journal DiscUtils cannot replay makes opening a
    /// writable stream fail with an <see cref="IOException"/>; a read-only stream is opened as it is.
    /// </summary>
    Replay,

    /// <summary>
    /// Open the volume only if the journal needs nothing (<see cref="NtfsJournalStatus.Clean"/> or
    /// <see cref="NtfsJournalStatus.NothingToReplay"/>); otherwise opening fails with an
    /// <see cref="IOException"/>. Nothing is written.
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

    /// <summary>Gets the state of the journal.</summary>
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

    /// <summary>Gets the number of unfinished transactions to roll back.</summary>
    public int TransactionsToUndo { get; internal set; }

    /// <summary>Gets the number of log records the undo pass applies.</summary>
    public int RecordsToUndo { get; internal set; }

    /// <summary>Gets the number of file records, index buffers and other pages the replay changes.</summary>
    public int PagesChanged { get; internal set; }

    /// <summary>Gets the MFT record numbers the replay changes.</summary>
    public IReadOnlyList<long> FileRecordsChanged { get; internal set; } = [];

    /// <summary>Gets a value indicating whether the replay was written to the volume.</summary>
    public bool Replayed { get; internal set; }

    /// <inheritdoc />
    public override string ToString() =>
        $"{Status}: {Detail} (LFS {LogVersion}, {RecordsAfterCheckpoint} records after the checkpoint, redo {RecordsToRedo}, " +
        $"undo {RecordsToUndo} in {TransactionsToUndo} transactions, {PagesChanged} pages)";
}

/// <summary>
/// Reads and replays the journal ($LogFile) of an NTFS volume, the way Windows does when it mounts a volume that
/// was not shut down cleanly (a crash, a power cut, a hard power-off, or a hibernated or Fast Startup Windows).
/// </summary>
public static class NtfsJournal
{
    /// <summary>
    /// Looks at the journal without writing anything: whether the volume needs a replay, and what it would change.
    /// </summary>
    /// <param name="volume">The NTFS volume (the partition, not the whole disk).</param>
    public static NtfsJournalInfo Analyze(Stream volume) => Run(volume, write: false, out _);

    /// <summary>
    /// Replays the journal onto the volume and resets it, so the volume is consistent and Windows has nothing left
    /// to replay. Does nothing if the journal is clean.
    /// </summary>
    /// <param name="volume">The NTFS volume (the partition, not the whole disk); must be writable.</param>
    /// <exception cref="IOException">The journal cannot be replayed (<see cref="NtfsJournalStatus.Unsupported"/>);
    /// nothing was written.</exception>
    public static NtfsJournalInfo Replay(Stream volume)
    {
        if (!volume.CanWrite)
        {
            throw new IOException("Replaying the NTFS journal needs a writable stream");
        }

        var info = Run(volume, write: true, out _);
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
        var info = Run(volume, write: false, out var clusters);
        replayed = null;
        if (info.Status == NtfsJournalStatus.NeedsReplay && clusters != null)
        {
            var snapshot = new SnapshotStream(volume, Ownership.None);
            snapshot.Snapshot();
            WriteClusters(snapshot, clusters, ClusterSize(volume));
            snapshot.Freeze();
            replayed = snapshot;
        }

        return info;
    }

    private static int ClusterSize(Stream volume) => JournalVolumeLayout.Read(volume).BytesPerCluster;

    private static NtfsJournalInfo Run(Stream volume, bool write, out Dictionary<long, byte[]> changedClusters)
    {
        changedClusters = null;
        JournalVolumeLayout layout;
        LfsLog log;
        LfsLog.Kind kind;
        LogFileStorage storage;
        try
        {
            layout = JournalVolumeLayout.Read(volume);
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

        if (log.Restart.IsClean || log.Restart.InUseClient == null)
        {
            info.Detail = "Windows shut the volume down cleanly";
            return info;
        }

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

            var clusters = replayer.Pages.Commit();
            AddMftMirror(volume, layout, clusters);
            info.PagesChanged = replayer.Pages.ChangedPageCount;
            if (clusters.Count == 0)
            {
                // Replaying changes nothing (bitmap updates, for instance, end where the disk already is).
                info.Status = NtfsJournalStatus.NothingToReplay;
                info.Detail = "every logged change is already on disk";
                info.RecordsToRedo = 0;
                info.RecordsToUndo = 0;
                info.TransactionsToUndo = 0;
                info.FileRecordsChanged = [];
                if (write)
                {
                    ResetLog(log, analysis.LastLsn);
                    volume.Flush();
                    info.Replayed = true;
                }

                return info;
            }

            info.Status = NtfsJournalStatus.NeedsReplay;
            info.Detail = $"{info.PagesChanged} pages are out of date on disk; {info.TransactionsToUndo} unfinished transactions to roll back";
            changedClusters = clusters;
            if (write)
            {
                // Pages first, then the reset: an interruption in between leaves a journal that replays to the
                // same result again.
                WriteClusters(volume, clusters, layout.BytesPerCluster);
                volume.Flush();
                ResetLog(log, analysis.LastLsn);
                volume.Flush();
                info.Replayed = true;
            }

            return info;
        }
        catch (JournalException e)
        {
            changedClusters = null;
            info.Status = NtfsJournalStatus.Unsupported;
            info.Detail = e.Message;
            return info;
        }
    }

    private static void WriteClusters(Stream stream, Dictionary<long, byte[]> clusters, int clusterSize)
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
    private static void AddMftMirror(Stream volume, JournalVolumeLayout layout, Dictionary<long, byte[]> clusters)
    {
        var mirrorBytes = 4 * layout.MftRecordSize;
        var clusterSize = layout.BytesPerCluster;
        var mftStart = layout.MftCluster * clusterSize;
        var touched = clusters.Keys.Any(lcn => lcn * clusterSize < mftStart + mirrorBytes && (lcn + 1) * clusterSize > mftStart);
        if (!touched)
        {
            return;
        }

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

        var mirrorStart = layout.MftMirrorCluster * clusterSize;
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
    }

    /// <summary>
    /// Leaves an empty journal: both restart pages say log file service 1.1, shut down cleanly, no client in use,
    /// with the end of the log as current LSN. Windows starts a new log from there at the next mount.
    /// </summary>
    private static void ResetLog(LfsLog log, ulong lastLsn)
    {
        var source = log.Restart;
        var page = (byte[])source.Page.Clone();
        var ra = source.RestartAreaOffset;
        Put16(page, 0x1A, 1);
        Put16(page, 0x1C, 1);
        Put64(page, ra + 0x00, Math.Max(lastLsn, source.CurrentLsn));
        Put16(page, ra + 0x0A, 0);
        Put16(page, ra + 0x0C, LfsRestartPage.NoClient);
        Put16(page, ra + 0x0E, LfsRestartPage.FlagCleanDismount);
        Put32(page, ra + 0x28, source.OpenLogCount + 1);
        Put32(page, 0, SignatureRstr);
        ApplyFixups(page, 0, page.Length);
        log.Storage.Write(0, page, 0, page.Length);
        log.Storage.Write(source.SystemPageSize, page, 0, page.Length);
    }
}
