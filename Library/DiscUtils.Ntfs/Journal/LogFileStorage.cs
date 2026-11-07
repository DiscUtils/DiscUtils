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
using System.Linq;
using static DiscUtils.Ntfs.Journal.JournalUtilities;

namespace DiscUtils.Ntfs.Journal;

/// <summary>
/// Byte access to the $LogFile data on the raw volume, located through MFT record 2 before the file system is
/// mounted. $LogFile's own record is written at format time only, so it is never stale.
/// </summary>
internal sealed class LogFileStorage
{
    private const int LogFileRecordNumber = 2;
    private const uint AttributeList = 0x20;
    private const uint AttributeData = 0x80;
    private const uint AttributeEnd = 0xFFFFFFFF;

    private readonly Stream _volume;
    private readonly int _clusterSize;
    private readonly List<(long Vcn, long Lcn, long Length)> _extents;

    private LogFileStorage(Stream volume, int clusterSize, List<(long, long, long)> extents, long length)
    {
        _volume = volume;
        _clusterSize = clusterSize;
        _extents = extents;
        Length = length;
    }

    /// <summary>Gets the size of the $LogFile data in bytes.</summary>
    public long Length { get; }

    public static LogFileStorage Open(Stream volume, JournalVolumeLayout layout)
    {
        var record = ReadLogFileRecord(volume, layout, layout.MftCluster)
                     ?? ReadLogFileRecord(volume, layout, layout.MftMirrorCluster)
                     ?? throw new JournalException("Neither $MFT nor $MFTMirr holds a valid record for $LogFile");

        var (extents, length) = ReadDataAttribute(record, layout, "$LogFile");
        var mftPrefixClusters = (4L * layout.MftRecordSize + layout.BytesPerCluster - 1) / layout.BytesPerCluster;
        foreach (var extent in extents)
        {
            if (Overlaps(extent, layout.MftCluster, mftPrefixClusters) ||
                (layout.ContainsClusters(layout.MftMirrorCluster, mftPrefixClusters) &&
                 Overlaps(extent, layout.MftMirrorCluster, mftPrefixClusters)))
            {
                throw new JournalException("$LogFile overlaps $MFT or $MFTMirr");
            }
        }

        return new LogFileStorage(volume, layout.BytesPerCluster, extents, length);
    }

    private static bool Overlaps((long Vcn, long Lcn, long Length) extent, long start, long count) =>
        extent.Lcn < start + count && start < extent.Lcn + extent.Length;

    public bool ContainsCluster(long lcn) => _extents.Any(extent => lcn >= extent.Lcn && lcn - extent.Lcn < extent.Length);

    // Both $LogFile and the supported $MFTMirr layout can be checked from a single base record.
    internal static (List<(long Vcn, long Lcn, long Length)> Extents, long Length) ReadDataAttribute(
        byte[] record, JournalVolumeLayout layout, string fileName)
    {
        var used = U32(record, 0x18);
        var firstAttribute = U16(record, 0x14);
        if (U64(record, 0x20) != 0 || used > record.Length || firstAttribute < 0x2A || used < firstAttribute + 4)
        {
            throw new JournalException($"{fileName} has a damaged base file record");
        }

        var data = -1;
        var dataAttributeLength = 0;
        var end = (int)used;
        for (var offset = (int)firstAttribute; offset <= end - 4;)
        {
            var type = U32(record, offset);
            if (type == AttributeEnd)
            {
                if (data < 0)
                {
                    throw new JournalException($"{fileName} has no unnamed data attribute");
                }

                return ReadNonResidentData(record, data, dataAttributeLength, layout, fileName);
            }

            if (offset > end - 8)
            {
                break;
            }

            var length = U32(record, offset + 4);
            if (length < 0x18 || length > end - offset || (length & 7) != 0)
            {
                break;
            }

            if (type == AttributeList)
            {
                throw new JournalException($"{fileName} has an $ATTRIBUTE_LIST, which DiscUtils does not support for replay");
            }

            if (type == AttributeData && record[offset + 9] == 0)
            {
                if (data >= 0)
                {
                    throw new JournalException($"{fileName} has split unnamed data, which DiscUtils does not support for replay");
                }

                data = offset;
                dataAttributeLength = (int)length;
            }

            offset += (int)length;
        }

        throw new JournalException($"{fileName} has a damaged attribute list in its base file record");
    }

    private static (List<(long Vcn, long Lcn, long Length)> Extents, long Length) ReadNonResidentData(
        byte[] record, int offset, int length, JournalVolumeLayout layout, string fileName)
    {
        if (record[offset + 8] != 1 || length < 0x40)
        {
            throw new JournalException($"{fileName} does not have a complete non-resident data header");
        }

        if (U64(record, offset + 0x10) != 0)
        {
            throw new JournalException($"{fileName} has a nonzero starting VCN, which DiscUtils does not support for replay");
        }

        if (U16(record, offset + 0x0C) != 0 || U16(record, offset + 0x22) != 0)
        {
            throw new JournalException($"{fileName} has unsupported data attribute flags or compression");
        }

        var runsOffset = U16(record, offset + 0x20);
        if (runsOffset < 0x40 || runsOffset >= length)
        {
            throw new JournalException($"{fileName} has an invalid mapping pairs offset");
        }

        var allocated = (long)U64(record, offset + 0x28);
        var dataLength = (long)U64(record, offset + 0x30);
        var initialized = (long)U64(record, offset + 0x38);
        if (dataLength <= 0 || dataLength > allocated || initialized != dataLength || allocated % layout.BytesPerCluster != 0)
        {
            throw new JournalException($"{fileName} has invalid allocated, data or initialized lengths");
        }

        var extents = ParseRuns(record, offset + runsOffset, offset + length, layout.TotalClusters, fileName);
        var last = extents[extents.Count - 1];
        var clusters = last.Vcn + last.Length;
        if (U64(record, offset + 0x18) != (ulong)(clusters - 1) || clusters != allocated / layout.BytesPerCluster)
        {
            throw new JournalException($"{fileName} run list does not cover the declared allocation in one attribute extent");
        }

        return (extents, dataLength);
    }

    private static byte[] ReadLogFileRecord(Stream volume, JournalVolumeLayout layout, long mftCluster)
    {
        var end = (LogFileRecordNumber + 1L) * layout.MftRecordSize;
        if (!layout.ContainsClusters(mftCluster, (end + layout.BytesPerCluster - 1) / layout.BytesPerCluster))
        {
            return null;
        }

        var record = new byte[layout.MftRecordSize];
        var position = (mftCluster * layout.BytesPerCluster) + (end - record.Length);
        ReadExactly(volume, position, record, 0, record.Length);
        if (U32(record, 0) != SignatureFile || !RemoveFixups(record, 0, record.Length) || (U16(record, 0x16) & 1) == 0)
        {
            return null;
        }

        return record;
    }

    internal static List<(long Vcn, long Lcn, long Length)> ParseRuns(byte[] record, int offset, int end,
        long totalClusters, string fileName)
    {
        var extents = new List<(long Vcn, long Lcn, long Length)>();
        long vcn = 0;
        long lcn = 0;
        while (offset < end && record[offset] != 0)
        {
            var header = record[offset];
            var lengthSize = header & 0x0F;
            var offsetSize = header >> 4;
            if (lengthSize is 0 or > 8 || offsetSize > 8 || offset + 1 + lengthSize + offsetSize > end)
            {
                throw new JournalException($"{fileName} has a damaged run list");
            }

            var runLength = ReadUnsigned(record, offset + 1, lengthSize);
            if (offsetSize == 0)
            {
                throw new JournalException($"{fileName} has a sparse run");
            }

            var delta = ReadSigned(record, offset + 1 + lengthSize, offsetSize);
            if (delta <= -lcn || delta > long.MaxValue - lcn)
            {
                throw new JournalException($"{fileName} run has an invalid or overflowing LCN");
            }

            lcn += delta;
            if (runLength <= 0 || runLength > totalClusters - lcn || runLength > totalClusters - vcn)
            {
                throw new JournalException($"{fileName} run has an invalid length or extends outside the volume");
            }

            extents.Add((vcn, lcn, runLength));
            vcn += runLength;
            offset += 1 + lengthSize + offsetSize;
        }

        if (extents.Count == 0 || offset == end)
        {
            throw new JournalException($"{fileName} run list is empty or has no terminator");
        }

        long previousEnd = 0;
        foreach (var extent in extents.OrderBy(extent => extent.Lcn))
        {
            if (extent.Lcn < previousEnd)
            {
                throw new JournalException($"{fileName} has overlapping physical runs");
            }

            previousEnd = extent.Lcn + extent.Length;
        }

        return extents;
    }

    private static long ReadUnsigned(byte[] buffer, int offset, int size)
    {
        long value = 0;
        for (var i = size - 1; i >= 0; i--)
        {
            value = (value << 8) | buffer[offset + i];
        }

        return value;
    }

    private static long ReadSigned(byte[] buffer, int offset, int size)
    {
        var value = ReadUnsigned(buffer, offset, size);

        // Sign-extend from the top bit of the last byte.
        if (size < 8 && (buffer[offset + size - 1] & 0x80) != 0)
        {
            value |= -1L << (size * 8);
        }

        return value;
    }

    public void Read(long position, byte[] buffer, int offset, int count) => Transfer(position, buffer, offset, count, write: false);

    public void Write(long position, byte[] buffer, int offset, int count) => Transfer(position, buffer, offset, count, write: true);

    private void Transfer(long position, byte[] buffer, int offset, int count, bool write)
    {
        if (position < 0 || count < 0 || position > Length - count)
        {
            throw new JournalException($"Access beyond the end of $LogFile at {position}");
        }

        while (count > 0)
        {
            var vcn = position / _clusterSize;
            var inCluster = (int)(position % _clusterSize);
            var extent = FindExtent(vcn);
            var available = ((extent.Vcn + extent.Length - vcn) * _clusterSize) - inCluster;
            var chunk = (int)Math.Min(count, available);
            var physical = ((extent.Lcn + (vcn - extent.Vcn)) * _clusterSize) + inCluster;
            if (write)
            {
                _volume.Position = physical;
                _volume.Write(buffer, offset, chunk);
            }
            else
            {
                ReadExactly(_volume, physical, buffer, offset, chunk);
            }

            position += chunk;
            offset += chunk;
            count -= chunk;
        }
    }

    private (long Vcn, long Lcn, long Length) FindExtent(long vcn)
    {
        // The extents follow each other from VCN 0, so the first one that ends after the VCN holds it.
        foreach (var extent in _extents)
        {
            if (vcn < extent.Vcn + extent.Length)
            {
                return extent;
            }
        }

        throw new JournalException($"$LogFile has no cluster for VCN {vcn}");
    }
}
