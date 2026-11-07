using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DiscUtils.Ntfs.Journal;
using DiscUtils.Streams;

namespace LibraryTests.Ntfs.Journal;

/// <summary>
/// One NTFS log record for <see cref="TestJournalWriter"/>.
/// </summary>
internal sealed class TestLogRecord
{
    public NtfsLogOperation Redo { get; set; }

    public NtfsLogOperation Undo { get; set; }

    public uint TransactionId { get; set; } = 0x18;

    public byte[] RedoData { get; set; } = [];

    public byte[] UndoData { get; set; } = [];

    /// <summary>Declared redo length when bytes are left out (Windows' trick for zeros); default: the data length.</summary>
    public int? RedoLength { get; set; }

    public int? UndoLength { get; set; }

    public ushort TargetAttribute { get; set; }

    public long TargetVcn { get; set; }

    public int ClusterBlockOffset { get; set; }

    public int RecordOffset { get; set; }

    public int AttributeOffset { get; set; }

    public long[] Lcns { get; set; } = [];

    /// <summary>The previous record of this transaction to undo; 0 = none.</summary>
    public ulong UndoNextLsn { get; set; }

    /// <summary>The NTFS client data: the 0x20-byte header, the clusters, then redo and undo data (8-byte aligned).</summary>
    public byte[] ClientData()
    {
        var redoOffset = 0x20 + (8 * Lcns.Length);
        var undoOffset = redoOffset + ((RedoData.Length + 7) & ~7);
        var length = undoOffset + UndoData.Length;
        var b = new byte[length];
        Put16(b, 0x00, (ushort)Redo);
        Put16(b, 0x02, (ushort)Undo);
        Put16(b, 0x04, (ushort)redoOffset);
        Put16(b, 0x06, (ushort)(RedoLength ?? RedoData.Length));
        Put16(b, 0x08, (ushort)undoOffset);
        Put16(b, 0x0A, (ushort)(UndoLength ?? UndoData.Length));
        Put16(b, 0x0C, TargetAttribute);
        Put16(b, 0x0E, (ushort)Lcns.Length);
        Put16(b, 0x10, (ushort)RecordOffset);
        Put16(b, 0x12, (ushort)AttributeOffset);
        Put16(b, 0x14, (ushort)ClusterBlockOffset);
        Put64(b, 0x18, (ulong)TargetVcn);
        for (var i = 0; i < Lcns.Length; i++)
        {
            Put64(b, 0x20 + (8 * i), (ulong)Lcns[i]);
        }

        Array.Copy(RedoData, 0, b, redoOffset, RedoData.Length);
        Array.Copy(UndoData, 0, b, undoOffset, UndoData.Length);
        return b;
    }

    private static void Put16(byte[] b, int o, ushort v) => EndianUtilities.WriteBytesLittleEndian(v, b, o);

    private static void Put64(byte[] b, int o, ulong v) => EndianUtilities.WriteBytesLittleEndian(v, b, o);
}

/// <summary>
/// Writes a journal into a volume's $LogFile the way Windows' log file service lays it out: restart pages, record
/// pages with update sequence arrays, 8-byte aligned records continued after the next page's header, wrapping to
/// the first record page with the next sequence number, and (version 2.0) tail copies of recently written pages.
/// Tests use it to make a volume "Windows crashed on".
/// </summary>
internal sealed class TestJournalWriter
{
    public const int PageSize = 4096;
    private const int DataOffset = 0x40;
    private const int HeaderLength = 0x30;

    private readonly LogFileStorage _storage;
    private readonly Dictionary<long, byte[]> _pages = [];
    private readonly Dictionary<long, (ulong LastLsn, ulong LastEndLsn, int NextFree, bool RecordEnds)> _pageInfo = [];
    private long _position;
    private ulong _sequence;

    public TestJournalWriter(Stream volume, int majorVersion = 2, long? startOffset = null, ulong sequence = 1)
    {
        Volume = volume;
        Layout = JournalVolumeLayout.Read(volume);
        _storage = LogFileStorage.Open(volume, Layout);
        MajorVersion = majorVersion;
        FileSize = _storage.Length & ~(long)(PageSize - 1);
        var log2 = 0;
        while ((1L << log2) < FileSize)
        {
            log2++;
        }

        SequenceNumberBits = 66 - log2;
        FirstPageOffset = (majorVersion >= 2 ? 0x22L : 4L) * PageSize;
        _position = (startOffset ?? FirstPageOffset) + DataOffset;
        _sequence = sequence;
    }

    public Stream Volume { get; }

    public JournalVolumeLayout Layout { get; }

    public int MajorVersion { get; }

    public long FileSize { get; }

    public int SequenceNumberBits { get; }

    public long FirstPageOffset { get; }

    public ulong LastLsn { get; private set; }

    /// <summary>Where the next record goes (file offset in $LogFile).</summary>
    public long Position => _position;

    /// <summary>Pages written to their place in the log (false: only to the tail copy area, as if the crash came first).</summary>
    public HashSet<long> PagesOnlyInTail { get; } = [];

    public ulong LsnOf(long offset, ulong sequence) => (sequence << (64 - SequenceNumberBits)) + (ulong)(offset >> 3);

    public long LsnToOffsetForTest(ulong lsn) => (long)((lsn << SequenceNumberBits) >> (SequenceNumberBits - 3));

    /// <summary>Appends an NTFS client record and returns its LSN.</summary>
    public ulong Append(TestLogRecord record, ushort flags = 0) =>
        AppendRaw(record.ClientData(), LfsRecord.TypeClientRecord, record.TransactionId, record.UndoNextLsn, flags);

    /// <summary>Appends any LFS record (the client data as given) and returns its LSN.</summary>
    public ulong AppendRaw(byte[] clientData, uint recordType, uint transactionId, ulong undoNextLsn, ushort flags = 0)
    {
        // A record starts after the page header, and on the next page when its header no longer fits.
        if (_position % PageSize == 0 || PageSize - (_position % PageSize) < HeaderLength)
        {
            NextPage();
        }

        var lsn = LsnOf(_position, _sequence);
        var header = new byte[HeaderLength];
        Put64(header, 0x00, lsn);
        Put64(header, 0x08, LastLsn);
        Put64(header, 0x10, undoNextLsn);
        Put32(header, 0x18, (uint)clientData.Length);
        Put32(header, 0x20, recordType);
        Put32(header, 0x24, transactionId);
        var multiPage = (_position % PageSize) + HeaderLength + clientData.Length > PageSize;
        Put16(header, 0x28, (ushort)(flags | (multiPage ? 1 : 0)));

        var bytes = new byte[HeaderLength + clientData.Length];
        Array.Copy(header, bytes, HeaderLength);
        Array.Copy(clientData, 0, bytes, HeaderLength, clientData.Length);

        var written = 0;
        while (written < bytes.Length)
        {
            var page = Page(_position & ~(long)(PageSize - 1));
            var inPage = (int)(_position % PageSize);
            var chunk = Math.Min(bytes.Length - written, PageSize - inPage);
            Array.Copy(bytes, written, page, inPage, chunk);
            written += chunk;
            _position += chunk;
            var info = _pageInfo[_position - 1 & ~(long)(PageSize - 1)];
            info.LastLsn = lsn;
            if (written == bytes.Length)
            {
                info.LastEndLsn = lsn;
                info.RecordEnds = true;
                info.NextFree = (int)(((_position + 7) & ~7L) % PageSize);
            }

            _pageInfo[_position - 1 & ~(long)(PageSize - 1)] = info;
            if (written < bytes.Length)
            {
                NextPage();
            }
        }

        _position = (_position + 7) & ~7L;
        LastLsn = lsn;
        return lsn;
    }

    /// <summary>
    /// Writes a checkpoint: the table dumps and the NTFS restart record. Returns the restart record's LSN (what the
    /// restart area's client points at).
    /// </summary>
    public ulong Checkpoint(
        IReadOnlyList<(ushort Offset, uint Type, ulong FileReference, uint BytesPerIndex, string Name)> openAttributes,
        IReadOnlyList<(uint TargetAttribute, long Vcn, ulong OldestLsn, long[] Lcns)>? dirtyPages = null,
        IReadOnlyList<(uint Offset, TransactionState State, ulong FirstLsn, ulong UndoNextLsn)>? transactions = null,
        int clustersPerPage = 1,
        ulong? checkpointLsn = null,
        uint ntfsVersion = 1)
    {
        var start = Append(new TestLogRecord { Redo = NtfsLogOperation.Noop, Undo = NtfsLogOperation.Noop, TransactionId = 0x18 });

        ulong oatLsn = 0, namesLsn = 0, dptLsn = 0, ttLsn = 0;
        int oatLen = 0, namesLen = 0, dptLen = 0, ttLen = 0;
        if (openAttributes is { Count: > 0 })
        {
            var max = 0;
            foreach (var a in openAttributes)
            {
                max = Math.Max(max, a.Offset);
            }

            var entrySize = ntfsVersion == 0 ? 0x2C : 0x28;
            var table = Table(entrySize, (max - 0x18) / entrySize + 1);
            var names = new MemoryStream();
            foreach (var a in openAttributes)
            {
                Put32(table, a.Offset, 0xFFFFFFFF);
                OpenAttributeEntryBytes(a.Type, a.FileReference, a.BytesPerIndex, ntfsVersion).AsSpan(4).CopyTo(table.AsSpan(a.Offset + 4));
                if (!string.IsNullOrEmpty(a.Name))
                {
                    var name = Encoding.Unicode.GetBytes(a.Name);
                    names.Write(BitConverter.GetBytes(a.Offset), 0, 2);
                    names.Write(BitConverter.GetBytes((ushort)name.Length), 0, 2);
                    names.Write(name, 0, name.Length);
                    names.Write(new byte[2], 0, 2);
                }
            }

            oatLen = table.Length;
            oatLsn = Append(Dump(NtfsLogOperation.OpenAttributeTableDump, table));
            if (names.Length > 0)
            {
                names.Write(new byte[4], 0, 4);
                var bytes = names.ToArray();
                namesLen = bytes.Length;
                namesLsn = Append(Dump(NtfsLogOperation.AttributeNamesDump, bytes));
            }
        }

        if (dirtyPages is { Count: > 0 })
        {
            var lcnsOffset = ntfsVersion == 0 ? 0x24 : 0x20;
            var size = lcnsOffset + (8 * clustersPerPage);
            var table = Table(size, dirtyPages.Count);
            for (var i = 0; i < dirtyPages.Count; i++)
            {
                var e = 0x18 + (i * size);
                var page = dirtyPages[i];
                Put32(table, e, 0xFFFFFFFF);
                Put32(table, e + 0x04, page.TargetAttribute);
                Put32(table, e + 0x08, (uint)(clustersPerPage * Layout.BytesPerCluster));
                Put32(table, e + 0x0C, (uint)clustersPerPage);
                Put64(table, e + (ntfsVersion == 0 ? 0x14 : 0x10), (ulong)page.Vcn);
                Put64(table, e + (ntfsVersion == 0 ? 0x1C : 0x18), page.OldestLsn);
                for (var j = 0; j < page.Lcns.Length; j++)
                {
                    Put64(table, e + lcnsOffset + (8 * j), (ulong)page.Lcns[j]);
                }
            }

            dptLen = table.Length;
            dptLsn = Append(Dump(NtfsLogOperation.DirtyPageTableDump, table));
        }

        if (transactions is { Count: > 0 })
        {
            var max = 0u;
            foreach (var t in transactions)
            {
                max = Math.Max(max, t.Offset);
            }

            var table = Table(0x28, (int)((max - 0x18) / 0x28) + 1);
            foreach (var t in transactions)
            {
                var e = (int)t.Offset;
                Put32(table, e, 0xFFFFFFFF);
                table[e + 4] = (byte)t.State;
                Put64(table, e + 0x08, t.FirstLsn);
                Put64(table, e + 0x10, t.UndoNextLsn);
                Put64(table, e + 0x18, t.UndoNextLsn);
            }

            ttLen = table.Length;
            ttLsn = Append(Dump(NtfsLogOperation.TransactionTableDump, table));
        }

        var restart = new byte[0x40];
        Put32(restart, 0x00, ntfsVersion);
        Put32(restart, 0x04, 0);
        Put64(restart, 0x08, checkpointLsn ?? start);
        Put64(restart, 0x10, oatLsn);
        Put64(restart, 0x18, namesLsn);
        Put64(restart, 0x20, dptLsn);
        Put64(restart, 0x28, ttLsn);
        Put32(restart, 0x30, (uint)oatLen);
        Put32(restart, 0x34, (uint)namesLen);
        Put32(restart, 0x38, (uint)dptLen);
        Put32(restart, 0x3C, (uint)ttLen);
        return AppendRaw(restart, LfsRecord.TypeClientRestart, 0, 0);
    }

    /// <summary>
    /// Writes the record pages and both restart pages. <paramref name="restartLsn"/> is the NTFS restart record.
    /// </summary>
    public void Finish(ulong restartLsn, ulong oldestLsn = 0, bool clean = false, ulong? currentLsn = null)
    {
        var tailSlot = 0;
        foreach (var pair in _pages)
        {
            var page = (byte[])pair.Value.Clone();
            var info = _pageInfo[pair.Key];
            Put32(page, 0, JournalUtilities.SignatureRcrd);
            Put16(page, 4, 0x28);
            Put16(page, 6, PageSize / 512 + 1);
            Put64(page, 0x08, MajorVersion >= 2 ? info.LastLsn : info.LastLsn);
            Put32(page, 0x10, info.RecordEnds ? 1u : 0u);
            Put16(page, 0x14, 1);
            Put16(page, 0x16, 1);
            Put16(page, 0x18, (ushort)info.NextFree);
            Put64(page, 0x20, info.LastEndLsn);
            if (MajorVersion >= 2)
            {
                Put32(page, 0x3C, (uint)pair.Key);
            }

            JournalUtilities.ApplyFixups(page, 0, PageSize);
            if (!PagesOnlyInTail.Contains(pair.Key))
            {
                _storage.Write(pair.Key, page, 0, PageSize);
            }

            if (PagesOnlyInTail.Contains(pair.Key) || TailCopies)
            {
                var copy = (byte[])pair.Value.Clone();
                Array.Copy(page, copy, PageSize);
                JournalUtilities.RemoveFixups(copy, 0, PageSize);
                if (MajorVersion < 2)
                {
                    // Version 1.x tail copies keep the page's place where an LSN would be.
                    Put64(copy, 0x08, (ulong)pair.Key);
                }

                JournalUtilities.ApplyFixups(copy, 0, PageSize);
                var slot = 2 + (tailSlot++ % (MajorVersion >= 2 ? 0x20 : 2));
                _storage.Write((long)slot * PageSize, copy, 0, PageSize);
            }
        }

        var restart = RestartPage(restartLsn, oldestLsn, clean, currentLsn ?? LastLsn);
        _storage.Write(0, restart, 0, PageSize);
        _storage.Write(PageSize, restart, 0, PageSize);
        Volume.Flush();
    }

    /// <summary>Whether every record page is also copied into the tail area.</summary>
    public bool TailCopies { get; set; }

    public byte[] RestartPage(ulong restartLsn, ulong oldestLsn, bool clean, ulong currentLsn)
    {
        var page = new byte[PageSize];
        Put32(page, 0, JournalUtilities.SignatureRstr);
        Put16(page, 4, 0x1E);
        Put16(page, 6, PageSize / 512 + 1);
        Put32(page, 0x10, PageSize);
        Put32(page, 0x14, PageSize);
        Put16(page, 0x18, 0x30);
        Put16(page, 0x1A, (ushort)(MajorVersion >= 2 ? 0 : 1));
        Put16(page, 0x1C, (ushort)MajorVersion);
        const int ra = 0x30;
        Put64(page, ra + 0x00, currentLsn);
        Put16(page, ra + 0x08, 1);
        Put16(page, ra + 0x0A, 0xFFFF);
        Put16(page, ra + 0x0C, 0);
        Put16(page, ra + 0x0E, (ushort)(clean ? 2 : 0));
        Put32(page, ra + 0x10, (uint)SequenceNumberBits);
        Put16(page, ra + 0x14, 0xD0);
        Put16(page, ra + 0x16, 0x40);
        Put64(page, ra + 0x18, (ulong)FileSize);
        Put16(page, ra + 0x24, HeaderLength);
        Put16(page, ra + 0x26, DataOffset);
        Put32(page, ra + 0x28, 7);
        const int client = ra + 0x40;
        Put64(page, client + 0x00, oldestLsn);
        Put64(page, client + 0x08, restartLsn);
        Put16(page, client + 0x10, 0xFFFF);
        Put16(page, client + 0x12, 0xFFFF);
        Put32(page, client + 0x1C, 8);
        Encoding.Unicode.GetBytes("NTFS").CopyTo(page, client + 0x20);
        JournalUtilities.ApplyFixups(page, 0, PageSize);
        return page;
    }

    /// <summary>Fills the whole $LogFile with 0xFF, like a freshly formatted or reset log.</summary>
    public void Erase()
    {
        var ff = new byte[PageSize];
        ff.AsSpan().Fill(0xFF);
        for (long offset = 0; offset < FileSize; offset += PageSize)
        {
            _storage.Write(offset, ff, 0, PageSize);
        }
    }

    public LogFileStorage Storage => _storage;

    /// <summary>An open attribute table entry in the layout of NTFS restart version 0 or 1 (also the redo data of OpenNonresidentAttribute).</summary>
    public static byte[] OpenAttributeEntryBytes(uint type, ulong fileReference, uint bytesPerIndex, uint ntfsVersion = 1)
    {
        var e = new byte[ntfsVersion == 0 ? 0x2C : 0x28];
        Put32(e, 0, 0xFFFFFFFF);
        if (ntfsVersion == 0)
        {
            Put64(e, 0x08, fileReference);
            Put32(e, 0x1C, type);
            Put32(e, 0x28, bytesPerIndex);
        }
        else
        {
            Put32(e, 0x04, bytesPerIndex);
            Put32(e, 0x08, type);
            Put64(e, 0x10, fileReference);
        }

        return e;
    }

    private static TestLogRecord Dump(NtfsLogOperation op, byte[] table) =>
        new() { Redo = op, Undo = NtfsLogOperation.Noop, TransactionId = 0x18, RedoData = table };

    private static byte[] Table(int entrySize, int entries)
    {
        var table = new byte[0x18 + (entrySize * entries)];
        Put16(table, 0x00, (ushort)entrySize);
        Put16(table, 0x02, (ushort)entries);
        Put16(table, 0x04, (ushort)entries);
        return table;
    }

    private byte[] Page(long offset)
    {
        if (!_pages.TryGetValue(offset, out var page))
        {
            page = new byte[PageSize];
            _pages[offset] = page;
            _pageInfo[offset] = (0, 0, DataOffset, false);
        }

        return page;
    }

    private void NextPage()
    {
        // The page the last byte went to (the position may already be at the start of the next one).
        var next = ((_position - 1) & ~(long)(PageSize - 1)) + PageSize;
        if (next >= FileSize)
        {
            next = FirstPageOffset;
            _sequence++;
        }

        _position = next + DataOffset;
    }

    private static void Put16(byte[] b, int o, ushort v) => EndianUtilities.WriteBytesLittleEndian(v, b, o);

    private static void Put16(byte[] b, int o, int v) => EndianUtilities.WriteBytesLittleEndian((ushort)v, b, o);

    private static void Put32(byte[] b, int o, uint v) => EndianUtilities.WriteBytesLittleEndian(v, b, o);

    private static void Put64(byte[] b, int o, ulong v) => EndianUtilities.WriteBytesLittleEndian(v, b, o);
}
