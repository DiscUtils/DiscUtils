//
// Copyright (c) 2008-2011, Kenneth Bell
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
using System.Text;
using DiscUtils;
using DiscUtils.Core.WindowsSecurity.AccessControl;
using DiscUtils.Ntfs;
using DiscUtils.Streams;

namespace LibraryTests.Ntfs;

/// <summary>
/// $Secure:$SDS keeps its records in 256 KB blocks, and every block is mirrored in the block after it.
/// Windows chkdsk compares each block with its mirror byte for byte, including the zero padding that
/// aligns every record to 16 bytes and the unused rest of a full block. Space past the last record can
/// hold stale data in the primary block (Windows leaves it there), so adding a record has to write the
/// padding in front of it to both copies.
/// </summary>
public class SecurityDescriptorsTest
{
    private const int BlockSize = 0x40000;
    private const string SdsPath = "$Secure:$SDS";

    [Fact]
    public void AddDescriptor_WritesTheAlignmentPaddingToBothCopies()
    {
        using var disk = new SparseMemoryStream();
        var nextFile = 0;

        using (var ntfs = Format(disk))
        {
            // Make sure the last record does not end on a 16-byte boundary, so the next record has padding in front of it.
            while (LastRecordEnd(ReadSds(ntfs)) % 16 == 0)
            {
                AddFileWithNewDescriptor(ntfs, ref nextFile, aceCount: 1);
            }
        }

        // Stale data after the last record in the primary block, as Windows leaves it.
        var lastEnd = PlantStaleBytes(disk, length: 64);

        using (var ntfs = new NtfsFileSystem(disk))
        {
            AddFileWithNewDescriptor(ntfs, ref nextFile, aceCount: 1);

            var sds = ReadSds(ntfs);
            var newRecord = MathUtilities.RoundUp(lastEnd, 16);

            Assert.Equal(newRecord, Records(sds).Last().Offset);
            AssertBlocksMatchTheirMirrors(sds);
            AssertZeros(sds, lastEnd, newRecord);
        }
    }

    [Fact]
    public void AddDescriptor_ThatMovesToTheNextBlockPair_ZeroesTheRestOfTheFullBlock()
    {
        using var disk = new SparseMemoryStream();
        var nextFile = 0;
        const int bigAceCount = 1500; // about 54 KB per record, so a 256 KB block holds four

        using (var ntfs = Format(disk))
        {
            // Fill the first block until the next big record no longer fits.
            while (true)
            {
                var sds = ReadSds(ntfs);
                var next = MathUtilities.RoundUp(LastRecordEnd(sds), 16);
                if (next + BigRecordSize(bigAceCount) >= BlockSize)
                {
                    break;
                }

                AddFileWithNewDescriptor(ntfs, ref nextFile, bigAceCount);
            }
        }

        var lastEnd = PlantStaleBytes(disk, length: BlockSize); // up to the end of the primary block

        using (var ntfs = new NtfsFileSystem(disk))
        {
            AddFileWithNewDescriptor(ntfs, ref nextFile, bigAceCount);

            var sds = ReadSds(ntfs);

            Assert.Equal(2L * BlockSize, Records(sds).Last().Offset);
            AssertBlocksMatchTheirMirrors(sds);
            AssertZeros(sds, lastEnd, BlockSize);
        }
    }

    [Fact]
    public void AddDescriptor_KeepsWorkingAfterRemount()
    {
        using var disk = new SparseMemoryStream();
        var nextFile = 0;
        var descriptors = new Dictionary<string, string>();

        for (var mount = 0; mount < 3; mount++)
        {
            using var ntfs = mount == 0 ? Format(disk) : new NtfsFileSystem(disk);

            for (var i = 0; i < 5; i++)
            {
                var (path, sddl) = AddFileWithNewDescriptor(ntfs, ref nextFile, aceCount: 1 + i);
                descriptors.Add(path, sddl);
            }

            AssertBlocksMatchTheirMirrors(ReadSds(ntfs));

            foreach (var entry in descriptors)
            {
                Assert.Equal(entry.Value, ntfs.GetSecurity(entry.Key).GetSddlForm(AccessControlSections.All));
            }
        }
    }

    private static NtfsFileSystem Format(Stream disk)
    {
        var geometry = Geometry.FromCapacity(64 * 1024 * 1024);
        return NtfsFileSystem.Format(disk, "", geometry, 0, geometry.TotalSectorsLong);
    }

    private static (string Path, string Sddl) AddFileWithNewDescriptor(NtfsFileSystem ntfs, ref int nextFile, int aceCount)
    {
        var id = nextFile++;
        var sddl = new StringBuilder("O:BAG:BAD:");
        for (var i = 0; i < aceCount; i++)
        {
            sddl.Append($"(A;;FA;;;S-1-5-21-{1000 + id}-{i}-3-4)");
        }

        var path = $"file{id}";
        using (ntfs.OpenFile(path, FileMode.Create, FileAccess.ReadWrite))
        {
        }

        var descriptor = new RawSecurityDescriptor(sddl.ToString());
        ntfs.SetSecurity(path, descriptor);
        return (path, descriptor.GetSddlForm(AccessControlSections.All));
    }

    private static int BigRecordSize(int aceCount)
    {
        // Header (0x14) + SD header (0x14) + owner and group (BA, 16 bytes each) + ACL header (8) + ACEs (8 + 28-byte SID).
        return 0x14 + 0x14 + 16 + 16 + 8 + aceCount * 36;
    }

    private static byte[] ReadSds(NtfsFileSystem ntfs)
    {
        using var stream = ntfs.OpenFile(SdsPath, FileMode.Open, FileAccess.Read);
        var data = new byte[stream.Length];
        stream.ReadExactly(data, 0, data.Length);
        return data;
    }

    /// <summary>Writes 0xFF after the last record in its primary block (never into the mirror) and returns where the record ends.</summary>
    private static long PlantStaleBytes(Stream disk, int length)
    {
        using var ntfs = new NtfsFileSystem(disk);
        var lastEnd = LastRecordEnd(ReadSds(ntfs));
        var blockEnd = (lastEnd / (2L * BlockSize) * (2L * BlockSize)) + BlockSize;
        var count = (int)Math.Min(length, blockEnd - lastEnd);

        using var stream = ntfs.OpenFile(SdsPath, FileMode.Open, FileAccess.ReadWrite);
        stream.Position = lastEnd;
        stream.Write(Enumerable.Repeat((byte)0xFF, count).ToArray(), 0, count);
        return lastEnd;
    }

    private static List<(long Offset, int Length)> Records(byte[] sds)
    {
        var records = new List<(long Offset, int Length)>();

        for (long blockStart = 0; blockStart < sds.Length; blockStart += 2L * BlockSize)
        {
            var pos = blockStart;
            var blockEnd = Math.Min(blockStart + BlockSize, sds.Length);

            while (pos + 0x14 <= blockEnd)
            {
                var offset = EndianUtilities.ToInt64LittleEndian(sds, (int)pos + 0x08);
                var length = EndianUtilities.ToInt32LittleEndian(sds, (int)pos + 0x10);
                if (offset != pos || length < 0x14 || pos + length > blockEnd)
                {
                    break;
                }

                records.Add((pos, length));
                pos = MathUtilities.RoundUp(pos + length, 16);
            }
        }

        return records;
    }

    private static long LastRecordEnd(byte[] sds)
    {
        var last = Records(sds).Last();
        return last.Offset + last.Length;
    }

    private static void AssertZeros(byte[] sds, long start, long end)
    {
        for (var i = start; i < end; i++)
        {
            if (sds[i] != 0)
            {
                Assert.Fail($"$SDS padding at 0x{i:X} is 0x{sds[i]:X2}, expected zero (0x{start:X}-0x{end:X} should be zero)");
            }
        }
    }

    /// <summary>What chkdsk checks: every block equals its mirror, as far as the stream holds the mirror.</summary>
    private static void AssertBlocksMatchTheirMirrors(byte[] sds)
    {
        for (var blockStart = 0; blockStart + BlockSize < sds.Length; blockStart += 2 * BlockSize)
        {
            var mirrorLength = Math.Min(BlockSize, sds.Length - (blockStart + BlockSize));
            var primary = sds.AsSpan(blockStart, mirrorLength);
            var mirror = sds.AsSpan(blockStart + BlockSize, mirrorLength);

            for (var i = 0; i < mirrorLength; i++)
            {
                if (primary[i] != mirror[i])
                {
                    Assert.Fail($"$SDS block at 0x{blockStart:X} differs from its mirror at 0x{blockStart + i:X}: 0x{primary[i]:X2} vs 0x{mirror[i]:X2}");
                }
            }
        }
    }
}
