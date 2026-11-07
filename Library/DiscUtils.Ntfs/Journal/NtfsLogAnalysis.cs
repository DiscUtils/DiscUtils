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
using System.Linq;
using System.Text;
using static DiscUtils.Ntfs.Journal.JournalUtilities;

namespace DiscUtils.Ntfs.Journal;

/// <summary>
/// The analysis pass of NTFS recovery: starting from the tables of the last checkpoint, it reads every record
/// after the checkpoint and works out which pages may be stale on disk (the dirty page table) and which
/// transactions were not finished (the transaction table).
/// </summary>
internal sealed class NtfsLogAnalysis
{
    private NtfsLogAnalysis(LfsLog log, NtfsRestartRecord restart)
    {
        Log = log;
        RestartRecord = restart;
    }

    public LfsLog Log { get; }

    public NtfsRestartRecord RestartRecord { get; }

    /// <summary>The open attribute table, keyed by entry offset (what log records call the target attribute).</summary>
    public Dictionary<int, OpenAttributeEntry> OpenAttributes { get; } = [];

    public List<DirtyPageEntry> DirtyPages { get; } = [];

    /// <summary>The transaction table, keyed by entry offset (the transaction id of log records).</summary>
    public Dictionary<uint, TransactionEntry> Transactions { get; } = [];

    /// <summary>Clusters per dirty page (from the checkpoint's table, else one log page worth).</summary>
    public int ClustersPerPage { get; private set; }

    /// <summary>Where the redo pass starts.</summary>
    public ulong RedoLsn { get; private set; }

    /// <summary>The last record of the log.</summary>
    public ulong LastLsn { get; private set; }

    public int RecordsAfterCheckpoint { get; private set; }

    /// <summary>Transactions that were still running at the crash and have changes to roll back.</summary>
    public IEnumerable<KeyValuePair<uint, TransactionEntry>> Losers =>
        Transactions.Where(t => t.Value.State == TransactionState.Active && t.Value.UndoNextLsn != 0);

    public static NtfsLogAnalysis Run(LfsLog log, JournalVolumeLayout layout)
    {
        var client = log.Restart.InUseClient ?? throw new JournalException("The log has no client in use");
        var restartLfs = log.ReadRecord(client.RestartLsn)
                         ?? throw new JournalException($"The NTFS restart record at {client.RestartLsn:X} is not in the log");
        var analysis = new NtfsLogAnalysis(log, NtfsRestartRecord.Parse(restartLfs));
        analysis.LoadCheckpointTables(layout);
        analysis.ReadAfterCheckpoint();
        return analysis;
    }

    public DirtyPageEntry FindDirtyPage(uint targetAttribute, long vcn)
    {
        foreach (var page in DirtyPages)
        {
            if (page.Covers(targetAttribute, vcn))
            {
                return page;
            }
        }

        return null;
    }

    private byte[] ReadTableDump(ulong lsn, int length, string name)
    {
        if (lsn == 0 || length == 0)
        {
            return null;
        }

        var lfs = Log.ReadRecord(lsn) ?? throw new JournalException($"The {name} dump at {lsn:X} is not in the log");
        var record = NtfsLogRecord.TryParse(lfs, out var error) ?? throw new JournalException(error);
        var data = record.RedoData;
        if (data.Count < length)
        {
            throw new JournalException($"The {name} dump at {lsn:X} is shorter than the restart record says");
        }

        return data.ToArray();
    }

    private void LoadCheckpointTables(JournalVolumeLayout layout)
    {
        var r = RestartRecord;
        var version = r.MajorVersion;

        var openAttributes = ReadTableDump(r.OpenAttributeTableLsn, r.OpenAttributeTableLength, "open attribute table");
        if (openAttributes != null)
        {
            foreach (var (offset, size) in RestartTable.Entries(openAttributes))
            {
                var expected = version == 0 ? OpenAttributeEntry.SizeVersion0 : OpenAttributeEntry.SizeVersion1;
                if (size != expected)
                {
                    throw new JournalException($"Open attribute table entries are {size} bytes, expected {expected}");
                }

                OpenAttributes[offset] = OpenAttributeEntry.Parse(openAttributes, offset, version);
            }
        }

        var names = ReadTableDump(r.AttributeNamesLsn, r.AttributeNamesLength, "attribute names");
        if (names != null)
        {
            foreach (var (offset, name) in RestartTable.AttributeNames(names))
            {
                if (OpenAttributes.TryGetValue(offset, out var entry))
                {
                    entry.Name = name;
                }
            }
        }

        var dirtyPages = ReadTableDump(r.DirtyPageTableLsn, r.DirtyPageTableLength, "dirty page table");
        var lcnsOffset = version == 0 ? 0x24 : 0x20;
        ClustersPerPage = Math.Max(1, Log.PageSize / layout.BytesPerCluster);
        if (dirtyPages != null)
        {
            var first = true;
            foreach (var (offset, size) in RestartTable.Entries(dirtyPages))
            {
                if (first)
                {
                    ClustersPerPage = (size - lcnsOffset) / 8;
                    first = false;
                }

                DirtyPages.Add(DirtyPageEntry.Parse(dirtyPages, offset, size, version));
            }

            if (first && dirtyPages.Length >= 2)
            {
                ClustersPerPage = Math.Max(1, (U16(dirtyPages, 0) - lcnsOffset) / 8);
            }
        }

        var transactions = ReadTableDump(r.TransactionTableLsn, r.TransactionTableLength, "transaction table");
        if (transactions != null)
        {
            foreach (var (offset, _) in RestartTable.Entries(transactions))
            {
                Transactions[(uint)offset] = TransactionEntry.Parse(transactions, offset);
            }
        }

        if (ClustersPerPage < 1 || (ClustersPerPage & (ClustersPerPage - 1)) != 0)
        {
            throw new JournalException($"A dirty page covers {ClustersPerPage} clusters, which is not a power of two");
        }
    }

    private void ReadAfterCheckpoint()
    {
        var checkpoint = RestartRecord.CheckpointLsn;
        ulong firstAfterCheckpoint = 0;
        LastLsn = checkpoint;
        foreach (var lfs in Log.ReadForward(checkpoint))
        {
            LastLsn = lfs.Lsn;
            if (lfs.Lsn == checkpoint)
            {
                // Analysis starts with the record after the checkpoint.
                continue;
            }

            RecordsAfterCheckpoint++;
            if (firstAfterCheckpoint == 0)
            {
                firstAfterCheckpoint = lfs.Lsn;
            }

            if (lfs.RecordType != LfsRecord.TypeClientRecord)
            {
                continue;
            }

            var record = NtfsLogRecord.TryParse(lfs, out var error) ?? throw new JournalException(error);
            Analyze(record);
        }

        if (LastLsn == checkpoint && Log.ReadRecord(checkpoint) == null)
        {
            throw new JournalException($"The checkpoint record {checkpoint:X} is not in the log");
        }

        var redo = firstAfterCheckpoint;
        foreach (var page in DirtyPages)
        {
            if (page.OldestLsn != 0 && (redo == 0 || page.OldestLsn < redo))
            {
                redo = page.OldestLsn;
            }
        }

        foreach (var transaction in Transactions.Values)
        {
            if (transaction.FirstLsn != 0 && (redo == 0 || transaction.FirstLsn < redo))
            {
                redo = transaction.FirstLsn;
            }
        }

        RedoLsn = redo;
    }

    private void Analyze(NtfsLogRecord record)
    {
        var lfs = record.Lfs;
        if (lfs.TransactionId == 0)
        {
            throw new JournalException($"Log record {lfs.Lsn:X} has no transaction");
        }

        if (!Transactions.TryGetValue(lfs.TransactionId, out var transaction))
        {
            transaction = new TransactionEntry { State = TransactionState.Active, FirstLsn = lfs.Lsn };
            Transactions[lfs.TransactionId] = transaction;
        }

        transaction.PreviousLsn = lfs.Lsn;
        transaction.UndoNextLsn = record.UndoOperation == NtfsLogOperation.CompensationLogRecord ? lfs.ClientUndoNextLsn : lfs.Lsn;

        switch (record.RedoOperation)
        {
            case NtfsLogOperation.DeleteDirtyClusters:
                DeleteDirtyClusters(record);
                return;

            case NtfsLogOperation.OpenNonresidentAttribute:
                OpenAttribute(record);
                return;

            case NtfsLogOperation.HotFix:
                var hot = FindDirtyPage(record.TargetAttribute, record.TargetVcn);
                if (hot != null && record.Lcns.Length > 0 && hot.Lcns[record.TargetVcn - hot.Vcn] != 0)
                {
                    hot.Lcns[record.TargetVcn - hot.Vcn] = record.Lcns[0];
                }

                return;

            case NtfsLogOperation.EndTopLevelAction:
                transaction.PreviousLsn = lfs.Lsn;
                transaction.UndoNextLsn = lfs.ClientUndoNextLsn;
                return;

            case NtfsLogOperation.PrepareTransaction:
                transaction.State = TransactionState.Prepared;
                return;

            case NtfsLogOperation.CommitTransaction:
                transaction.State = TransactionState.Committed;
                return;

            case NtfsLogOperation.ForgetTransaction:
                Transactions.Remove(lfs.TransactionId);
                return;
        }

        var target = NtfsLogOperations.TargetOf(record.RedoOperation);
        if (target == NtfsLogTarget.None || record.Lcns.Length == 0)
        {
            return;
        }

        // Every record that changes a page (also one DiscUtils cannot apply: the redo pass refuses it then) marks
        // that page dirty from this LSN on, with the clusters the record names.
        var page = FindDirtyPage(record.TargetAttribute, record.TargetVcn);
        if (page == null)
        {
            page = new DirtyPageEntry
            {
                TargetAttribute = record.TargetAttribute,
                Vcn = record.TargetVcn & ~((long)ClustersPerPage - 1),
                OldestLsn = lfs.Lsn,
                Lcns = new long[ClustersPerPage],
            };
            DirtyPages.Add(page);
        }

        var first = record.TargetVcn - page.Vcn;
        if (first + record.Lcns.Length > page.Lcns.Length)
        {
            throw new JournalException($"Log record {lfs.Lsn:X} names clusters beyond its dirty page");
        }

        Array.Copy(record.Lcns, 0, page.Lcns, first, record.Lcns.Length);
    }

    private void DeleteDirtyClusters(NtfsLogRecord record)
    {
        var data = record.RedoData;
        for (var i = 0; i + 16 <= data.Count; i += 16)
        {
            var lcn = (long)U64(data.Array, data.Offset + i);
            var length = (long)U64(data.Array, data.Offset + i + 8);
            foreach (var page in DirtyPages)
            {
                for (var j = 0; j < page.Lcns.Length; j++)
                {
                    if (page.Lcns[j] >= lcn && page.Lcns[j] < lcn + length)
                    {
                        page.Lcns[j] = 0;
                    }
                }
            }
        }
    }

    private void OpenAttribute(NtfsLogRecord record)
    {
        var data = record.RedoData;
        var version = RestartRecord.MajorVersion;
        var size = version == 0 ? OpenAttributeEntry.SizeVersion0 : OpenAttributeEntry.SizeVersion1;
        if (data.Count < size - 8)
        {
            throw new JournalException($"Log record {record.Lsn:X} opens an attribute with too little data");
        }

        var buffer = new byte[size];
        Array.Copy(data.Array, data.Offset, buffer, 0, Math.Min(size, data.Count));
        var entry = OpenAttributeEntry.Parse(buffer, 0, version);
        var name = record.UndoData;
        if (name.Count > 0)
        {
            entry.Name = Encoding.Unicode.GetString(name.Array, name.Offset, name.Count);
        }

        OpenAttributes[record.TargetAttribute] = entry;
    }
}
