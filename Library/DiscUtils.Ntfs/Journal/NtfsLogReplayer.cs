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
using static DiscUtils.Ntfs.Journal.JournalUtilities;

namespace DiscUtils.Ntfs.Journal;

/// <summary>
/// The redo and undo passes of NTFS recovery, applied to an in-memory set of pages.
/// </summary>
/// <remarks>
/// Redo repeats history: every change after a page's oldest dirty LSN is applied again when the page (file
/// record or index buffer) carries an older LSN than the record; pages without an LSN (bitmaps, plain data) are
/// replayed unconditionally, which is safe because those operations set bytes and bits to fixed values. Undo then
/// rolls back the transactions that were still running, newest change first, following the undo-next chain.
/// Windows writes a compensation record for every undone change and stamps the page with its LSN; this replay
/// writes no log records, so it stamps undone pages with an LSN just past the end of the log instead. That keeps
/// a second replay (after an interruption) from undoing a change twice, and it is below every LSN Windows will
/// hand out after the log is reset.
/// </remarks>
internal sealed class NtfsLogReplayer
{
    private readonly NtfsLogAnalysis _analysis;
    private readonly JournalVolumeLayout _layout;
    private readonly NtfsLogPages _pages;
    private readonly ulong _undoStamp;

    public NtfsLogReplayer(Stream volume, JournalVolumeLayout layout, NtfsLogAnalysis analysis)
    {
        _analysis = analysis;
        _layout = layout;
        _pages = new NtfsLogPages(volume, layout);
        _undoStamp = analysis.LastLsn + 1;
    }

    public NtfsLogPages Pages => _pages;

    /// <summary>Log records whose redo action changed a page.</summary>
    public int RedoneRecords { get; private set; }

    /// <summary>Log records whose undo action changed a page.</summary>
    public int UndoneRecords { get; private set; }

    public int UndoneTransactions { get; private set; }

    /// <summary>The MFT record numbers that changed (as far as the log tells, for reporting).</summary>
    public SortedSet<long> ChangedFileRecords { get; } = [];

    public void Redo()
    {
        if (_analysis.DirtyPages.Count == 0 || _analysis.RedoLsn == 0)
        {
            return;
        }

        foreach (var lfs in _analysis.Log.ReadForward(_analysis.RedoLsn))
        {
            if (lfs.RecordType != LfsRecord.TypeClientRecord)
            {
                continue;
            }

            var record = NtfsLogRecord.TryParse(lfs, out var error) ?? throw new JournalException(error);
            if (record.Lcns.Length == 0)
            {
                continue;
            }

            var target = NtfsLogOperations.TargetOf(record.RedoOperation);
            if (target == NtfsLogTarget.None)
            {
                continue;
            }

            var page = _analysis.FindDirtyPage(record.TargetAttribute, record.TargetVcn);
            if (page == null || record.Lsn < _analysis.OldestLsnFor(record.TargetAttribute, record.TargetVcn))
            {
                continue;
            }

            // The dirty page table has the current clusters (DeleteDirtyClusters zeroes freed ones).
            var first = (int)(record.TargetVcn - page.Vcn);
            var lcns = new long[page.Lcns.Length - first];
            Array.Copy(page.Lcns, first, lcns, 0, lcns.Length);
            if (Apply(record, record.RedoOperation, record.RedoData, lcns, redo: true))
            {
                RedoneRecords++;
            }
        }
    }

    public void Undo()
    {
        foreach (var loser in _analysis.Losers)
        {
            // Counted only if it changed pages: the checkpoint's table dumps also leave an "open" transaction.
            var changesPages = false;
            var lsn = loser.Value.UndoNextLsn;
            var seen = new HashSet<ulong>();
            while (lsn != 0)
            {
                if (!seen.Add(lsn))
                {
                    throw new JournalException($"The undo chain of transaction {loser.Key:X} loops at {lsn:X}");
                }

                var lfs = _analysis.Log.ReadRecord(lsn)
                          ?? throw new JournalException($"Record {lsn:X} to undo is not in the log");
                var record = NtfsLogRecord.TryParse(lfs, out var error) ?? throw new JournalException(error);
                if (lfs.TransactionId != loser.Key)
                {
                    throw new JournalException($"Record {lsn:X} in the undo chain of transaction {loser.Key:X} belongs to {lfs.TransactionId:X}");
                }

                var target = NtfsLogOperations.TargetOf(record.UndoOperation);
                if (target != NtfsLogTarget.None && record.Lcns.Length > 0)
                {
                    changesPages = true;
                    var lcns = (long[])record.Lcns.Clone();

                    // A cluster the dirty page table says was freed holds nothing of this page any more.
                    var page = _analysis.FindDirtyPage(record.TargetAttribute, record.TargetVcn);
                    if (page != null)
                    {
                        for (var i = 0; i < lcns.Length && record.TargetVcn - page.Vcn + i < page.Lcns.Length; i++)
                        {
                            if (page.Lcns[record.TargetVcn - page.Vcn + i] == 0)
                            {
                                lcns[i] = 0;
                            }
                        }
                    }

                    if (Apply(record, record.UndoOperation, record.UndoData, lcns, redo: false))
                    {
                        UndoneRecords++;
                    }
                }

                // A compensation record points past the changes that were already undone.
                lsn = lfs.ClientUndoNextLsn;
            }

            if (changesPages)
            {
                UndoneTransactions++;
            }
        }
    }

    /// <summary>
    /// Applies one action; <c>false</c> if it was not needed (the page is newer, or its clusters were freed).
    /// </summary>
    private bool Apply(NtfsLogRecord record, NtfsLogOperation op, ArraySegment<byte> data, long[] lcns, bool redo)
    {
        var target = NtfsLogOperations.TargetOf(op);
        var start = record.ClusterBlockOffset * 512;
        switch (target)
        {
            case NtfsLogTarget.FileRecord:
            {
                var length = _layout.MftRecordSize;
                if (!Allocated(lcns, start, length))
                {
                    return false;
                }

                var page = _pages.GetLogical(PageKind.FileRecord, lcns, start, length);
                if (!PageNeedsAction(page, record, op, redo))
                {
                    return false;
                }

                if (!page.Valid)
                {
                    // A record that was never written can be rebuilt (InitializeFileRecordSegment) or freed again
                    // (DeallocateFileRecordSegment, the compensation of a creation that never reached the disk;
                    // Windows then only changes the header fields). Anything else needs a real record.
                    if (op == NtfsLogOperation.InitializeFileRecordSegment)
                    {
                        Array.Clear(page.Data, 0, page.Data.Length);
                    }
                    else if (op != NtfsLogOperation.DeallocateFileRecordSegment || page.Signature == SignatureFile)
                    {
                        throw new JournalException($"Log record {record.Lsn:X} changes file record at cluster {lcns[start / _pages.ClusterSize]}, which is damaged");
                    }
                }

                NtfsLogActions.ApplyToFileRecord(op, record, page.Data, data);
                Finish(page, record, redo);
                ChangedFileRecords.Add(((record.TargetVcn * _layout.BytesPerCluster) + start) / _layout.MftRecordSize);
                return true;
            }

            case NtfsLogTarget.IndexBuffer:
            {
                var length = IndexBufferSize(record);
                start += record.RecordOffset;
                if (!Allocated(lcns, start, length))
                {
                    return false;
                }

                var page = _pages.GetLogical(PageKind.IndexBuffer, lcns, start, length);
                if (!PageNeedsAction(page, record, op, redo))
                {
                    return false;
                }

                if (!page.Valid)
                {
                    throw new JournalException($"Log record {record.Lsn:X} changes an index buffer at cluster {lcns[start / _pages.ClusterSize]}, which is damaged");
                }

                NtfsLogActions.ApplyToIndexBuffer(op, record, page.Data, data);
                Finish(page, record, redo);
                return true;
            }

            case NtfsLogTarget.NonResident:
                return ApplyNonResident(record, op, data, lcns, start);

            default:
                throw NtfsLogActions.Unsupported(op, record);
        }
    }

    private bool ApplyNonResident(NtfsLogRecord record, NtfsLogOperation op, ArraySegment<byte> data, long[] lcns, int start)
    {
        if (!_analysis.OpenAttributes.TryGetValue(record.TargetAttribute, out var attribute))
        {
            throw new JournalException($"Log record {record.Lsn:X} changes attribute {record.TargetAttribute:X}, which is not open");
        }

        if (op == NtfsLogOperation.UpdateNonresidentValue)
        {
            if (attribute.IsIndexAllocation)
            {
                // Writes into an index buffer (often a whole new one); the bytes are in logical form.
                var length = IndexBufferSize(record);
                if (!Allocated(lcns, start, length))
                {
                    return false;
                }

                var page = _pages.GetLogical(PageKind.IndexBuffer, lcns, start, length);
                if (record.RecordOffset + data.Count > length)
                {
                    throw new JournalException($"Log record {record.Lsn:X} writes past its index buffer");
                }

                Array.Copy(data.Array, data.Offset, page.Data, record.RecordOffset, data.Count);
                page.Valid = page.Signature == SignatureIndx && HasFixupLayout(page.Data, 0, page.Data.Length);
                page.Dirty = true;
                return true;
            }

            var offset = start + record.RecordOffset;
            if (!Allocated(lcns, offset, Math.Max(1, data.Count)))
            {
                return false;
            }

            if (data.Count == 0)
            {
                return false;
            }

            var current = new byte[data.Count];
            _pages.ReadRaw(lcns, offset, current, 0, current.Length);
            if (current.AsSpan().SequenceEqual(data.AsSpan()))
            {
                return false;
            }

            _pages.WriteRaw(lcns, offset, data.Array, data.Offset, data.Count);
            return true;
        }

        // Set or clear a run of bits; the record's data is the first bit and the count.
        if (data.Count < 8)
        {
            throw new JournalException($"Log record {record.Lsn:X} has a bit range that is too short");
        }

        var firstBit = (long)U32(data.Array, data.Offset);
        var bits = (long)U32(data.Array, data.Offset + 4);
        var byteStart = start + record.RecordOffset + (int)(firstBit >> 3);
        var byteCount = (int)(((firstBit & 7) + bits + 7) >> 3);
        if (bits == 0)
        {
            return false;
        }

        if (!Allocated(lcns, byteStart, byteCount))
        {
            return false;
        }

        var bytes = new byte[byteCount];
        _pages.ReadRaw(lcns, byteStart, bytes, 0, byteCount);
        var before = (byte[])bytes.Clone();
        NtfsLogActions.ChangeBits(bytes, (int)(firstBit & 7), (int)bits, op == NtfsLogOperation.SetBitsInNonresidentBitMap);
        if (before.AsSpan().SequenceEqual(bytes))
        {
            return false;
        }

        _pages.WriteRaw(lcns, byteStart, bytes, 0, byteCount);
        return true;
    }

    /// <summary>
    /// Whether a file record or index buffer needs the action: redo only when the page is older than the record;
    /// undo unless this replay already undid it (the stamp is past the end of the log).
    /// </summary>
    private bool PageNeedsAction(LogicalPage page, NtfsLogRecord record, NtfsLogOperation op, bool redo)
    {
        if (page.Signature == SignatureBaad)
        {
            throw new JournalException($"Log record {record.Lsn:X} changes a page chkdsk marked bad");
        }

        if (page.Signature == SignatureHole)
        {
            return false;
        }

        // A torn record (signature, but the update sequence array does not match) has no trustworthy LSN.
        if (!page.Valid && (page.Signature == SignatureFile || page.Signature == SignatureIndx))
        {
            return true;
        }

        // Otherwise the LSN is at offset 8, also in a record that was never written (0): Windows checks it there.
        return redo ? page.Lsn < record.Lsn : page.Lsn < _undoStamp;
    }

    private void Finish(LogicalPage page, NtfsLogRecord record, bool redo)
    {
        // An undo can turn a page back into "never written" (no signature); it is then written as it is.
        var expected = page.Kind == PageKind.FileRecord ? SignatureFile : SignatureIndx;
        page.Valid = page.Signature == expected && HasFixupLayout(page.Data, 0, page.Data.Length);
        page.Dirty = true;

        // Redo stamps the record's LSN, as Windows does even in a page that is not a valid record yet (it is then
        // written without an update sequence array). Undo stamps only real records.
        if (redo)
        {
            page.Lsn = record.Lsn;
        }
        else if (page.Valid)
        {
            page.Lsn = _undoStamp;
        }
    }

    private int IndexBufferSize(NtfsLogRecord record)
    {
        if (_analysis.OpenAttributes.TryGetValue(record.TargetAttribute, out var attribute) && attribute.BytesPerIndexBuffer != 0)
        {
            return (int)attribute.BytesPerIndexBuffer;
        }

        return _layout.IndexBufferSize;
    }

    /// <summary>
    /// <c>false</c> if the first cluster of the range was freed (nothing to do); a range that is only partly freed
    /// cannot be replayed faithfully.
    /// </summary>
    private bool Allocated(long[] lcns, int start, int length)
    {
        var first = start / _pages.ClusterSize;
        var last = (start + length - 1) / _pages.ClusterSize;
        if (last >= lcns.Length)
        {
            throw new JournalException("A log record addresses bytes beyond the clusters it names");
        }

        if (lcns[first] == 0)
        {
            return false;
        }

        for (var i = first + 1; i <= last; i++)
        {
            if (lcns[i] == 0)
            {
                throw new JournalException("A log record changes a page whose clusters were partly freed");
            }
        }

        return true;
    }
}
