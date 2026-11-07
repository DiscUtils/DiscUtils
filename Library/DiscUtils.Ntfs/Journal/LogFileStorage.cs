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
/// Byte access to the $LogFile data on the raw volume, located through MFT record 2 before the file system is
/// mounted. $LogFile's own record is written at format time only, so it is never stale.
/// </summary>
internal sealed class LogFileStorage
{
    private const int LogFileRecordNumber = 2;
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

        int attrOffset = U16(record, 0x14);
        while (attrOffset + 8 <= record.Length)
        {
            var type = U32(record, attrOffset);
            var length = (int)U32(record, attrOffset + 4);
            if (type == AttributeEnd || length < 0x18 || attrOffset + length > record.Length)
            {
                break;
            }

            var nameLength = record[attrOffset + 9];
            if (type == AttributeData && nameLength == 0)
            {
                if (record[attrOffset + 8] == 0)
                {
                    throw new JournalException("$LogFile has resident data, which NTFS never does");
                }

                var runsOffset = U16(record, attrOffset + 0x20);
                var dataLength = (long)U64(record, attrOffset + 0x30);
                var extents = ParseRuns(record, attrOffset + runsOffset, attrOffset + length);
                return new LogFileStorage(volume, layout.BytesPerCluster, extents, dataLength);
            }

            attrOffset += length;
        }

        throw new JournalException("$LogFile has no data attribute");
    }

    private static byte[] ReadLogFileRecord(Stream volume, JournalVolumeLayout layout, long mftCluster)
    {
        var record = new byte[layout.MftRecordSize];
        var position = (mftCluster * layout.BytesPerCluster) + ((long)LogFileRecordNumber * layout.MftRecordSize);
        if (position < 0 || position + record.Length > volume.Length)
        {
            return null;
        }

        ReadExactly(volume, position, record, 0, record.Length);
        if (U32(record, 0) != SignatureFile || !RemoveFixups(record, 0, record.Length) || (U16(record, 0x16) & 1) == 0)
        {
            return null;
        }

        return record;
    }

    private static List<(long, long, long)> ParseRuns(byte[] record, int offset, int end)
    {
        var extents = new List<(long, long, long)>();
        long vcn = 0;
        long lcn = 0;
        while (offset < end && record[offset] != 0)
        {
            var header = record[offset];
            var lengthSize = header & 0x0F;
            var offsetSize = header >> 4;
            if (lengthSize is 0 or > 8 || offsetSize > 8 || offset + 1 + lengthSize + offsetSize > end)
            {
                throw new JournalException("$LogFile has a damaged run list");
            }

            var runLength = ReadUnsigned(record, offset + 1, lengthSize);
            if (offsetSize == 0)
            {
                throw new JournalException("$LogFile is sparse, which NTFS never does");
            }

            lcn += ReadSigned(record, offset + 1 + lengthSize, offsetSize);
            extents.Add((vcn, lcn, runLength));
            vcn += runLength;
            offset += 1 + lengthSize + offsetSize;
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
        if (position < 0 || position + count > Length)
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
        foreach (var extent in _extents)
        {
            if (vcn >= extent.Vcn && vcn < extent.Vcn + extent.Length)
            {
                return extent;
            }
        }

        throw new JournalException($"$LogFile has no cluster for VCN {vcn}");
    }
}
