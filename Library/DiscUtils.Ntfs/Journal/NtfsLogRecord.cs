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
using System.Text;
using static DiscUtils.Ntfs.Journal.JournalUtilities;

namespace DiscUtils.Ntfs.Journal;

/// <summary>
/// An NTFS log record: the redo and undo action of one step of a transaction, with the place it applies to.
/// </summary>
internal sealed class NtfsLogRecord
{
    public const int HeaderLength = 0x20;

    public LfsRecord Lfs { get; private set; }

    public ulong Lsn => Lfs.Lsn;

    public uint TransactionId => Lfs.TransactionId;

    public NtfsLogOperation RedoOperation { get; private set; }

    public NtfsLogOperation UndoOperation { get; private set; }

    public int RedoOffset { get; private set; }

    public int RedoLength { get; private set; }

    public int UndoOffset { get; private set; }

    public int UndoLength { get; private set; }

    /// <summary>The byte offset of the target attribute's entry in the open attribute table.</summary>
    public ushort TargetAttribute { get; private set; }

    /// <summary>The offset of the attribute in a file record, or of the index buffer in a page.</summary>
    public int RecordOffset { get; private set; }

    /// <summary>The offset inside the attribute (or of the index entry inside the index buffer).</summary>
    public int AttributeOffset { get; private set; }

    /// <summary>Where the target starts inside the cluster at <see cref="TargetVcn"/>, in 512-byte units.</summary>
    public int ClusterBlockOffset { get; private set; }

    public long TargetVcn { get; private set; }

    /// <summary>The clusters of the target page when the record was written, one per VCN from <see cref="TargetVcn"/>.</summary>
    public long[] Lcns { get; private set; }

    /// <summary>LFS header flag: the redo data is all zeros and not stored (its place may hold the undo data).</summary>
    public const ushort FlagRedoDataZero = 0x0002;

    /// <summary>LFS header flag: the undo data is all zeros and not stored.</summary>
    public const ushort FlagUndoDataZero = 0x0004;

    /// <summary>
    /// The redo data. Windows does not store data that is all zeros: flag 0x2 in the LFS header marks zero redo
    /// data (seen when a resident value is zeroed, e.g. by making a range of a file sparse; the bytes at the redo
    /// offset are then the undo data, stored once), flag 0x4 zero undo data. A record can also end before the
    /// declared data does; the missing bytes count as zeros as well.
    /// </summary>
    public ArraySegment<byte> RedoData => Data(RedoOffset, RedoLength, (Lfs.Flags & FlagRedoDataZero) != 0);

    /// <summary>The undo data (see <see cref="RedoData"/>).</summary>
    public ArraySegment<byte> UndoData => Data(UndoOffset, UndoLength, (Lfs.Flags & FlagUndoDataZero) != 0);

    private ArraySegment<byte> Data(int offset, int length, bool zero)
    {
        var bytes = Lfs.ClientData;
        if (length == 0)
        {
            return new ArraySegment<byte>(bytes, 0, 0);
        }

        if (zero)
        {
            return new ArraySegment<byte>(new byte[length]);
        }

        if (offset + length <= bytes.Length)
        {
            return new ArraySegment<byte>(bytes, offset, length);
        }

        var padded = new byte[length];
        if (offset < bytes.Length)
        {
            Array.Copy(bytes, offset, padded, 0, bytes.Length - offset);
        }

        return new ArraySegment<byte>(padded);
    }

    /// <summary>
    /// Parses the client data of an LFS client record; <c>null</c> and an error if it is not a well-formed NTFS
    /// log record.
    /// </summary>
    public static NtfsLogRecord TryParse(LfsRecord lfs, out string error)
    {
        var data = lfs.ClientData;
        error = null;
        if (data.Length < HeaderLength)
        {
            error = $"log record {lfs.Lsn:X} is too short";
            return null;
        }

        var record = new NtfsLogRecord
        {
            Lfs = lfs,
            RedoOperation = (NtfsLogOperation)U16(data, 0x00),
            UndoOperation = (NtfsLogOperation)U16(data, 0x02),
            RedoOffset = U16(data, 0x04),
            RedoLength = U16(data, 0x06),
            UndoOffset = U16(data, 0x08),
            UndoLength = U16(data, 0x0A),
            TargetAttribute = U16(data, 0x0C),
            RecordOffset = U16(data, 0x10),
            AttributeOffset = U16(data, 0x12),
            ClusterBlockOffset = U16(data, 0x14),
            TargetVcn = (long)U64(data, 0x18),
        };

        var lcnCount = U16(data, 0x0E);
        if (HeaderLength + (8 * lcnCount) > data.Length)
        {
            error = $"log record {lfs.Lsn:X} lists more clusters than it holds";
            return null;
        }

        record.Lcns = new long[lcnCount];
        for (var i = 0; i < lcnCount; i++)
        {
            record.Lcns[i] = (long)U64(data, HeaderLength + (8 * i));
        }

        // Data offsets are 8-byte aligned and start after the header and clusters; data may end early (see RedoData).
        var minimum = HeaderLength + (8 * lcnCount);
        if ((record.RedoLength > 0 && (record.RedoOffset < minimum || (record.RedoOffset & 7) != 0)) ||
            (record.UndoLength > 0 && (record.UndoOffset < minimum || (record.UndoOffset & 7) != 0)))
        {
            error = $"log record {lfs.Lsn:X} has misplaced redo or undo data";
            return null;
        }

        return record;
    }

    public override string ToString() =>
        $"{Lsn:X} {RedoOperation}/{UndoOperation} attr {TargetAttribute:X} vcn {TargetVcn:X} +{ClusterBlockOffset * 512:X} rec {RecordOffset:X} at {AttributeOffset:X}";
}

/// <summary>
/// The NTFS restart record (written at every checkpoint): where analysis starts and where the dumps of the
/// four restart tables are.
/// </summary>
internal sealed class NtfsRestartRecord
{
    public uint MajorVersion { get; private set; }

    public uint MinorVersion { get; private set; }

    public ulong CheckpointLsn { get; private set; }

    public ulong OpenAttributeTableLsn { get; private set; }

    public ulong AttributeNamesLsn { get; private set; }

    public ulong DirtyPageTableLsn { get; private set; }

    public ulong TransactionTableLsn { get; private set; }

    public int OpenAttributeTableLength { get; private set; }

    public int AttributeNamesLength { get; private set; }

    public int DirtyPageTableLength { get; private set; }

    public int TransactionTableLength { get; private set; }

    public static NtfsRestartRecord Parse(LfsRecord record)
    {
        var d = record.ClientData;
        if (record.RecordType != LfsRecord.TypeClientRestart || d.Length < 0x40)
        {
            throw new JournalException($"The NTFS restart record at {record.Lsn:X} is not a restart record");
        }

        var result = new NtfsRestartRecord
        {
            MajorVersion = U32(d, 0x00),
            MinorVersion = U32(d, 0x04),
            CheckpointLsn = U64(d, 0x08),
            OpenAttributeTableLsn = U64(d, 0x10),
            AttributeNamesLsn = U64(d, 0x18),
            DirtyPageTableLsn = U64(d, 0x20),
            TransactionTableLsn = U64(d, 0x28),
            OpenAttributeTableLength = (int)U32(d, 0x30),
            AttributeNamesLength = (int)U32(d, 0x34),
            DirtyPageTableLength = (int)U32(d, 0x38),
            TransactionTableLength = (int)U32(d, 0x3C),
        };

        if (result.MajorVersion > 1)
        {
            throw new JournalException($"NTFS restart version {result.MajorVersion}.{result.MinorVersion} is not supported");
        }

        return result;
    }
}

/// <summary>
/// An entry of the open attribute table: an attribute that log records refer to by the entry's offset.
/// </summary>
internal sealed class OpenAttributeEntry
{
    public const int SizeVersion0 = 0x2C;
    public const int SizeVersion1 = 0x28;

    public uint AttributeType { get; set; }

    public ulong FileReference { get; set; }

    public long FileRecordNumber => (long)(FileReference & 0x0000FFFFFFFFFFFF);

    public uint BytesPerIndexBuffer { get; set; }

    public ulong OpenRecordLsn { get; set; }

    public string Name { get; set; } = "";

    public bool IsIndexAllocation => AttributeType == 0xA0;

    /// <summary>Reads an entry in the layout of NTFS restart version 0 or 1.</summary>
    public static OpenAttributeEntry Parse(byte[] b, int offset, uint version)
    {
        if (version == 0)
        {
            return new OpenAttributeEntry
            {
                FileReference = U64(b, offset + 0x08),
                OpenRecordLsn = U64(b, offset + 0x10),
                AttributeType = U32(b, offset + 0x1C),
                BytesPerIndexBuffer = U32(b, offset + 0x28),
            };
        }

        return new OpenAttributeEntry
        {
            BytesPerIndexBuffer = U32(b, offset + 0x04),
            AttributeType = U32(b, offset + 0x08),
            FileReference = U64(b, offset + 0x10),
            OpenRecordLsn = U64(b, offset + 0x18),
        };
    }

    public override string ToString() => $"MFT {FileRecordNumber} type {AttributeType:X} '{Name}'";
}

/// <summary>
/// An entry of the dirty page table: a page that may hold changes that are only in the log.
/// </summary>
internal sealed class DirtyPageEntry
{
    public uint TargetAttribute { get; set; }

    public uint TransferLength { get; set; }

    public long Vcn { get; set; }

    /// <summary>The LSN of the oldest change to this page that may be missing on disk.</summary>
    public ulong OldestLsn { get; set; }

    /// <summary>The page's clusters, one per VCN from <see cref="Vcn"/>; 0 once the cluster was freed.</summary>
    public long[] Lcns { get; set; }

    public bool Covers(uint targetAttribute, long vcn) =>
        TargetAttribute == targetAttribute && vcn >= Vcn && vcn < Vcn + Lcns.Length;

    public static DirtyPageEntry Parse(byte[] b, int offset, int entrySize, uint version)
    {
        var lcnsOffset = version == 0 ? 0x24 : 0x20;
        var lcnCount = (int)U32(b, offset + 0x0C);
        if (lcnCount < 0 || lcnsOffset + (8 * lcnCount) > entrySize)
        {
            throw new JournalException("A dirty page table entry lists more clusters than it holds");
        }

        var entry = new DirtyPageEntry
        {
            TargetAttribute = U32(b, offset + 0x04),
            TransferLength = U32(b, offset + 0x08),
            Vcn = (long)U64(b, offset + (version == 0 ? 0x14 : 0x10)),
            OldestLsn = U64(b, offset + (version == 0 ? 0x1C : 0x18)),
            Lcns = new long[lcnCount],
        };
        for (var i = 0; i < lcnCount; i++)
        {
            entry.Lcns[i] = (long)U64(b, offset + lcnsOffset + (8 * i));
        }

        return entry;
    }
}

internal enum TransactionState : byte
{
    Uninitialized = 0,
    Active = 1,
    Prepared = 2,
    Committed = 3,
}

/// <summary>
/// An entry of the transaction table; its offset in the table is the transaction id in log records.
/// </summary>
internal sealed class TransactionEntry
{
    public TransactionState State { get; set; }

    public ulong FirstLsn { get; set; }

    public ulong PreviousLsn { get; set; }

    /// <summary>The next record to undo when rolling the transaction back; 0 if there is nothing to undo.</summary>
    public ulong UndoNextLsn { get; set; }

    public static TransactionEntry Parse(byte[] b, int offset) => new()
    {
        State = (TransactionState)b[offset + 0x04],
        FirstLsn = U64(b, offset + 0x08),
        PreviousLsn = U64(b, offset + 0x10),
        UndoNextLsn = U64(b, offset + 0x18),
    };
}

/// <summary>
/// Reads the restart tables that NTFS dumps into the log at every checkpoint.
/// </summary>
internal static class RestartTable
{
    private const int HeaderLength = 0x18;
    private const uint EntryAllocated = 0xFFFFFFFF;

    /// <summary>
    /// The allocated entries of a dumped restart table, keyed by their byte offset from the start of the table.
    /// </summary>
    public static IEnumerable<(int Offset, int EntrySize)> Entries(byte[] table)
    {
        if (table.Length < HeaderLength)
        {
            throw new JournalException("A restart table dump is too short");
        }

        int entrySize = U16(table, 0x00);
        int entryCount = U16(table, 0x02);
        if (entrySize < 8 || HeaderLength + (entrySize * entryCount) > table.Length)
        {
            throw new JournalException("A restart table dump has an implausible header");
        }

        for (var i = 0; i < entryCount; i++)
        {
            var offset = HeaderLength + (i * entrySize);
            if (U32(table, offset) == EntryAllocated)
            {
                yield return (offset, entrySize);
            }
        }
    }

    /// <summary>
    /// The attribute names dump: (open attribute table offset, name) pairs.
    /// </summary>
    public static IEnumerable<(int Offset, string Name)> AttributeNames(byte[] dump)
    {
        var position = 0;
        while (position + 4 <= dump.Length)
        {
            int tableOffset = U16(dump, position);
            int nameBytes = U16(dump, position + 2);
            if (tableOffset == 0 && nameBytes == 0)
            {
                yield break;
            }

            if (position + 4 + nameBytes > dump.Length)
            {
                throw new JournalException("The attribute names dump is damaged");
            }

            yield return (tableOffset, Encoding.Unicode.GetString(dump, position + 4, nameBytes));
            position += 4 + nameBytes + 2;
        }
    }
}
