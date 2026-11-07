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
/// follows each active transaction's undo-next chain backwards. Undo of different transactions is supported
/// only for disjoint clusters. We do not write compensation records or persist changes that require undo.
/// </remarks>
internal sealed class NtfsLogReplayer
{
    private readonly NtfsLogAnalysis _analysis;
    private readonly JournalVolumeLayout _layout;
    private readonly NtfsLogPages _pages;

    public NtfsLogReplayer(Stream volume, JournalVolumeLayout layout, NtfsLogAnalysis analysis)
    {
        _analysis = analysis;
        _layout = layout;
        _pages = new NtfsLogPages(volume, layout);
    }

    public NtfsLogPages Pages => _pages;

    /// <summary>Log records whose redo action changed a page.</summary>
    public int RedoneRecords { get; private set; }

    /// <summary>Log records whose undo action changed a page.</summary>
    public int UndoneRecords { get; private set; }

    public int UndoneTransactions => _pages.UndoTransactionCount;

    /// <summary>The MFT record numbers that changed (as far as the log tells, for reporting).</summary>
    public SortedSet<long> ChangedFileRecords { get; } = [];

    public void Redo()
    {
        if (_analysis.RedoLsn == 0)
        {
            return;
        }

        var foundStart = false;
        foreach (var lfs in _analysis.Log.ReadForward(_analysis.RedoLsn))
        {
            foundStart = true;
            if (!_analysis.Log.IsForNtfsClient(lfs) || lfs.RecordType != LfsRecord.TypeClientRecord)
            {
                continue;
            }

            var record = NtfsLogRecord.TryParse(lfs, out var error) ?? throw new JournalException(error);
            var target = NtfsLogOperations.TargetOf(record.RedoOperation);
            if (target == NtfsLogTarget.None)
            {
                continue;
            }

            if (record.Lcns.Length == 0)
            {
                if (record.UndoOperation == NtfsLogOperation.CompensationLogRecord)
                {
                    throw new JournalException($"Compensation record {record.Lsn:X} changes metadata without a cluster mapping");
                }

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

        if (!foundStart && _analysis.DirtyPages.Count != 0)
        {
            throw new JournalException($"The redo start record {_analysis.RedoLsn:X} is not in the log");
        }
    }

    public void Undo()
    {
        foreach (var loser in _analysis.Losers)
        {
            var lsn = loser.Value.UndoNextLsn;
            while (lsn != 0)
            {
                var lfs = _analysis.Log.ReadRecord(lsn)
                          ?? throw new JournalException($"Record {lsn:X} to undo is not in the log");
                _analysis.Log.RequireNtfsRecord(lfs, "undo chain record", LfsRecord.TypeClientRecord);

                if (lfs.TransactionId != loser.Key)
                {
                    throw new JournalException($"Record {lsn:X} in the undo chain of transaction {loser.Key:X} belongs to {lfs.TransactionId:X}");
                }

                if (lsn < loser.Value.FirstLsn || lsn > loser.Value.PreviousLsn || lsn > _analysis.LastLsn)
                {
                    throw new JournalException($"Record {lsn:X} is outside the undo history of transaction {loser.Key:X}");
                }

                var next = lfs.ClientUndoNextLsn;
                if (next != 0 && next >= lsn)
                {
                    throw new JournalException($"The undo chain of transaction {loser.Key:X} loops or points forward at {lsn:X}");
                }

                var record = NtfsLogRecord.TryParse(lfs, out var error) ?? throw new JournalException(error);
                var target = NtfsLogOperations.TargetOf(record.UndoOperation);
                if (target != NtfsLogTarget.None)
                {
                    if (record.Lcns.Length == 0)
                    {
                        throw new JournalException($"Record {lsn:X} needs metadata undo without a cluster mapping");
                    }

                    var lcns = (long[])record.Lcns.Clone();

                    // A cluster the dirty page table says was freed holds nothing of this page any more.
                    var page = _analysis.FindDirtyPage(record.TargetAttribute, record.TargetVcn);
                    if (page != null)
                    {
                        for (var i = 0; i < lcns.Length && record.TargetVcn - page.Vcn + i < page.Lcns.Length; i++)
                        {
                            var current = page.Lcns[record.TargetVcn - page.Vcn + i];
                            if (current == 0)
                            {
                                lcns[i] = 0;
                            }
                            else if (current != lcns[i])
                            {
                                throw new JournalException($"Record {lsn:X} has a remapped cluster in its undo target");
                            }
                        }
                    }

                    if (Apply(record, record.UndoOperation, record.UndoData, lcns, redo: false))
                    {
                        UndoneRecords++;
                    }
                }

                // A compensation record points past the changes that were already undone.
                lsn = next;
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
        var undoTransaction = redo ? 0 : record.Lfs.TransactionId;
        switch (target)
        {
            case NtfsLogTarget.FileRecord:
            {
                if (record.TargetVcn < 0 || record.TargetVcn > (long.MaxValue - start) / _layout.BytesPerCluster)
                {
                    throw new JournalException($"Log record {record.Lsn:X} has an invalid file record VCN");
                }

                var length = _layout.MftRecordSize;
                if (!Allocated(lcns, start, length))
                {
                    return false;
                }

                var page = _pages.GetLogical(PageKind.FileRecord, lcns, start, length, undoTransaction);
                if (!PageNeedsAction(page, record, redo))
                {
                    return false;
                }

                var torn = !page.Valid && page.Signature == SignatureFile;
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

                var lsn = redo ? record.Lsn : page.Lsn;
                NtfsLogActions.ApplyToFileRecord(op, record, page.Data, data);
                Finish(page, lsn, stampInvalid: redo);
                if (torn)
                {
                    var used = U32(page.Data, 0x18);
                    var firstAttribute = U16(page.Data, 0x14);
                    if (!page.Valid || record.RecordOffset != 0 || firstAttribute < 0x20 || used < firstAttribute + 4 || data.Count < used)
                    {
                        throw new JournalException($"Log record {record.Lsn:X} does not supply a complete replacement for the torn file record");
                    }
                }

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

                var page = _pages.GetLogical(PageKind.IndexBuffer, lcns, start, length, undoTransaction);
                if (!PageNeedsAction(page, record, redo))
                {
                    return false;
                }

                if (!page.Valid)
                {
                    throw new JournalException($"Log record {record.Lsn:X} changes an index buffer at cluster {lcns[start / _pages.ClusterSize]}, which is damaged");
                }

                var lsn = redo ? record.Lsn : page.Lsn;
                NtfsLogActions.ApplyToIndexBuffer(op, record, page.Data, data);
                Finish(page, lsn, stampInvalid: redo);
                return true;
            }

            case NtfsLogTarget.NonResident:
                return ApplyNonResident(record, op, data, lcns, start, redo, undoTransaction);

            default:
                throw NtfsLogActions.Unsupported(op, record);
        }
    }

    private bool ApplyNonResident(NtfsLogRecord record, NtfsLogOperation op, ArraySegment<byte> data, long[] lcns, int start,
        bool redo, uint undoTransaction)
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

                var page = _pages.GetLogical(PageKind.IndexBuffer, lcns, start, length, undoTransaction);
                if (record.RecordOffset + data.Count > length)
                {
                    throw new JournalException($"Log record {record.Lsn:X} writes past its index buffer");
                }

                if ((page.Signature == SignatureIndx || page.Signature == SignatureBaad || page.Signature == SignatureHole) &&
                    !PageNeedsAction(page, record, redo))
                {
                    return false;
                }

                var torn = !page.Valid && page.Signature == SignatureIndx;
                if (torn && (record.RecordOffset != 0 || data.Count != length))
                {
                    throw new JournalException($"Log record {record.Lsn:X} only partially replaces a torn index buffer");
                }

                var lsn = redo ? record.Lsn : page.Lsn;
                Array.Copy(data.Array, data.Offset, page.Data, record.RecordOffset, data.Count);
                Finish(page, lsn, stampInvalid: false);
                if (torn && !page.Valid)
                {
                    throw new JournalException($"Log record {record.Lsn:X} supplies an invalid replacement for the torn index buffer");
                }

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

            var current = _pages.ReadRaw(lcns, offset, data.Count, undoTransaction);
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

        if (bits > int.MaxValue - (firstBit & 7))
        {
            throw new JournalException($"Log record {record.Lsn:X} has an oversized bit range");
        }

        if (!Allocated(lcns, byteStart, byteCount))
        {
            return false;
        }

        var bytes = _pages.ReadRaw(lcns, byteStart, byteCount, undoTransaction);
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
    /// Redo checks the page LSN. Undo checks that the page history is plausible, then follows the chain without
    /// using a page stamp as an undo cursor. More than one inverse operation can target the same page.
    /// </summary>
    private bool PageNeedsAction(LogicalPage page, NtfsLogRecord record, bool redo)
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

        if (redo)
        {
            return page.Lsn < record.Lsn;
        }

        if (page.Valid && (page.Lsn < record.Lsn || page.Lsn > _analysis.LastLsn))
        {
            throw new JournalException($"Page LSN {page.Lsn:X} cannot establish the undo history for record {record.Lsn:X}");
        }

        return true;
    }

    private static void Finish(LogicalPage page, ulong lsn, bool stampInvalid)
    {
        // An undo can turn a page back into "never written" (no signature); it is then written as it is.
        var expected = page.Kind == PageKind.FileRecord ? SignatureFile : SignatureIndx;
        page.Valid = page.Signature == expected && HasFixupLayout(page.Data, 0, page.Data.Length);
        page.Dirty = true;

        // In-memory undo retains the page's historical LSN. It does not create a new log record.
        if (stampInvalid || page.Valid)
        {
            page.Lsn = lsn;
        }
    }

    private int IndexBufferSize(NtfsLogRecord record)
    {
        if (_analysis.OpenAttributes.TryGetValue(record.TargetAttribute, out var attribute) && attribute.BytesPerIndexBuffer != 0)
        {
            if (!JournalVolumeLayout.IsRecordSize(attribute.BytesPerIndexBuffer))
            {
                throw new JournalException($"Log record {record.Lsn:X} has an invalid index buffer size");
            }

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
        var last = (start + (long)length - 1) / _pages.ClusterSize;
        if (start < 0 || length <= 0 || last >= lcns.Length)
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
