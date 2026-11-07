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
using static DiscUtils.Ntfs.Journal.JournalUtilities;

namespace DiscUtils.Ntfs.Journal;

/// <summary>
/// The byte-level meaning of every operation NTFS logs, applied to a page in its logical form. Each action checks
/// that the page looks the way the log record expects (an attribute or index entry starts exactly at the logged
/// offset, sizes stay inside the record) and throws otherwise, so a replay never writes a guess.
/// </summary>
internal static class NtfsLogActions
{
    // File record header
    private const int FrSequence = 0x10;
    private const int FrHardLinks = 0x12;
    private const int FrFirstAttribute = 0x14;
    private const int FrFlags = 0x16;
    private const int FrBytesInUse = 0x18;
    private const int FrNextAttributeId = 0x28;

    // Attribute header
    private const int AType = 0x00;
    private const int ALength = 0x04;
    private const int ANonResident = 0x08;
    private const int AId = 0x0E;
    private const int RValueLength = 0x10;
    private const int RValueOffset = 0x14;
    private const int RIndexed = 0x16;
    private const int NStartVcn = 0x10;
    private const int NLastVcn = 0x18;
    private const int NRunsOffset = 0x20;
    private const int NAllocated = 0x28;
    private const int NDataSize = 0x30;
    private const int NValidSize = 0x38;
    private const int NTotalSize = 0x40;

    private const uint AttributeEnd = 0xFFFFFFFF;
    private const uint AttributeIndexRoot = 0x90;

    // Index header (inside an INDEX_ROOT value at +0x10, inside an INDX buffer at +0x18)
    private const int IhEntriesOffset = 0x00;
    private const int IhUsed = 0x04;
    private const int IhTotal = 0x08;
    private const int IndxHeader = 0x18;

    // Index entry
    private const int IeLength = 0x08;
    private const int FileNameDuplicatedInfo = 0x18;
    private const int DuplicatedInfoLength = 0x38;

    /// <summary>
    /// Applies <paramref name="op"/> with <paramref name="data"/> to a file record.
    /// </summary>
    public static void ApplyToFileRecord(NtfsLogOperation op, NtfsLogRecord record, byte[] p, ArraySegment<byte> data)
    {
        var roff = record.RecordOffset;
        var aoff = record.AttributeOffset;
        var size = p.Length;
        switch (op)
        {
            case NtfsLogOperation.InitializeFileRecordSegment:
                // The record image; like Windows, the rest of the record keeps its old bytes.
                Require(roff + data.Count <= size, record, "the new file record does not fit");
                Copy(data, p, roff);
                return;

            case NtfsLogOperation.DeallocateFileRecordSegment:
                Put16(p, FrFlags, (ushort)(U16(p, FrFlags) & ~1));
                Put16(p, FrSequence, (ushort)(U16(p, FrSequence) + 1));
                return;

            case NtfsLogOperation.WriteEndOfFileRecordSegment:
                RequireAttributeAt(p, roff, record, allowEnd: true);
                Require(roff + data.Count <= size, record, "the end of the file record does not fit");
                Copy(data, p, roff);
                Put32(p, FrBytesInUse, (uint)Align8(roff + data.Count));
                return;

            case NtfsLogOperation.CreateAttribute:
                CreateAttribute(p, record, data);
                return;

            case NtfsLogOperation.DeleteAttribute:
                DeleteAttribute(p, record);
                return;

            case NtfsLogOperation.UpdateResidentValue:
                UpdateResidentValue(p, record, data);
                return;

            case NtfsLogOperation.UpdateMappingPairs:
                UpdateMappingPairs(p, record, data);
                return;

            case NtfsLogOperation.SetNewAttributeSizes:
                RequireAttributeAt(p, roff, record);
                Require(p[roff + ANonResident] != 0, record, "new sizes for a resident attribute");
                Require(data.Count >= 24, record, "the new sizes are too short");
                Put64(p, roff + NAllocated, U64(data.Array, data.Offset));
                Put64(p, roff + NValidSize, U64(data.Array, data.Offset + 8));
                Put64(p, roff + NDataSize, U64(data.Array, data.Offset + 16));
                if (data.Count >= 32 && U16(p, roff + NRunsOffset) >= NTotalSize + 8)
                {
                    Put64(p, roff + NTotalSize, U64(data.Array, data.Offset + 24));
                }

                return;

            case NtfsLogOperation.AddIndexEntryRoot:
                AddIndexEntryRoot(p, record, data);
                return;

            case NtfsLogOperation.DeleteIndexEntryRoot:
                DeleteIndexEntryRoot(p, record);
                return;

            case NtfsLogOperation.SetIndexEntryVcnRoot:
            {
                var entry = RequireRootEntry(p, record, out _);
                SetEntryVcn(p, entry, data, record);
                return;
            }

            case NtfsLogOperation.UpdateFileNameRoot:
            {
                var entry = RequireRootEntry(p, record, out _);
                UpdateFileName(p, entry, data, record);
                return;
            }

            case NtfsLogOperation.UpdateRecordDataRoot:
            {
                var entry = RequireRootEntry(p, record, out _);
                UpdateRecordData(p, entry, data, record);
                return;
            }

            case NtfsLogOperation.ZeroEndOfFileRecord:
                Require(roff + data.Count <= size, record, "the zeroed range does not fit");
                Array.Clear(p, roff, data.Count);
                return;

            default:
                throw Unsupported(op, record);
        }
    }

    /// <summary>
    /// Applies <paramref name="op"/> with <paramref name="data"/> to an index buffer.
    /// </summary>
    public static void ApplyToIndexBuffer(NtfsLogOperation op, NtfsLogRecord record, byte[] q, ArraySegment<byte> data)
    {
        var aoff = record.AttributeOffset;
        switch (op)
        {
            case NtfsLogOperation.AddIndexEntryAllocation:
            {
                RequireIndexBufferEntry(q, aoff, record);
                var used = (int)U32(q, IndxHeader + IhUsed);
                var total = (int)U32(q, IndxHeader + IhTotal);
                var length = EntryLength(data, record);
                Require(used + length <= total && IndxHeader + used + length <= q.Length, record, "the index buffer is full");
                var end = IndxHeader + used;
                Array.Copy(q, aoff, q, aoff + length, end - aoff);
                Array.Copy(data.Array, data.Offset, q, aoff, length);
                Put32(q, IndxHeader + IhUsed, (uint)(used + length));
                return;
            }

            case NtfsLogOperation.DeleteIndexEntryAllocation:
            {
                RequireIndexBufferEntry(q, aoff, record);
                var used = (int)U32(q, IndxHeader + IhUsed);
                var end = IndxHeader + used;
                var length = U16(q, aoff + IeLength);
                Require(length > 0 && aoff + length <= end, record, "the index entry runs past the used part");
                Array.Copy(q, aoff + length, q, aoff, end - aoff - length);
                Put32(q, IndxHeader + IhUsed, (uint)(used - length));
                return;
            }

            case NtfsLogOperation.WriteEndOfIndexBuffer:
            {
                RequireIndexBufferEntry(q, aoff, record);
                var total = (int)U32(q, IndxHeader + IhTotal);
                Require(aoff + data.Count <= IndxHeader + total && aoff + data.Count <= q.Length, record,
                    "the end of the index buffer does not fit");
                Put32(q, IndxHeader + IhUsed, (uint)(aoff - IndxHeader + data.Count));
                Copy(data, q, aoff);
                return;
            }

            case NtfsLogOperation.SetIndexEntryVcnAllocation:
                RequireIndexBufferEntry(q, aoff, record);
                SetEntryVcn(q, aoff, data, record);
                return;

            case NtfsLogOperation.UpdateFileNameAllocation:
                RequireIndexBufferEntry(q, aoff, record);
                UpdateFileName(q, aoff, data, record);
                return;

            case NtfsLogOperation.UpdateRecordDataAllocation:
                RequireIndexBufferEntry(q, aoff, record);
                UpdateRecordData(q, aoff, data, record);
                return;

            default:
                throw Unsupported(op, record);
        }
    }

    /// <summary>Sets (<paramref name="set"/>) or clears a range of bits.</summary>
    public static void ChangeBits(byte[] bytes, int firstBit, int count, bool set)
    {
        for (var bit = firstBit; bit < firstBit + count; bit++)
        {
            if (set)
            {
                bytes[bit >> 3] |= (byte)(1 << (bit & 7));
            }
            else
            {
                bytes[bit >> 3] &= (byte)~(1 << (bit & 7));
            }
        }
    }

    public static JournalException Unsupported(NtfsLogOperation op, NtfsLogRecord record) =>
        new($"Log record {record.Lsn:X} needs operation {op}, which DiscUtils cannot apply");

    private static void CreateAttribute(byte[] p, NtfsLogRecord record, ArraySegment<byte> data)
    {
        var roff = record.RecordOffset;
        RequireAttributeAt(p, roff, record, allowEnd: true);
        Require(data.Count >= 0x18, record, "the new attribute is too short");
        var length = (int)U32(data.Array, data.Offset + ALength);
        var used = (int)U32(p, FrBytesInUse);
        Require(length >= 0x18 && (length & 7) == 0 && length <= data.Count, record, "the new attribute has a bad length");
        Require(used + length <= p.Length, record, "the new attribute does not fit in the file record");

        Array.Copy(p, roff, p, roff + length, used - roff);
        Array.Copy(data.Array, data.Offset, p, roff, length);
        Put32(p, FrBytesInUse, (uint)(used + length));
        var id = U16(p, roff + AId);
        if (U16(p, FrNextAttributeId) <= id)
        {
            Put16(p, FrNextAttributeId, (ushort)(id + 1));
        }

        if (IsIndexed(p, roff))
        {
            Put16(p, FrHardLinks, (ushort)(U16(p, FrHardLinks) + 1));
        }
    }

    private static void DeleteAttribute(byte[] p, NtfsLogRecord record)
    {
        var roff = record.RecordOffset;
        RequireAttributeAt(p, roff, record);
        var used = (int)U32(p, FrBytesInUse);
        var length = (int)U32(p, roff + ALength);
        Require(roff + length <= used, record, "the attribute runs past the used part of the file record");
        if (IsIndexed(p, roff))
        {
            Put16(p, FrHardLinks, (ushort)(U16(p, FrHardLinks) - 1));
        }

        // Only the used part moves; the bytes left behind past the new end are unused (Windows logs a
        // ZeroEndOfFileRecord when it wants them cleared).
        Array.Copy(p, roff + length, p, roff, used - roff - length);
        Put32(p, FrBytesInUse, (uint)(used - length));
    }

    private static void UpdateResidentValue(byte[] p, NtfsLogRecord record, ArraySegment<byte> data)
    {
        var roff = record.RecordOffset;
        var aoff = record.AttributeOffset;
        RequireAttributeAt(p, roff, record);
        Require(p[roff + ANonResident] == 0, record, "a resident value update of a non-resident attribute");
        var length = (int)U32(p, roff + ALength);
        var valueOffset = U16(p, roff + RValueOffset);
        Require(aoff >= valueOffset, record, "the value update starts inside the attribute header");
        if (record.RedoLength == record.UndoLength)
        {
            // Same size before and after: an in-place overwrite.
            Require(aoff + data.Count <= length, record, "the value update runs past the attribute");
            Copy(data, p, roff + aoff);
            return;
        }

        // Otherwise the value now ends where the data ends.
        var newLength = Align8(aoff + data.Count);
        ResizeAttribute(p, roff, length, newLength, record);
        Put32(p, roff + RValueLength, (uint)(aoff + data.Count - valueOffset));
        Copy(data, p, roff + aoff);
    }

    private static void UpdateMappingPairs(byte[] p, NtfsLogRecord record, ArraySegment<byte> data)
    {
        var roff = record.RecordOffset;
        var aoff = record.AttributeOffset;
        RequireAttributeAt(p, roff, record);
        Require(p[roff + ANonResident] != 0, record, "a run list update of a resident attribute");
        var length = (int)U32(p, roff + ALength);
        var runsOffset = U16(p, roff + NRunsOffset);
        Require(aoff >= runsOffset && aoff <= length, record, "the run list update is outside the run list");
        var newLength = Align8(aoff + data.Count);
        ResizeAttribute(p, roff, length, newLength, record);
        Copy(data, p, roff + aoff);

        // The attribute's last VCN follows from its runs.
        var position = roff + runsOffset;
        var end = roff + newLength;
        var vcn = (long)U64(p, roff + NStartVcn);
        while (position < end && p[position] != 0)
        {
            var header = p[position];
            var lengthSize = header & 0x0F;
            var offsetSize = header >> 4;
            Require(lengthSize is > 0 and <= 8 && offsetSize <= 8 && position + 1 + lengthSize + offsetSize <= end, record,
                "the new run list is damaged");
            long runLength = 0;
            for (var i = lengthSize - 1; i >= 0; i--)
            {
                runLength = (runLength << 8) | p[position + 1 + i];
            }

            vcn += runLength;
            position += 1 + lengthSize + offsetSize;
        }

        Put64(p, roff + NLastVcn, (ulong)(vcn - 1));
    }

    private static void AddIndexEntryRoot(byte[] p, NtfsLogRecord record, ArraySegment<byte> data)
    {
        var roff = record.RecordOffset;
        var entry = RequireRootEntry(p, record, out var header);
        var length = EntryLength(data, record);
        var used = (int)U32(p, FrBytesInUse);
        Require(used + length <= p.Length, record, "the index entry does not fit in the file record");

        // Insert the entry, which moves the following entries and the following attributes up.
        Array.Copy(p, entry, p, entry + length, used - entry);
        Array.Copy(data.Array, data.Offset, p, entry, length);
        Put32(p, FrBytesInUse, (uint)(used + length));
        Put32(p, roff + ALength, U32(p, roff + ALength) + (uint)length);
        Put32(p, roff + RValueLength, U32(p, roff + RValueLength) + (uint)length);
        Put32(p, header + IhUsed, U32(p, header + IhUsed) + (uint)length);
        Put32(p, header + IhTotal, U32(p, header + IhTotal) + (uint)length);
    }

    private static void DeleteIndexEntryRoot(byte[] p, NtfsLogRecord record)
    {
        var roff = record.RecordOffset;
        var entry = RequireRootEntry(p, record, out var header);
        var length = U16(p, entry + IeLength);
        var entriesEnd = header + (int)U32(p, header + IhUsed);
        Require(length > 0 && entry + length <= entriesEnd, record, "the index entry runs past the used part");
        var used = (int)U32(p, FrBytesInUse);
        Array.Copy(p, entry + length, p, entry, used - entry - length);
        Put32(p, FrBytesInUse, (uint)(used - length));
        Put32(p, roff + ALength, U32(p, roff + ALength) - length);
        Put32(p, roff + RValueLength, U32(p, roff + RValueLength) - length);
        Put32(p, header + IhUsed, U32(p, header + IhUsed) - length);
        Put32(p, header + IhTotal, U32(p, header + IhTotal) - length);
    }

    private static void SetEntryVcn(byte[] page, int entry, ArraySegment<byte> data, NtfsLogRecord record)
    {
        var length = U16(page, entry + IeLength);
        Require(data.Count >= 8 && length >= 0x18 && entry + length <= page.Length, record, "the index entry has no child VCN");
        Array.Copy(data.Array, data.Offset, page, entry + length - 8, 8);
    }

    private static void UpdateFileName(byte[] page, int entry, ArraySegment<byte> data, NtfsLogRecord record)
    {
        var length = U16(page, entry + IeLength);
        var count = Math.Min(data.Count, DuplicatedInfoLength);
        Require(length >= FileNameDuplicatedInfo + DuplicatedInfoLength && entry + length <= page.Length, record,
            "the index entry has no file name");
        Array.Copy(data.Array, data.Offset, page, entry + FileNameDuplicatedInfo, count);
    }

    private static void UpdateRecordData(byte[] page, int entry, ArraySegment<byte> data, NtfsLogRecord record)
    {
        var length = U16(page, entry + IeLength);
        var dataOffset = U16(page, entry);
        Require(dataOffset + data.Count <= length && entry + length <= page.Length, record,
            "the index entry's data does not fit");
        Copy(data, page, entry + dataOffset);
    }

    private static void ResizeAttribute(byte[] p, int roff, int oldLength, int newLength, NtfsLogRecord record)
    {
        if (newLength == oldLength)
        {
            return;
        }

        // Like Windows, only the following attributes move; padding keeps whatever bytes were there.
        var used = (int)U32(p, FrBytesInUse);
        Require(used - oldLength + newLength <= p.Length, record, "the attribute does not fit in the file record");
        Array.Copy(p, roff + oldLength, p, roff + newLength, used - roff - oldLength);
        Put32(p, roff + ALength, (uint)newLength);
        Put32(p, FrBytesInUse, (uint)(used - oldLength + newLength));
    }

    /// <summary>Checks that an attribute (or, with <paramref name="allowEnd"/>, the end marker) starts at <paramref name="roff"/>.</summary>
    private static void RequireAttributeAt(byte[] p, int roff, NtfsLogRecord record, bool allowEnd = false)
    {
        var used = (int)U32(p, FrBytesInUse);
        var position = (int)U16(p, FrFirstAttribute);
        Require(used <= p.Length && position >= 0x30 - 0x10 && position < used, record, "the file record header is damaged");
        while (position < roff)
        {
            Require(position + 8 <= used, record, "the attribute list runs past the used part");
            if (U32(p, position + AType) == AttributeEnd)
            {
                break;
            }

            var length = (int)U32(p, position + ALength);
            Require(length >= 0x18 && position + length <= used, record, "an attribute has a bad length");
            position += length;
        }

        Require(position == roff && roff + 4 <= used, record, $"no attribute starts at offset {roff:X} of the file record");
        if (U32(p, roff + AType) == AttributeEnd)
        {
            Require(allowEnd, record, $"offset {roff:X} is the end of the file record, not an attribute");
            return;
        }

        var attributeLength = (int)U32(p, roff + ALength);
        Require(attributeLength >= 0x18 && roff + attributeLength <= used, record, "the attribute has a bad length");
    }

    /// <summary>
    /// Checks that an index entry of the INDEX_ROOT at the logged attribute offset starts at the logged entry
    /// offset; returns the entry's offset in the record and the index header's.
    /// </summary>
    private static int RequireRootEntry(byte[] p, NtfsLogRecord record, out int header)
    {
        var roff = record.RecordOffset;
        RequireAttributeAt(p, roff, record);
        Require(U32(p, roff + AType) == AttributeIndexRoot && p[roff + ANonResident] == 0, record,
            "the attribute is not an index root");
        var attributeLength = (int)U32(p, roff + ALength);
        header = roff + U16(p, roff + RValueOffset) + 0x10;
        var entries = header + (int)U32(p, header + IhEntriesOffset);
        var end = header + (int)U32(p, header + IhUsed);
        Require(entries >= header + 0x10 && end <= roff + attributeLength, record, "the index root header is damaged");
        var target = roff + record.AttributeOffset;
        RequireEntryChain(p, entries, end, target, record);
        return target;
    }

    private static void RequireIndexBufferEntry(byte[] q, int aoff, NtfsLogRecord record)
    {
        var entries = IndxHeader + (int)U32(q, IndxHeader + IhEntriesOffset);
        var end = IndxHeader + (int)U32(q, IndxHeader + IhUsed);
        Require(entries >= IndxHeader + 0x10 && end <= q.Length, record, "the index buffer header is damaged");
        RequireEntryChain(q, entries, end, aoff, record);
    }

    /// <summary>The target must be reachable by walking the entries; it may be the end of the used part (append).</summary>
    private static void RequireEntryChain(byte[] page, int position, int end, int target, NtfsLogRecord record)
    {
        while (position < target && position < end)
        {
            var length = U16(page, position + IeLength);
            Require(length >= 0x10, record, "an index entry has a bad length");
            position += length;
        }

        Require(position == target && target <= end, record, $"no index entry starts at offset {target:X}");
    }

    private static int EntryLength(ArraySegment<byte> data, NtfsLogRecord record)
    {
        Require(data.Count >= 0x10, record, "the index entry is too short");
        int length = U16(data.Array, data.Offset + IeLength);
        Require(length >= 0x10 && length <= data.Count && (length & 7) == 0, record, "the index entry has a bad length");
        return length;
    }

    private static bool IsIndexed(byte[] p, int roff) => p[roff + ANonResident] == 0 && (p[roff + RIndexed] & 1) != 0;

    private static void Copy(ArraySegment<byte> data, byte[] target, int offset) =>
        Array.Copy(data.Array, data.Offset, target, offset, data.Count);

    private static void Require(bool condition, NtfsLogRecord record, string what)
    {
        if (!condition)
        {
            throw new JournalException($"Log record {record.Lsn:X} ({record.RedoOperation}/{record.UndoOperation}) does not fit the volume: {what}");
        }
    }
}
