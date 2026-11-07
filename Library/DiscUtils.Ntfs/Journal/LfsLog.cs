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
/// A client of the log (NTFS is the only one) as listed in the restart area.
/// </summary>
internal sealed class LfsClient
{
    public ulong OldestLsn { get; set; }

    public ulong RestartLsn { get; set; }

    public ushort PreviousClient { get; set; }

    public ushort NextClient { get; set; }

    public ushort SequenceNumber { get; set; }

    public string Name { get; set; }
}

/// <summary>
/// One of the two restart pages at the start of $LogFile: the page header and its restart area.
/// </summary>
internal sealed class LfsRestartPage
{
    public const ushort NoClient = 0xFFFF;
    public const ushort FlagCleanDismount = 0x0002;

    public long Offset { get; set; }

    public uint Signature { get; set; }

    public int SystemPageSize { get; set; }

    public int LogPageSize { get; set; }

    public int RestartAreaOffset { get; set; }

    public short MinorVersion { get; set; }

    public short MajorVersion { get; set; }

    public ulong CurrentLsn { get; set; }

    public ushort LogClients { get; set; }

    public ushort FreeClientList { get; set; }

    public ushort InUseClientList { get; set; }

    public ushort Flags { get; set; }

    public int SequenceNumberBits { get; set; }

    public ushort ClientArrayOffset { get; set; }

    public long FileSize { get; set; }

    public ushort RecordHeaderLength { get; set; }

    public ushort LogPageDataOffset { get; set; }

    public uint OpenLogCount { get; set; }

    public List<LfsClient> Clients { get; } = [];

    /// <summary>The page with its update sequence array removed.</summary>
    public byte[] Page { get; set; }

    public bool IsClean => (Flags & FlagCleanDismount) != 0;

    public LfsClient InUseClient => InUseClientList == NoClient ? null : Clients[InUseClientList];

    /// <summary>
    /// Reads a restart page at <paramref name="offset"/>; <c>null</c> if there is none or it is damaged.
    /// </summary>
    public static LfsRestartPage TryRead(LogFileStorage storage, long offset)
    {
        var head = new byte[512];
        if (offset > storage.Length - head.Length)
        {
            return null;
        }

        storage.Read(offset, head, 0, head.Length);
        var signature = U32(head, 0);
        if (signature != SignatureRstr && signature != SignatureChkd)
        {
            return null;
        }

        var systemPageSize = (int)U32(head, 0x10);
        var logPageSize = (int)U32(head, 0x14);
        if (!IsPageSize(systemPageSize) || !IsPageSize(logPageSize) ||
            (offset != 0 && offset != systemPageSize) || offset > storage.Length - systemPageSize)
        {
            return null;
        }

        var page = new byte[systemPageSize];
        storage.Read(offset, page, 0, page.Length);
        if (!RemoveFixups(page, 0, page.Length))
        {
            return null;
        }

        var result = new LfsRestartPage
        {
            Offset = offset,
            Signature = signature,
            SystemPageSize = systemPageSize,
            LogPageSize = logPageSize,
            RestartAreaOffset = U16(page, 0x18),
            MinorVersion = (short)U16(page, 0x1A),
            MajorVersion = (short)U16(page, 0x1C),
            Page = page,
        };

        var ra = result.RestartAreaOffset;
        if ((ra & 7) != 0 || ra < 0x1E || ra < U16(page, 4) + 2 * U16(page, 6) || ra + 0x30 > systemPageSize)
        {
            return null;
        }

        result.CurrentLsn = U64(page, ra);
        result.LogClients = U16(page, ra + 0x08);
        result.FreeClientList = U16(page, ra + 0x0A);
        result.InUseClientList = U16(page, ra + 0x0C);
        result.Flags = U16(page, ra + 0x0E);
        result.SequenceNumberBits = (int)U32(page, ra + 0x10);
        result.ClientArrayOffset = U16(page, ra + 0x16);
        result.FileSize = (long)U64(page, ra + 0x18);
        result.RecordHeaderLength = U16(page, ra + 0x24);
        result.LogPageDataOffset = U16(page, ra + 0x26);
        result.OpenLogCount = U32(page, ra + 0x28);

        var clients = ra + result.ClientArrayOffset;
        if (result.LogClients == 0 || result.LogClients > 64 || result.ClientArrayOffset < 0x2C ||
            clients + (result.LogClients * 0xA0) > systemPageSize)
        {
            return null;
        }

        for (var i = 0; i < result.LogClients; i++)
        {
            var c = clients + (i * 0xA0);
            var nameBytes = (int)Math.Min(U32(page, c + 0x1C), 0x80);
            result.Clients.Add(new LfsClient
            {
                OldestLsn = U64(page, c),
                RestartLsn = U64(page, c + 0x08),
                PreviousClient = U16(page, c + 0x10),
                NextClient = U16(page, c + 0x12),
                SequenceNumber = U16(page, c + 0x14),
                Name = Encoding.Unicode.GetString(page, c + 0x20, nameBytes),
            });
        }

        if (result.InUseClientList != NoClient && result.InUseClientList >= result.LogClients)
        {
            return null;
        }

        return result;
    }

    private static bool IsPageSize(int size) => size is >= 512 and <= 0x10000 && (size & (size - 1)) == 0;
}

/// <summary>
/// A log record as LFS stores it: its header and the client's data.
/// </summary>
internal sealed class LfsRecord
{
    public const uint TypeClientRecord = 1;
    public const uint TypeClientRestart = 2;

    public ulong Lsn { get; set; }

    public ulong ClientPreviousLsn { get; set; }

    public ulong ClientUndoNextLsn { get; set; }

    public ushort ClientSequenceNumber { get; set; }

    public ushort ClientIndex { get; set; }

    public uint RecordType { get; set; }

    public uint TransactionId { get; set; }

    public ushort Flags { get; set; }

    public byte[] ClientData { get; set; }
}

/// <summary>
/// Reads the log records of $LogFile (log file service versions 1.x and 2.0).
/// </summary>
/// <remarks>
/// LFS 2.0 (Windows 8 and later, while the volume is mounted) keeps 32 "tail" copies of recently written log pages
/// in pages 2 to 0x21, version 1.x keeps two in pages 2 and 3. A copy can be newer than the page at its place in
/// the log, so every page is read from whichever copy holds the highest LSN. A record belongs to the log only if
/// its header carries exactly the LSN it was looked up by; the first position where that fails is the end.
/// </remarks>
internal sealed class LfsLog
{
    // The NTFS client uses 16-bit counts for the LCN array and 16-bit offsets and lengths for operation data.
    private const int MaxClientDataLength = NtfsLogRecord.HeaderLength + 8 * ushort.MaxValue;

    private readonly LogFileStorage _storage;
    private readonly Dictionary<long, byte[]> _pages = [];
    private readonly Dictionary<long, List<byte[]>> _tailCopies = [];

    private LfsLog(LogFileStorage storage, LfsRestartPage restart)
    {
        _storage = storage;
        Restart = restart;
        PageSize = restart.LogPageSize;
        SequenceNumberBits = restart.SequenceNumberBits;
        FileSize = restart.FileSize;
        DataOffset = restart.LogPageDataOffset;
        RecordHeaderLength = restart.RecordHeaderLength;
        FirstPageOffset = (restart.MajorVersion >= 2 ? 0x22L : 4L) * PageSize;
        ReadTailCopies();
    }

    public LogFileStorage Storage => _storage;

    /// <summary>The restart page in use (the valid copy with the highest current LSN).</summary>
    public LfsRestartPage Restart { get; }

    public int PageSize { get; }

    public int SequenceNumberBits { get; }

    public long FileSize { get; }

    public int DataOffset { get; }

    public int RecordHeaderLength { get; }

    public long FirstPageOffset { get; }

    /// <summary>
    /// The kind of $LogFile found.
    /// </summary>
    public enum Kind
    {
        /// <summary>No restart page; the file is filled with 0xFF (freshly formatted or reset). Nothing to do.</summary>
        Empty,

        /// <summary>chkdsk wrote a CHKD restart page: it already repaired the volume and obsoleted the log.</summary>
        CheckedByChkdsk,

        /// <summary>A usable log.</summary>
        Log,
    }

    /// <summary>
    /// Opens the log. <paramref name="kind"/> tells whether there is a log at all; the result is <c>null</c> unless
    /// it is <see cref="Kind.Log"/>.
    /// </summary>
    public static LfsLog Open(LogFileStorage storage, out Kind kind)
    {
        var first = LfsRestartPage.TryRead(storage, 0);
        LfsRestartPage second = null;
        if (first != null)
        {
            second = LfsRestartPage.TryRead(storage, first.SystemPageSize);
        }
        else
        {
            for (long offset = 512; offset <= 0x10000 && second == null; offset *= 2)
            {
                second = LfsRestartPage.TryRead(storage, offset);
            }
        }

        if (first == null && second == null)
        {
            var head = new byte[512];
            storage.Read(0, head, 0, head.Length);
            if (Array.TrueForAll(head, b => b == 0xFF))
            {
                kind = Kind.Empty;
                return null;
            }

            throw new JournalException("$LogFile has no valid restart page");
        }

        if (first?.Signature == SignatureChkd || second?.Signature == SignatureChkd)
        {
            kind = Kind.CheckedByChkdsk;
            return null;
        }

        var restart = first == null || (second != null && second.CurrentLsn > first.CurrentLsn) ? second : first;
        if (restart.MajorVersion is not (1 or 2))
        {
            throw new JournalException(
                $"$LogFile uses log file service version {restart.MajorVersion}.{restart.MinorVersion}, which DiscUtils does not support");
        }

        var minLogPages = restart.MajorVersion >= 2 ? 0x23L : 8L;
        if (restart.SequenceNumberBits is < 16 or > 60 || restart.FileSize > storage.Length ||
            restart.FileSize < minLogPages * restart.LogPageSize || restart.FileSize < 2L * restart.SystemPageSize ||
            restart.FileSize > (1L << (67 - restart.SequenceNumberBits)) || (restart.FileSize & (restart.LogPageSize - 1)) != 0 ||
            restart.RecordHeaderLength != 0x30 || restart.LogPageDataOffset < 0x28 ||
            restart.LogPageDataOffset > restart.LogPageSize - restart.RecordHeaderLength || (restart.LogPageDataOffset & 7) != 0)
        {
            throw new JournalException("$LogFile has an implausible restart area");
        }

        kind = Kind.Log;
        return new LfsLog(storage, restart);
    }

    public long LsnToOffset(ulong lsn) => (long)((lsn << SequenceNumberBits) >> (SequenceNumberBits - 3));

    public ulong SequenceOf(ulong lsn) => lsn >> (64 - SequenceNumberBits);

    private ulong MakeLsn(ulong sequence, long offset) => (sequence << (64 - SequenceNumberBits)) + (ulong)(offset >> 3);

    /// <summary>
    /// Reads the record with this LSN; <c>null</c> if the log holds no such record (the end of the log).
    /// </summary>
    public LfsRecord ReadRecord(ulong lsn)
    {
        if (lsn == 0)
        {
            return null;
        }

        var offset = LsnToOffset(lsn);
        var pageOffset = offset & ~(long)(PageSize - 1);
        var inPage = (int)(offset - pageOffset);
        if (pageOffset < FirstPageOffset || offset >= FileSize || inPage < DataOffset || inPage + RecordHeaderLength > PageSize)
        {
            return null;
        }

        var page = GetPage(pageOffset, lsn);
        if (page == null || U64(page, inPage) != lsn)
        {
            return null;
        }

        var dataLength = U32(page, inPage + 0x18);
        var firstPageCapacity = PageSize - (inPage + RecordHeaderLength);
        var continuationPages = (FileSize - FirstPageOffset) / PageSize - 1;
        if (dataLength > MaxClientDataLength || dataLength > firstPageCapacity + continuationPages * (PageSize - DataOffset))
        {
            throw new JournalException($"Log record {lsn:X} has an invalid client data length");
        }

        var record = new LfsRecord
        {
            Lsn = lsn,
            ClientPreviousLsn = U64(page, inPage + 0x08),
            ClientUndoNextLsn = U64(page, inPage + 0x10),
            ClientSequenceNumber = U16(page, inPage + 0x1C),
            ClientIndex = U16(page, inPage + 0x1E),
            RecordType = U32(page, inPage + 0x20),
            TransactionId = U32(page, inPage + 0x24),
            Flags = U16(page, inPage + 0x28),
            ClientData = new byte[(int)dataLength],
        };

        var copied = Math.Min(record.ClientData.Length, firstPageCapacity);
        Array.Copy(page, inPage + RecordHeaderLength, record.ClientData, 0, copied);
        var current = pageOffset;
        while (copied < record.ClientData.Length)
        {
            current = NextPageOffset(current, out _);
            var next = GetPage(current, lsn);
            if (next == null)
            {
                return null;
            }

            var chunk = Math.Min(record.ClientData.Length - copied, PageSize - DataOffset);
            Array.Copy(next, DataOffset, record.ClientData, copied, chunk);
            copied += chunk;
        }

        return record;
    }

    /// <summary>
    /// The LSN right after a record: records are 8-byte aligned, continue after the header of the next page, and
    /// a record whose header no longer fits in a page starts on the next page.
    /// </summary>
    public ulong NextLsn(LfsRecord record)
    {
        var offset = LsnToOffset(record.Lsn);
        var sequence = SequenceOf(record.Lsn);
        var remaining = (long)Align8(RecordHeaderLength + record.ClientData.Length);
        var pageOffset = offset & ~(long)(PageSize - 1);
        var room = PageSize - (offset - pageOffset);
        long position;
        if (remaining < room)
        {
            position = offset + remaining;
        }
        else
        {
            remaining -= room;
            while (true)
            {
                pageOffset = NextPageOffset(pageOffset, out var wrapped);
                if (wrapped)
                {
                    sequence++;
                }

                var available = PageSize - DataOffset;
                if (remaining < available)
                {
                    position = pageOffset + DataOffset + remaining;
                    break;
                }

                remaining -= available;
            }
        }

        if (PageSize - (position & (PageSize - 1)) < RecordHeaderLength)
        {
            pageOffset = NextPageOffset(position & ~(long)(PageSize - 1), out var wrapped);
            if (wrapped)
            {
                sequence++;
            }

            position = pageOffset + DataOffset;
        }

        return MakeLsn(sequence, position);
    }

    /// <summary>
    /// Reads the records from <paramref name="startLsn"/> up to the end of the log, in order.
    /// </summary>
    public IEnumerable<LfsRecord> ReadForward(ulong startLsn)
    {
        var record = ReadRecord(startLsn);
        while (record != null)
        {
            yield return record;
            var next = NextLsn(record);
            if (next <= record.Lsn)
            {
                throw new JournalException($"The next LSN after {record.Lsn:X} overflows the log sequence");
            }

            record = ReadRecord(next);
        }
    }

    private long NextPageOffset(long pageOffset, out bool wrapped)
    {
        var next = pageOffset + PageSize;
        wrapped = next >= FileSize;
        return wrapped ? FirstPageOffset : next;
    }

    /// <summary>
    /// The log page at <paramref name="pageOffset"/> from whichever copy (in place or tail copy) is the newest
    /// one that can hold data of the record <paramref name="lsn"/>; <c>null</c> if none is valid.
    /// </summary>
    private byte[] GetPage(long pageOffset, ulong lsn)
    {
        if (!_pages.TryGetValue(pageOffset, out var main))
        {
            main = ReadLogPage(pageOffset);
            _pages[pageOffset] = main;
        }

        byte[] best = null;
        ulong bestLsn = 0;
        if (main != null)
        {
            best = main;
            bestLsn = U64(main, 0x08);
        }

        if (_tailCopies.TryGetValue(pageOffset, out var copies))
        {
            foreach (var copy in copies)
            {
                var copyLsn = TailCopyLsn(copy);
                if (best == null || copyLsn > bestLsn)
                {
                    best = copy;
                    bestLsn = copyLsn;
                }
            }
        }

        // A page that was last written before this record existed cannot hold any of its data.
        if (best != null && bestLsn < lsn)
        {
            return null;
        }

        return best;
    }

    private ulong TailCopyLsn(byte[] copy) => Restart.MajorVersion >= 2 ? U64(copy, 0x08) : U64(copy, 0x20);

    private byte[] ReadLogPage(long pageOffset)
    {
        var page = new byte[PageSize];
        _storage.Read(pageOffset, page, 0, page.Length);
        if (U32(page, 0) != SignatureRcrd || !RemoveFixups(page, 0, page.Length))
        {
            return null;
        }

        return page;
    }

    private void ReadTailCopies()
    {
        for (var offset = 2L * PageSize; offset < FirstPageOffset; offset += PageSize)
        {
            var page = ReadLogPage(offset);
            if (page == null)
            {
                continue;
            }

            // Version 2.0 keeps the place of the copied page at 0x3C; version 1.x puts it where an LSN would be.
            var target = Restart.MajorVersion >= 2 ? U32(page, 0x3C) : (long)U64(page, 0x08);
            if (target < FirstPageOffset || target >= FileSize || (target & (PageSize - 1)) != 0)
            {
                continue;
            }

            if (!_tailCopies.TryGetValue(target, out var list))
            {
                list = [];
                _tailCopies[target] = list;
            }

            list.Add(page);
        }
    }
}
