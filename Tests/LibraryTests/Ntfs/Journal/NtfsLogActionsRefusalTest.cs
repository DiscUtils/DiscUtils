using System;
using System.Collections.Generic;
using System.Linq;
using DiscUtils.Ntfs.Journal;
using DiscUtils.Streams;

namespace LibraryTests.Ntfs.Journal;

/// <summary>
/// Every check an action makes on the page before changing it: each case breaks one of them, and the action must
/// refuse instead of writing.
/// </summary>
public partial class NtfsLogActionsTest
{
    private static readonly Dictionary<string, Func<(NtfsLogOperation Op, byte[] Page, NtfsLogRecord Record, bool IndexBuffer)>> Malformed = new()
    {
        // Index buffers
        ["delete: entry of length 0"] = () =>
        {
            var a = Entry(1, "a");
            var q = IndexBuffer(a, LastEntry());
            Put16(q, 0x40 + a.Length + 8, 0);
            return (NtfsLogOperation.DeleteIndexEntryAllocation, q, Record(NtfsLogOperation.DeleteIndexEntryAllocation, NtfsLogOperation.Noop, [], [], attributeOffset: 0x40 + a.Length), true);
        },
        ["delete: entry past the used part"] = () =>
        {
            var a = Entry(1, "a");
            var q = IndexBuffer(a, LastEntry());
            Put16(q, 0x40 + a.Length + 8, 0x800);
            return (NtfsLogOperation.DeleteIndexEntryAllocation, q, Record(NtfsLogOperation.DeleteIndexEntryAllocation, NtfsLogOperation.Noop, [], [], attributeOffset: 0x40 + a.Length), true);
        },
        ["write end: past the allocated size"] = () => (NtfsLogOperation.WriteEndOfIndexBuffer, IndexBuffer(LastEntry()),
            Record(NtfsLogOperation.WriteEndOfIndexBuffer, NtfsLogOperation.Noop, new byte[4096], [], attributeOffset: 0x40), true),
        ["write end: past the buffer"] = () =>
        {
            var q = IndexBuffer(LastEntry());
            Put32(q, 0x20, 0x10000);
            return (NtfsLogOperation.WriteEndOfIndexBuffer, q, Record(NtfsLogOperation.WriteEndOfIndexBuffer, NtfsLogOperation.Noop, new byte[4096], [], attributeOffset: 0x40), true);
        },
        ["set vcn: data too short"] = () => (NtfsLogOperation.SetIndexEntryVcnAllocation, IndexBuffer(Entry(1, "a", 3), LastEntry()),
            Record(NtfsLogOperation.SetIndexEntryVcnAllocation, NtfsLogOperation.Noop, new byte[4], [], attributeOffset: 0x40), true),
        ["set vcn: entry too short"] = () => (NtfsLogOperation.SetIndexEntryVcnAllocation, IndexBuffer(LastEntry()),
            Record(NtfsLogOperation.SetIndexEntryVcnAllocation, NtfsLogOperation.Noop, new byte[8], [], attributeOffset: 0x40), true),
        ["set vcn: entry past the page"] = () =>
        {
            var q = IndexBuffer(Entry(1, "a", 3), LastEntry());
            Put16(q, 0x40 + 8, 0xFF8);
            return (NtfsLogOperation.SetIndexEntryVcnAllocation, q, Record(NtfsLogOperation.SetIndexEntryVcnAllocation, NtfsLogOperation.Noop, new byte[8], [], attributeOffset: 0x40), true);
        },
        ["file name: entry too short"] = () => (NtfsLogOperation.UpdateFileNameAllocation, IndexBuffer(LastEntry()),
            Record(NtfsLogOperation.UpdateFileNameAllocation, NtfsLogOperation.Noop, new byte[0x38], [], attributeOffset: 0x40), true),
        ["file name: entry past the page"] = () =>
        {
            var q = IndexBuffer(Entry(1, "a"), LastEntry());
            Put16(q, 0x40 + 8, 0xFF8);
            return (NtfsLogOperation.UpdateFileNameAllocation, q, Record(NtfsLogOperation.UpdateFileNameAllocation, NtfsLogOperation.Noop, new byte[0x38], [], attributeOffset: 0x40), true);
        },
        ["record data: past the entry"] = () => (NtfsLogOperation.UpdateRecordDataAllocation, IndexBuffer(ViewEntry([1, 2, 3, 4], [9, 9, 9, 9]), LastEntry()),
            Record(NtfsLogOperation.UpdateRecordDataAllocation, NtfsLogOperation.Noop, new byte[64], [], attributeOffset: 0x40), true),
        ["record data: entry past the page"] = () =>
        {
            var q = IndexBuffer(ViewEntry([1, 2, 3, 4], [9, 9, 9, 9]), LastEntry());
            Put16(q, 0x40 + 8, 0xFF8);
            return (NtfsLogOperation.UpdateRecordDataAllocation, q, Record(NtfsLogOperation.UpdateRecordDataAllocation, NtfsLogOperation.Noop, [1], [], attributeOffset: 0x40), true);
        },
        ["buffer header: entries before the header"] = () =>
        {
            var q = IndexBuffer(LastEntry());
            Put32(q, 0x18, 0);
            return (NtfsLogOperation.DeleteIndexEntryAllocation, q, Record(NtfsLogOperation.DeleteIndexEntryAllocation, NtfsLogOperation.Noop, [], [], attributeOffset: 0x40), true);
        },
        ["buffer header: used past the page"] = () =>
        {
            var q = IndexBuffer(LastEntry());
            Put32(q, 0x1C, 0x2000);
            return (NtfsLogOperation.DeleteIndexEntryAllocation, q, Record(NtfsLogOperation.DeleteIndexEntryAllocation, NtfsLogOperation.Noop, [], [], attributeOffset: 0x40), true);
        },
        ["add: entry shorter than a header"] = () => (NtfsLogOperation.AddIndexEntryAllocation, IndexBuffer(LastEntry()), AddWithLength(0x08), true),
        ["add: entry longer than its data"] = () => (NtfsLogOperation.AddIndexEntryAllocation, IndexBuffer(LastEntry()), AddWithLength(0x100), true),
        ["add: entry length not aligned"] = () => (NtfsLogOperation.AddIndexEntryAllocation, IndexBuffer(LastEntry()), AddWithLength(0x1C), true),

        // File records
        ["create: length shorter than a header"] = () => (NtfsLogOperation.CreateAttribute, FileRecord(Si()), CreateWithLength(0x10, 0x20), false),
        ["create: length not aligned"] = () => (NtfsLogOperation.CreateAttribute, FileRecord(Si()), CreateWithLength(0x1C, 0x20), false),
        ["create: length longer than the data"] = () => (NtfsLogOperation.CreateAttribute, FileRecord(Si()), CreateWithLength(0x100, 0x20), false),
        ["mapping pairs: before the run list"] = () => (NtfsLogOperation.UpdateMappingPairs, FileRecord(Si(), Runs()), MappingPairs([0x11, 4, 0x20, 0], 0x10), false),
        ["mapping pairs: past the attribute"] = () => (NtfsLogOperation.UpdateMappingPairs, FileRecord(Si(), Runs()), MappingPairs([0x11, 4, 0x20, 0], 0x200), false),
        ["mapping pairs: a run with no length"] = () => (NtfsLogOperation.UpdateMappingPairs, FileRecord(Si(), Runs()), MappingPairs([0x10, 0x20, 0], 0x40), false),
        ["mapping pairs: a run length of nine bytes"] = () => (NtfsLogOperation.UpdateMappingPairs, FileRecord(Si(), Runs()), MappingPairs([0x19, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0], 0x40), false),
        ["mapping pairs: a run offset of nine bytes"] = () => (NtfsLogOperation.UpdateMappingPairs, FileRecord(Si(), Runs()), MappingPairs([0x91, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0], 0x40), false),
        ["mapping pairs: a run past the attribute"] = () => (NtfsLogOperation.UpdateMappingPairs, FileRecord(Si(), Runs()), MappingPairs([0x11, 4, 0x20, 0x11, 4, 0x20, 0x11, 1], 0x40), false),
        ["root: delete an entry of length 0"] = () =>
        {
            var r = FileRecord(Si(), IndexRoot(Entry(1, "a"), LastEntry()));
            Put16(r, 0x98 + 0x38 + 8, 0);
            return (NtfsLogOperation.DeleteIndexEntryRoot, r, Record(NtfsLogOperation.DeleteIndexEntryRoot, NtfsLogOperation.Noop, [], [], recordOffset: 0x98, attributeOffset: 0x38), false);
        },
        ["root: delete an entry past the used part"] = () =>
        {
            var r = FileRecord(Si(), IndexRoot(Entry(1, "a"), LastEntry()));
            Put16(r, 0x98 + 0x38 + 8, 0x300);
            return (NtfsLogOperation.DeleteIndexEntryRoot, r, Record(NtfsLogOperation.DeleteIndexEntryRoot, NtfsLogOperation.Noop, [], [], recordOffset: 0x98, attributeOffset: 0x38), false);
        },
        ["root header: entries before the header"] = () =>
        {
            var r = FileRecord(Si(), IndexRoot(LastEntry()));
            Put32(r, 0x98 + 0x28, 0);
            return (NtfsLogOperation.SetIndexEntryVcnRoot, r, Record(NtfsLogOperation.SetIndexEntryVcnRoot, NtfsLogOperation.Noop, new byte[8], [], recordOffset: 0x98, attributeOffset: 0x38), false);
        },
        ["root header: used past the attribute"] = () =>
        {
            var r = FileRecord(Si(), IndexRoot(LastEntry()));
            Put32(r, 0x98 + 0x2C, 0x400);
            return (NtfsLogOperation.SetIndexEntryVcnRoot, r, Record(NtfsLogOperation.SetIndexEntryVcnRoot, NtfsLogOperation.Noop, new byte[8], [], recordOffset: 0x98, attributeOffset: 0x38), false);
        },
        ["record header: used past the record"] = () =>
        {
            var r = FileRecord(Si());
            Put32(r, 0x18, 0x500);
            return (NtfsLogOperation.DeleteAttribute, r, Record(NtfsLogOperation.DeleteAttribute, NtfsLogOperation.Noop, [], [], recordOffset: 0x38), false);
        },
        ["record header: attributes start in the header"] = () =>
        {
            var r = FileRecord(Si());
            Put16(r, 0x14, 0x10);
            return (NtfsLogOperation.DeleteAttribute, r, Record(NtfsLogOperation.DeleteAttribute, NtfsLogOperation.Noop, [], [], recordOffset: 0x38), false);
        },
        ["record header: attributes start past the used part"] = () =>
        {
            var r = FileRecord(Si());
            Put16(r, 0x14, 0x300);
            return (NtfsLogOperation.DeleteAttribute, r, Record(NtfsLogOperation.DeleteAttribute, NtfsLogOperation.Noop, [], [], recordOffset: 0x38), false);
        },
        ["attribute list: an attribute shorter than a header"] = () =>
        {
            var r = FileRecord(Si(), Resident(0x80, [1], 1));
            Put32(r, 0x38 + 4, 0x10);
            return (NtfsLogOperation.DeleteAttribute, r, Record(NtfsLogOperation.DeleteAttribute, NtfsLogOperation.Noop, [], [], recordOffset: 0x98), false);
        },
        ["attribute list: an attribute past the used part"] = () =>
        {
            var r = FileRecord(Si(), Resident(0x80, [1], 1));
            Put32(r, 0x38 + 4, 0x300);
            return (NtfsLogOperation.DeleteAttribute, r, Record(NtfsLogOperation.DeleteAttribute, NtfsLogOperation.Noop, [], [], recordOffset: 0x98), false);
        },
        ["attribute: the changed one is shorter than a header"] = () =>
        {
            var r = FileRecord(Si(), Resident(0x80, [1], 1));
            Put32(r, 0x98 + 4, 0x10);
            return (NtfsLogOperation.DeleteAttribute, r, Record(NtfsLogOperation.DeleteAttribute, NtfsLogOperation.Noop, [], [], recordOffset: 0x98), false);
        },
        ["attribute: the changed one runs past the used part"] = () =>
        {
            var r = FileRecord(Si(), Resident(0x80, [1], 1));
            Put32(r, 0x98 + 4, 0x300);
            return (NtfsLogOperation.DeleteAttribute, r, Record(NtfsLogOperation.DeleteAttribute, NtfsLogOperation.Noop, [], [], recordOffset: 0x98), false);
        },
    };

    public static IEnumerable<object[]> MalformedCases => Malformed.Keys.Select(k => new object[] { k });

    private static byte[] Si() => Resident(0x10, new byte[0x48], 0);

    private static byte[] Runs() => NonResident([0x11, 0x04, 0x20, 0x00], 0, 3, 1);

    private static NtfsLogRecord AddWithLength(int length)
    {
        var entry = Entry(2, "b");
        Put16(entry, 8, (ushort)length);
        return Record(NtfsLogOperation.AddIndexEntryAllocation, NtfsLogOperation.Noop, entry, [], attributeOffset: 0x40);
    }

    private static NtfsLogRecord CreateWithLength(int length, int dataLength)
    {
        var data = new byte[dataLength];
        Put32(data, 0, 0x80);
        Put32(data, 4, (uint)length);
        return Record(NtfsLogOperation.CreateAttribute, NtfsLogOperation.Noop, data, [], recordOffset: 0x98);
    }

    private static NtfsLogRecord MappingPairs(byte[] runs, int attributeOffset) =>
        Record(NtfsLogOperation.UpdateMappingPairs, NtfsLogOperation.Noop, runs, [], recordOffset: 0x98, attributeOffset: attributeOffset);

    [Theory]
    [MemberData(nameof(MalformedCases))]
    public void ActionsOnPagesThatDoNotMatchTheRecord_AreRefused(string name)
    {
        var (op, page, record, indexBuffer) = Malformed[name]();
        var before = (byte[])page.Clone();
        Assert.Throws<JournalException>(() =>
        {
            if (indexBuffer)
            {
                NtfsLogActions.ApplyToIndexBuffer(op, record, page, record.RedoData);
            }
            else
            {
                NtfsLogActions.ApplyToFileRecord(op, record, page, record.RedoData);
            }
        });

        // Checks come before changes, apart from a run list found to be damaged after it was written.
        if (op != NtfsLogOperation.UpdateMappingPairs)
        {
            Assert.True(before.AsSpan().SequenceEqual(page), name);
        }
    }

    [Fact]
    public void ARunListEndingAtTheAttributeEnd_NeedsNoTerminator()
    {
        var page = FileRecord(Si(), Runs());
        var runs = new byte[] { 0x11, 4, 0x20, 0x11, 4, 0x20, 0x01, 0x01 };
        var record = MappingPairs(runs, 0x40);
        NtfsLogActions.ApplyToFileRecord(record.RedoOperation, record, page, record.RedoData);
        Assert.Equal(8UL, EndianUtilities.ToUInt64LittleEndian(page, 0x98 + 0x18));
    }
}
