using System;
using System.Collections.Generic;
using System.Text;
using DiscUtils.Ntfs.Journal;
using DiscUtils.Streams;

namespace LibraryTests.Ntfs.Journal;

/// <summary>
/// The byte-level redo and undo actions, on file records and index buffers built here: every action applied and
/// then undone with the undo action NTFS logs for it must give back the original used bytes.
/// </summary>
public class NtfsLogActionsTest
{
    private const int RecordSize = 1024;
    private const int FirstAttribute = 0x38;

    #region builders

    private static byte[] Resident(uint type, byte[] value, ushort id, bool indexed = false)
    {
        var length = (0x18 + value.Length + 7) & ~7;
        var a = new byte[length];
        Put32(a, 0x00, type);
        Put32(a, 0x04, (uint)length);
        Put16(a, 0x0A, 0x18);
        Put16(a, 0x0E, id);
        Put32(a, 0x10, (uint)value.Length);
        Put16(a, 0x14, 0x18);
        a[0x16] = (byte)(indexed ? 1 : 0);
        value.CopyTo(a, 0x18);
        return a;
    }

    private static byte[] NonResident(byte[] runs, long startVcn, long lastVcn, ushort id, bool withTotalSize = false)
    {
        var runsOffset = withTotalSize ? 0x48 : 0x40;
        var length = (runsOffset + runs.Length + 7) & ~7;
        var a = new byte[length];
        Put32(a, 0x00, 0x80);
        Put32(a, 0x04, (uint)length);
        a[0x08] = 1;
        Put16(a, 0x0A, 0x40);
        Put16(a, 0x0E, id);
        Put64(a, 0x10, (ulong)startVcn);
        Put64(a, 0x18, (ulong)lastVcn);
        Put16(a, 0x20, (ushort)runsOffset);
        Put64(a, 0x28, 0x10000);
        Put64(a, 0x30, 0x8000);
        Put64(a, 0x38, 0x8000);
        runs.CopyTo(a, runsOffset);
        return a;
    }

    private static byte[] Entry(ulong fileReference, string name, long? childVcn = null)
    {
        var key = new byte[(0x42 + (2 * name.Length) + 7) & ~7];
        Put64(key, 0x00, 5);
        for (var i = 0x08; i < 0x40; i++)
        {
            key[i] = (byte)(i + name.Length);
        }

        key[0x40] = (byte)name.Length;
        Encoding.Unicode.GetBytes(name).CopyTo(key, 0x42);
        var length = 0x10 + key.Length + (childVcn.HasValue ? 8 : 0);
        var e = new byte[length];
        Put64(e, 0x00, fileReference);
        Put16(e, 0x08, (ushort)length);
        Put16(e, 0x0A, (ushort)key.Length);
        Put16(e, 0x0C, (ushort)(childVcn.HasValue ? 1 : 0));
        key.CopyTo(e, 0x10);
        if (childVcn.HasValue)
        {
            Put64(e, length - 8, (ulong)childVcn.Value);
        }

        return e;
    }

    private static byte[] LastEntry(long? childVcn = null)
    {
        var length = 0x10 + (childVcn.HasValue ? 8 : 0);
        var e = new byte[length];
        Put16(e, 0x08, (ushort)length);
        Put16(e, 0x0C, (ushort)(2 | (childVcn.HasValue ? 1 : 0)));
        if (childVcn.HasValue)
        {
            Put64(e, length - 8, (ulong)childVcn.Value);
        }

        return e;
    }

    private static byte[] ViewEntry(byte[] key, byte[] data)
    {
        var length = (0x10 + key.Length + data.Length + 7) & ~7;
        var e = new byte[length];
        Put16(e, 0x00, (ushort)(0x10 + key.Length));
        Put16(e, 0x02, (ushort)data.Length);
        Put16(e, 0x08, (ushort)length);
        Put16(e, 0x0A, (ushort)key.Length);
        key.CopyTo(e, 0x10);
        data.CopyTo(e, 0x10 + key.Length);
        return e;
    }

    private static byte[] IndexRoot(params byte[][] entries)
    {
        var size = 0;
        foreach (var e in entries)
        {
            size += e.Length;
        }

        var value = new byte[0x20 + size];
        Put32(value, 0x00, 0x30);
        Put32(value, 0x04, 1);
        Put32(value, 0x08, 4096);
        value[0x0C] = 1;
        Put32(value, 0x10, 0x10);
        Put32(value, 0x14, (uint)(0x10 + size));
        Put32(value, 0x18, (uint)(0x10 + size));
        var position = 0x20;
        foreach (var e in entries)
        {
            e.CopyTo(value, position);
            position += e.Length;
        }

        return Resident(0x90, value, 4);
    }

    /// <summary>A file record (logical form) holding the attributes in order, then the end marker.</summary>
    private static byte[] FileRecord(params byte[][] attributes)
    {
        var r = new byte[RecordSize];
        Put32(r, 0x00, JournalUtilities.SignatureFile);
        Put16(r, 0x04, 0x30);
        Put16(r, 0x06, 3);
        Put16(r, 0x10, 1);
        Put16(r, 0x12, 1);
        Put16(r, 0x14, FirstAttribute);
        Put16(r, 0x16, 1);
        Put32(r, 0x1C, RecordSize);
        Put16(r, 0x28, 8);
        var position = FirstAttribute;
        foreach (var a in attributes)
        {
            a.CopyTo(r, position);
            position += a.Length;
        }

        Put32(r, position, 0xFFFFFFFF);
        Put32(r, 0x18, (uint)(position + 8));
        return r;
    }

    private static byte[] IndexBuffer(params byte[][] entries)
    {
        var q = new byte[4096];
        Put32(q, 0x00, JournalUtilities.SignatureIndx);
        Put16(q, 0x04, 0x28);
        Put16(q, 0x06, 9);
        Put32(q, 0x18, 0x28);
        var position = 0x40;
        foreach (var e in entries)
        {
            e.CopyTo(q, position);
            position += e.Length;
        }

        Put32(q, 0x1C, (uint)(position - 0x18));
        Put32(q, 0x20, 4096 - 0x18);
        return q;
    }

    private static NtfsLogRecord Record(NtfsLogOperation redo, NtfsLogOperation undo, byte[] redoData, byte[] undoData,
        int recordOffset = 0, int attributeOffset = 0)
    {
        var test = new TestLogRecord
        {
            Redo = redo,
            Undo = undo,
            RedoData = redoData,
            UndoData = undoData,
            RecordOffset = recordOffset,
            AttributeOffset = attributeOffset,
            Lcns = [100],
        };
        var lfs = new LfsRecord { Lsn = 0x1000, RecordType = LfsRecord.TypeClientRecord, TransactionId = 0x18, ClientData = test.ClientData() };
        return NtfsLogRecord.TryParse(lfs, out var error) ?? throw new InvalidOperationException(error);
    }

    private static int Used(byte[] r) => (int)EndianUtilities.ToUInt32LittleEndian(r, 0x18);

    private static void AssertUsedEqual(byte[] expected, byte[] actual)
    {
        Assert.Equal(Used(expected), Used(actual));
        Assert.True(expected.AsSpan(0, Used(expected)).SequenceEqual(actual.AsSpan(0, Used(actual))));
    }

    private static int IndxUsedEnd(byte[] q) => 0x18 + (int)EndianUtilities.ToUInt32LittleEndian(q, 0x1C);

    private static void AssertIndxEqual(byte[] expected, byte[] actual)
    {
        Assert.Equal(IndxUsedEnd(expected), IndxUsedEnd(actual));
        Assert.True(expected.AsSpan(0, IndxUsedEnd(expected)).SequenceEqual(actual.AsSpan(0, IndxUsedEnd(actual))));
    }

    private static void Redo(NtfsLogRecord record, byte[] page) =>
        NtfsLogActions.ApplyToFileRecord(record.RedoOperation, record, page, record.RedoData);

    private static void Undo(NtfsLogRecord record, byte[] page) =>
        NtfsLogActions.ApplyToFileRecord(record.UndoOperation, record, page, record.UndoData);

    #endregion

    [Fact]
    public void CreateAndDeleteAttribute_RoundTrip_UpdateIdsAndHardLinks()
    {
        var si = Resident(0x10, new byte[0x48], 0);
        var data = Resident(0x80, [1, 2, 3], 1);
        var original = FileRecord(si, data);
        var fileName = Resident(0x30, new byte[0x44], 9, indexed: true);
        var create = Record(NtfsLogOperation.CreateAttribute, NtfsLogOperation.DeleteAttribute, fileName, [],
            recordOffset: FirstAttribute + si.Length);

        var page = (byte[])original.Clone();
        Redo(create, page);
        Assert.Equal(Used(original) + fileName.Length, Used(page));
        Assert.Equal(10, EndianUtilities.ToUInt16LittleEndian(page, 0x28));
        Assert.Equal(2, EndianUtilities.ToUInt16LittleEndian(page, 0x12));
        Assert.Equal(0x30u, EndianUtilities.ToUInt32LittleEndian(page, FirstAttribute + si.Length));

        Undo(create, page);
        Assert.Equal(1, EndianUtilities.ToUInt16LittleEndian(page, 0x12));
        Put16(page, 0x28, 8);
        AssertUsedEqual(original, page);
    }

    [Fact]
    public void CreateAttribute_AtTheEndMarker_Appends()
    {
        var original = FileRecord(Resident(0x10, new byte[0x48], 0));
        var data = Resident(0x80, [7, 7], 1);
        var create = Record(NtfsLogOperation.CreateAttribute, NtfsLogOperation.DeleteAttribute, data, [],
            recordOffset: Used(original) - 8);
        var page = (byte[])original.Clone();
        Redo(create, page);
        Assert.Equal(0xFFFFFFFFu, EndianUtilities.ToUInt32LittleEndian(page, Used(page) - 8));
        Assert.Equal(0x80u, EndianUtilities.ToUInt32LittleEndian(page, Used(original) - 8));
    }

    [Theory]
    [InlineData(new byte[] { 9, 9, 9 }, new byte[] { 1, 2, 3 })]
    [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 }, new byte[] { 1, 2, 3 })]
    [InlineData(new byte[] { 5 }, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 })]
    public void UpdateResidentValue_InPlaceGrowAndShrink_RoundTrip(byte[] newValue, byte[] oldValue)
    {
        var si = Resident(0x10, new byte[0x48], 0);
        var data = Resident(0x80, oldValue, 1);
        var after = Resident(0x30, new byte[0x44], 2, indexed: true);
        var original = FileRecord(si, data, after);
        var roff = FirstAttribute + si.Length;
        var update = Record(NtfsLogOperation.UpdateResidentValue, NtfsLogOperation.UpdateResidentValue, newValue, oldValue,
            recordOffset: roff, attributeOffset: 0x18);

        var page = (byte[])original.Clone();
        Redo(update, page);
        Assert.True(page.AsSpan(roff + 0x18, newValue.Length).SequenceEqual(newValue));
        if (newValue.Length != oldValue.Length)
        {
            Assert.Equal((uint)newValue.Length, EndianUtilities.ToUInt32LittleEndian(page, roff + 0x10));
        }

        // The attribute after it follows the new size.
        var next = roff + (int)EndianUtilities.ToUInt32LittleEndian(page, roff + 4);
        Assert.Equal(0x30u, EndianUtilities.ToUInt32LittleEndian(page, next));

        // Undone: the old value, length and layout. (Padding inside the attribute keeps bytes of the bigger
        // value, as on Windows; it is not part of the value.)
        Undo(update, page);
        Assert.Equal(Used(original), Used(page));
        Assert.Equal(EndianUtilities.ToUInt32LittleEndian(original, roff + 4), EndianUtilities.ToUInt32LittleEndian(page, roff + 4));
        Assert.Equal((uint)oldValue.Length, EndianUtilities.ToUInt32LittleEndian(page, roff + 0x10));
        Assert.True(page.AsSpan(roff + 0x18, oldValue.Length).SequenceEqual(oldValue));
        var end = roff + (int)EndianUtilities.ToUInt32LittleEndian(page, roff + 4);
        Assert.True(original.AsSpan(end, Used(original) - end).SequenceEqual(page.AsSpan(end, Used(page) - end)));
    }

    [Fact]
    public void UpdateMappingPairs_RewritesTheRunsAndTheLastVcn()
    {
        var si = Resident(0x10, new byte[0x48], 0);
        var oldRuns = new byte[] { 0x11, 0x04, 0x20, 0x00 };
        var data = NonResident(oldRuns, 0, 3, 1);
        var original = FileRecord(si, data);
        var roff = FirstAttribute + si.Length;

        // Two runs (4 + 44 clusters) and the terminator.
        var newRuns = new byte[] { 0x11, 0x04, 0x20, 0x21, 0x2C, 0x01, 0x40, 0x00 };
        var update = Record(NtfsLogOperation.UpdateMappingPairs, NtfsLogOperation.UpdateMappingPairs, newRuns, oldRuns,
            recordOffset: roff, attributeOffset: 0x40);
        var page = (byte[])original.Clone();
        Redo(update, page);
        Assert.Equal(47UL, EndianUtilities.ToUInt64LittleEndian(page, roff + 0x18));
        Assert.True(page.AsSpan(roff + 0x40, newRuns.Length).SequenceEqual(newRuns));

        // Undone: the old runs, last VCN and layout (bytes after the run list's terminator are not part of it).
        Undo(update, page);
        Assert.Equal(Used(original), Used(page));
        Assert.Equal(3UL, EndianUtilities.ToUInt64LittleEndian(page, roff + 0x18));
        Assert.True(page.AsSpan(roff + 0x40, oldRuns.Length).SequenceEqual(oldRuns));
        Assert.Equal(EndianUtilities.ToUInt32LittleEndian(original, roff + 4), EndianUtilities.ToUInt32LittleEndian(page, roff + 4));
    }

    [Fact]
    public void SetNewAttributeSizes_SetsAllocatedValidAndDataSize_AndTotalSizeOnlyWhenTheAttributeHasIt()
    {
        var si = Resident(0x10, new byte[0x48], 0);
        var runs = new byte[] { 0x11, 0x04, 0x20, 0x00 };
        foreach (var compressed in new[] { false, true })
        {
            var page = FileRecord(si, NonResident(runs, 0, 3, 1, withTotalSize: compressed));
            var roff = FirstAttribute + si.Length;
            var sizes = new byte[32];
            Put64(sizes, 0, 0x20000);
            Put64(sizes, 8, 0x1100);
            Put64(sizes, 16, 0x1234);
            Put64(sizes, 24, 0x9000);
            Redo(Record(NtfsLogOperation.SetNewAttributeSizes, NtfsLogOperation.SetNewAttributeSizes, sizes, sizes, recordOffset: roff), page);
            Assert.Equal(0x20000UL, EndianUtilities.ToUInt64LittleEndian(page, roff + 0x28));
            Assert.Equal(0x1234UL, EndianUtilities.ToUInt64LittleEndian(page, roff + 0x30));
            Assert.Equal(0x1100UL, EndianUtilities.ToUInt64LittleEndian(page, roff + 0x38));
            Assert.Equal(compressed ? 0x9000UL : 0x11UL, EndianUtilities.ToUInt64LittleEndian(page, roff + 0x40) & (compressed ? ~0UL : 0xFFUL));
        }
    }

    [Fact]
    public void WriteEndOfFileRecordSegment_ReplacesTheTailAndSetsTheUsedSize()
    {
        var si = Resident(0x10, new byte[0x48], 0);
        var page = FileRecord(si, Resident(0x80, new byte[100], 1));
        var tail = new byte[0x20];
        Put32(tail, 0x00, 0x80);
        Put32(tail, 0x04, 0x18);
        Put32(tail, 0x18, 0xFFFFFFFF);
        var roff = FirstAttribute + si.Length;
        Redo(Record(NtfsLogOperation.WriteEndOfFileRecordSegment, NtfsLogOperation.WriteEndOfFileRecordSegment, tail, [], recordOffset: roff), page);
        Assert.Equal(roff + 0x20, Used(page));
    }

    [Fact]
    public void InitializeDeallocateAndZeroEnd()
    {
        var image = FileRecord(Resident(0x10, new byte[0x48], 0));
        var page = new byte[RecordSize];
        page.AsSpan().Fill(0xAB);
        var init = Record(NtfsLogOperation.InitializeFileRecordSegment, NtfsLogOperation.DeallocateFileRecordSegment,
            image.AsSpan(0, Used(image)).ToArray(), []);
        Redo(init, page);
        AssertUsedEqual(image, page);

        Undo(init, page);
        Assert.Equal(0, EndianUtilities.ToUInt16LittleEndian(page, 0x16) & 1);
        Assert.Equal(2, EndianUtilities.ToUInt16LittleEndian(page, 0x10));

        Redo(Record(NtfsLogOperation.ZeroEndOfFileRecord, NtfsLogOperation.Noop, new byte[0x100], [], recordOffset: 0x300), page);
        Assert.True(AllAre(page.AsSpan(0x300, 0x100), 0));
    }

    [Fact]
    public void IndexRootEntries_AddDeleteSetVcnAndUpdate()
    {
        var si = Resident(0x10, new byte[0x48], 0);
        var first = Entry(0x0001000000000040, "a", childVcn: 2);
        var root = IndexRoot(first, LastEntry(childVcn: 5));
        var original = FileRecord(si, root, Resident(0x80, [1], 1));
        var roff = FirstAttribute + si.Length;
        var entriesStart = 0x18 + 0x20;
        var added = Entry(0x0001000000000041, "bb", childVcn: 3);

        var add = Record(NtfsLogOperation.AddIndexEntryRoot, NtfsLogOperation.DeleteIndexEntryRoot, added, [],
            recordOffset: roff, attributeOffset: entriesStart + first.Length);
        var page = (byte[])original.Clone();
        Redo(add, page);
        var attrLength = (int)EndianUtilities.ToUInt32LittleEndian(page, roff + 4);
        Assert.Equal(root.Length + added.Length, attrLength);
        Assert.Equal(0x80u, EndianUtilities.ToUInt32LittleEndian(page, roff + attrLength));

        var vcn = new byte[8];
        Put64(vcn, 0, 77);
        var setVcn = Record(NtfsLogOperation.SetIndexEntryVcnRoot, NtfsLogOperation.SetIndexEntryVcnRoot, vcn, [],
            recordOffset: roff, attributeOffset: entriesStart + first.Length);
        Redo(setVcn, page);
        Assert.Equal(77UL, EndianUtilities.ToUInt64LittleEndian(page, roff + entriesStart + first.Length + added.Length - 8));

        var info = new byte[0x38];
        info.AsSpan().Fill(0x5A);
        var update = Record(NtfsLogOperation.UpdateFileNameRoot, NtfsLogOperation.UpdateFileNameRoot, info, [],
            recordOffset: roff, attributeOffset: entriesStart);
        Redo(update, page);
        Assert.True(AllAre(page.AsSpan(roff + entriesStart + 0x18, 0x38), 0x5A));

        Undo(add, page);
        Assert.Equal(root.Length, (int)EndianUtilities.ToUInt32LittleEndian(page, roff + 4));
        Assert.Equal(Used(original), Used(page));
    }

    [Fact]
    public void IndexRoot_UpdateRecordData_OfAViewIndex()
    {
        var si = Resident(0x10, new byte[0x48], 0);
        var view = ViewEntry([1, 2, 3, 4], [9, 9, 9, 9, 9, 9, 9, 9]);
        var root = IndexRoot(view, LastEntry());
        var page = FileRecord(si, root);
        var roff = FirstAttribute + si.Length;
        var update = Record(NtfsLogOperation.UpdateRecordDataRoot, NtfsLogOperation.UpdateRecordDataRoot, [4, 4, 4, 4, 4, 4, 4, 4], [],
            recordOffset: roff, attributeOffset: 0x38);
        Redo(update, page);
        Assert.Equal(4, page[roff + 0x38 + 0x14]);
    }

    [Fact]
    public void IndexBufferEntries_AddDeleteWriteEndSetVcnAndUpdate()
    {
        var a = Entry(0x0001000000000040, "a");
        var b = Entry(0x0001000000000042, "c");
        var original = IndexBuffer(a, b, LastEntry());
        var added = Entry(0x0001000000000041, "b");
        var add = Record(NtfsLogOperation.AddIndexEntryAllocation, NtfsLogOperation.DeleteIndexEntryAllocation, added, [],
            attributeOffset: 0x40 + a.Length);
        var q = (byte[])original.Clone();
        NtfsLogActions.ApplyToIndexBuffer(add.RedoOperation, add, q, add.RedoData);
        Assert.Equal(IndxUsedEnd(original) + added.Length, IndxUsedEnd(q));
        Assert.Equal(0x0001000000000041UL, EndianUtilities.ToUInt64LittleEndian(q, 0x40 + a.Length));

        var info = new byte[0x38];
        info.AsSpan().Fill(0x33);
        var update = Record(NtfsLogOperation.UpdateFileNameAllocation, NtfsLogOperation.UpdateFileNameAllocation, info, [],
            attributeOffset: 0x40);
        NtfsLogActions.ApplyToIndexBuffer(update.RedoOperation, update, q, update.RedoData);
        Assert.Equal(0x33, q[0x40 + 0x18]);

        NtfsLogActions.ApplyToIndexBuffer(add.UndoOperation, add, q, add.UndoData);
        q.AsSpan(0x40 + 0x18, 0x38).Clear();
        original.AsSpan(0x40 + 0x18, 0x38).Clear();
        AssertIndxEqual(original, q);

        // A split: the end of the buffer is rewritten, and an entry points to the new child buffer.
        var tail = LastEntry(childVcn: 8);
        var writeEnd = Record(NtfsLogOperation.WriteEndOfIndexBuffer, NtfsLogOperation.WriteEndOfIndexBuffer, tail, [],
            attributeOffset: 0x40 + a.Length);
        NtfsLogActions.ApplyToIndexBuffer(writeEnd.RedoOperation, writeEnd, q, writeEnd.RedoData);
        Assert.Equal(0x40 + a.Length + tail.Length, IndxUsedEnd(q));

        var vcn = new byte[8];
        Put64(vcn, 0, 12);
        var setVcn = Record(NtfsLogOperation.SetIndexEntryVcnAllocation, NtfsLogOperation.SetIndexEntryVcnAllocation, vcn, [],
            attributeOffset: 0x40 + a.Length);
        NtfsLogActions.ApplyToIndexBuffer(setVcn.RedoOperation, setVcn, q, setVcn.RedoData);
        Assert.Equal(12UL, EndianUtilities.ToUInt64LittleEndian(q, 0x40 + a.Length + tail.Length - 8));
    }

    [Fact]
    public void IndexBuffer_UpdateRecordData_OfAViewIndex()
    {
        var view = ViewEntry([1, 2, 3, 4], [9, 9, 9, 9]);
        var q = IndexBuffer(view, LastEntry());
        var update = Record(NtfsLogOperation.UpdateRecordDataAllocation, NtfsLogOperation.UpdateRecordDataAllocation, [6, 6, 6, 6], [],
            attributeOffset: 0x40);
        NtfsLogActions.ApplyToIndexBuffer(update.RedoOperation, update, q, update.RedoData);
        Assert.Equal(6, q[0x40 + 0x14]);
    }

    [Fact]
    public void ChangeBits_SetsAndClearsRanges()
    {
        var bytes = new byte[4];
        NtfsLogActions.ChangeBits(bytes, 3, 10, set: true);
        Assert.Equal(new byte[] { 0xF8, 0x1F, 0, 0 }, bytes);
        NtfsLogActions.ChangeBits(bytes, 5, 4, set: false);
        Assert.Equal(new byte[] { 0x18, 0x1E, 0, 0 }, bytes);
    }

    public static IEnumerable<object[]> RefusedFileRecordActions()
    {
        var si = Resident(0x10, new byte[0x48], 0);
        var record = FileRecord(si, Resident(0x80, [1, 2], 1));
        var roff = FirstAttribute + si.Length;

        // No attribute starts there.
        yield return [(int)NtfsLogOperation.DeleteAttribute, record, new byte[0], roff + 8, 0];

        // The end marker is not an attribute to change.
        yield return [(int)NtfsLogOperation.UpdateResidentValue, record, new byte[] { 1 }, Used(record) - 8, 0x18];

        // The value update starts inside the attribute header.
        yield return [(int)NtfsLogOperation.UpdateResidentValue, record, new byte[] { 1 }, roff, 0x08];

        // An attribute that does not fit in the record.
        var huge = Resident(0x80, new byte[1000], 3);
        yield return [(int)NtfsLogOperation.CreateAttribute, record, huge, roff, 0];

        // An index operation on an attribute that is not an index root.
        yield return [(int)NtfsLogOperation.AddIndexEntryRoot, record, Entry(1, "x"), roff, 0x18];

        // New sizes for a resident attribute.
        yield return [(int)NtfsLogOperation.SetNewAttributeSizes, record, new byte[24], roff, 0];

        // An operation that does not apply to file records.
        yield return [(int)NtfsLogOperation.AddIndexEntryAllocation, record, new byte[16], roff, 0];
    }

    [Theory]
    [MemberData(nameof(RefusedFileRecordActions))]
    public void FileRecordActions_ThatDoNotFitTheRecord_AreRefused(int opCode, byte[] record, byte[] data, int roff, int aoff)
    {
        var op = (NtfsLogOperation)opCode;
        var page = (byte[])record.Clone();
        var log = Record(op, NtfsLogOperation.Noop, data, [], recordOffset: roff, attributeOffset: aoff);
        Assert.Throws<JournalException>(() => NtfsLogActions.ApplyToFileRecord(op, log, page, log.RedoData));
    }

    [Fact]
    public void IndexBufferActions_ThatDoNotFitTheBuffer_AreRefused()
    {
        var a = Entry(0x0001000000000040, "a");
        var q = IndexBuffer(a, LastEntry());

        // Not on an entry boundary.
        var misplaced = Record(NtfsLogOperation.DeleteIndexEntryAllocation, NtfsLogOperation.Noop, [], [], attributeOffset: 0x48);
        Assert.Throws<JournalException>(() => NtfsLogActions.ApplyToIndexBuffer(misplaced.RedoOperation, misplaced, q, misplaced.RedoData));

        // Past the used part.
        var beyond = Record(NtfsLogOperation.SetIndexEntryVcnAllocation, NtfsLogOperation.Noop, new byte[8], [], attributeOffset: 0x800);
        Assert.Throws<JournalException>(() => NtfsLogActions.ApplyToIndexBuffer(beyond.RedoOperation, beyond, q, beyond.RedoData));

        // An entry that does not fit any more.
        var full = (byte[])q.Clone();
        Put32(full, 0x20, (uint)(IndxUsedEnd(full) - 0x18));
        var add = Record(NtfsLogOperation.AddIndexEntryAllocation, NtfsLogOperation.Noop, Entry(2, "b"), [], attributeOffset: 0x40);
        Assert.Throws<JournalException>(() => NtfsLogActions.ApplyToIndexBuffer(add.RedoOperation, add, full, add.RedoData));

        // A file record operation.
        var wrong = Record(NtfsLogOperation.CreateAttribute, NtfsLogOperation.Noop, [], [], attributeOffset: 0x40);
        Assert.Throws<JournalException>(() => NtfsLogActions.ApplyToIndexBuffer(wrong.RedoOperation, wrong, q, wrong.RedoData));
    }

    [Fact]
    public void RecordData_LeftOutOfTheRecord_CountsAsZeros()
    {
        var test = new TestLogRecord
        {
            Redo = NtfsLogOperation.UpdateResidentValue,
            Undo = NtfsLogOperation.UpdateResidentValue,
            RedoData = [],
            RedoLength = 12,
            UndoData = [],
            UndoLength = 0,
            Lcns = [1],
        };
        var lfs = new LfsRecord { Lsn = 1, RecordType = LfsRecord.TypeClientRecord, TransactionId = 0x18, ClientData = test.ClientData() };
        var record = NtfsLogRecord.TryParse(lfs, out _);
        Assert.Equal(12, record.RedoData.Count);
        Assert.True(AllAre(record.RedoData.AsSpan(), 0));
    }

    [Fact]
    public void Records_WithMisplacedData_AreRejected()
    {
        var test = new TestLogRecord { Redo = NtfsLogOperation.Noop, Undo = NtfsLogOperation.Noop, RedoData = [1, 2, 3, 4, 5, 6, 7, 8] };
        var data = test.ClientData();
        Put16(data, 0x04, 0x22);
        var lfs = new LfsRecord { Lsn = 1, RecordType = LfsRecord.TypeClientRecord, TransactionId = 0x18, ClientData = data };
        Assert.Null(NtfsLogRecord.TryParse(lfs, out var error));
        Assert.NotNull(error);

        var tooShort = new LfsRecord { Lsn = 1, RecordType = LfsRecord.TypeClientRecord, ClientData = new byte[8] };
        Assert.Null(NtfsLogRecord.TryParse(tooShort, out _));

        var lcns = new byte[0x20];
        Put16(lcns, 0x0E, 3);
        Assert.Null(NtfsLogRecord.TryParse(new LfsRecord { Lsn = 1, ClientData = lcns }, out _));
    }

    private static bool AllAre(ReadOnlySpan<byte> bytes, byte value)
    {
        foreach (var b in bytes)
        {
            if (b != value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AllAre(ArraySegment<byte> bytes, byte value) => AllAre(bytes.AsSpan(), value);

    private static void Put16(byte[] b, int o, ushort v) => EndianUtilities.WriteBytesLittleEndian(v, b, o);

    private static void Put32(byte[] b, int o, uint v) => EndianUtilities.WriteBytesLittleEndian(v, b, o);

    private static void Put64(byte[] b, int o, ulong v) => EndianUtilities.WriteBytesLittleEndian(v, b, o);
}
