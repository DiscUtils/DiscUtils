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
using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.Ntfs.Journal;

/// <summary>
/// The journal could not be read or replayed. The volume is left as it was.
/// </summary>
internal sealed class JournalException : IOException
{
    public JournalException(string message)
        : base(message) { }
}

internal static class JournalUtilities
{
    public static readonly uint SignatureFile = Signature("FILE");
    public static readonly uint SignatureIndx = Signature("INDX");
    public static readonly uint SignatureRstr = Signature("RSTR");
    public static readonly uint SignatureRcrd = Signature("RCRD");
    public static readonly uint SignatureChkd = Signature("CHKD");
    public static readonly uint SignatureBaad = Signature("BAAD");
    public static readonly uint SignatureHole = Signature("HOLE");

    private static uint Signature(string text) =>
        (uint)(text[0] | (text[1] << 8) | (text[2] << 16) | (text[3] << 24));

    public static ushort U16(byte[] b, int offset) => EndianUtilities.ToUInt16LittleEndian(b, offset);

    public static uint U32(byte[] b, int offset) => EndianUtilities.ToUInt32LittleEndian(b, offset);

    public static ulong U64(byte[] b, int offset) => EndianUtilities.ToUInt64LittleEndian(b, offset);

    public static void Put16(byte[] b, int offset, ushort value) => EndianUtilities.WriteBytesLittleEndian(value, b, offset);

    public static void Put32(byte[] b, int offset, uint value) => EndianUtilities.WriteBytesLittleEndian(value, b, offset);

    public static void Put64(byte[] b, int offset, ulong value) => EndianUtilities.WriteBytesLittleEndian(value, b, offset);

    public static int Align8(int value) => (value + 7) & ~7;

    /// <summary>
    /// Removes the update sequence array protection of a multi-sector structure (MFT record, index buffer,
    /// log page) in place: the last two bytes of every stride go back to the values saved in the array.
    /// </summary>
    /// <returns><c>false</c> if the header is implausible or a stride does not end with the sequence number
    /// (a torn write); the buffer is then left unchanged.</returns>
    public static bool RemoveFixups(byte[] buffer, int offset, int length)
    {
        if (!TryGetFixupLayout(buffer, offset, length, out var usaOffset, out var usaCount, out var stride))
        {
            return false;
        }

        var usn = U16(buffer, offset + usaOffset);
        for (var i = 1; i < usaCount; i++)
        {
            if (U16(buffer, offset + (i * stride) - 2) != usn)
            {
                return false;
            }
        }

        for (var i = 1; i < usaCount; i++)
        {
            buffer[offset + (i * stride) - 2] = buffer[offset + usaOffset + (2 * i)];
            buffer[offset + (i * stride) - 1] = buffer[offset + usaOffset + (2 * i) + 1];
        }

        return true;
    }

    /// <summary>
    /// Protects a multi-sector structure before it is written: increments the update sequence number and puts it
    /// at the end of every stride, saving the original bytes in the array.
    /// </summary>
    public static void ApplyFixups(byte[] buffer, int offset, int length)
    {
        if (!TryGetFixupLayout(buffer, offset, length, out var usaOffset, out var usaCount, out var stride))
        {
            throw new JournalException("A changed record has no valid update sequence array");
        }

        var usn = (ushort)(U16(buffer, offset + usaOffset) + 1);
        if (usn is 0 or 0xFFFF)
        {
            usn = 1;
        }

        Put16(buffer, offset + usaOffset, usn);
        for (var i = 1; i < usaCount; i++)
        {
            var end = offset + (i * stride) - 2;
            buffer[offset + usaOffset + (2 * i)] = buffer[end];
            buffer[offset + usaOffset + (2 * i) + 1] = buffer[end + 1];
            Put16(buffer, end, usn);
        }
    }

    /// <summary>Whether the header describes a plausible update sequence array for this length.</summary>
    public static bool HasFixupLayout(byte[] buffer, int offset, int length) =>
        TryGetFixupLayout(buffer, offset, length, out _, out _, out _);

    private static bool TryGetFixupLayout(byte[] buffer, int offset, int length, out int usaOffset, out int usaCount,
        out int stride)
    {
        usaOffset = usaCount = stride = 0;
        if (offset < 0 || length < 8 || offset > buffer.Length - length)
        {
            return false;
        }

        usaOffset = U16(buffer, offset + 4);
        usaCount = U16(buffer, offset + 6);
        if (usaCount < 2 || usaOffset < 8 || usaOffset + (2 * usaCount) > length || (usaOffset & 1) != 0)
        {
            return false;
        }

        // NTFS protects every 512 bytes; derive it from the array length so 4K-sector volumes work too.
        if (length % (usaCount - 1) != 0)
        {
            return false;
        }

        stride = length / (usaCount - 1);
        return stride is >= 256 and <= 4096 && (stride & (stride - 1)) == 0 && usaOffset + (2 * usaCount) <= stride - 2;
    }

    public static void ReadExactly(Stream stream, long position, byte[] buffer, int offset, int count)
    {
        stream.Position = position;
        var done = 0;
        while (done < count)
        {
            var n = stream.Read(buffer, offset + done, count - done);
            if (n <= 0)
            {
                throw new JournalException($"Unexpected end of the volume at byte {position + done}");
            }

            done += n;
        }
    }
}

/// <summary>
/// The parts of the boot sector the journal code needs.
/// </summary>
internal sealed class JournalVolumeLayout
{
    private const int MaxClusterSize = 2 * 1024 * 1024;

    public int BytesPerSector { get; private set; }

    public int BytesPerCluster { get; private set; }

    public int MftRecordSize { get; private set; }

    public int IndexBufferSize { get; private set; }

    public long MftCluster { get; private set; }

    public long MftMirrorCluster { get; private set; }

    public long TotalClusters { get; private set; }

    public bool ContainsClusters(long lcn, long count) => lcn > 0 && count > 0 &&
        lcn < TotalClusters && count <= TotalClusters - lcn;

    public static bool IsRecordSize(long size) => size is >= 256 and <= MaxClusterSize && (size & (size - 1)) == 0;

    public static JournalVolumeLayout Read(Stream volume)
    {
        var bytes = new byte[512];
        JournalUtilities.ReadExactly(volume, 0, bytes, 0, bytes.Length);
        var bpb = BiosParameterBlock.FromBytes(bytes);
        if (!bpb.IsValidOemId())
        {
            throw new InvalidFileSystemException("BIOS Parameter Block is invalid for an NTFS file system");
        }

        var clusterSize = (long)bpb.BytesPerSector * bpb.SectorsPerCluster;
        var mftRecordSize = RecordSize(bpb.RawMftRecordSize, clusterSize);
        var indexBufferSize = RecordSize(bpb.RawIndexBufferSize, clusterSize);
        if (bpb.TotalSectors16 != 0 || bpb.TotalSectors32 != 0 || bpb.TotalSectors64 <= 0 ||
            bpb.BytesPerSector is < 256 or > 4096 || (bpb.BytesPerSector & (bpb.BytesPerSector - 1)) != 0 ||
            bpb.TotalSectors64 > long.MaxValue / bpb.BytesPerSector || bytes[0x0D] is > 0x80 and < (256 - 13) ||
            clusterSize < bpb.BytesPerSector || clusterSize > MaxClusterSize || (clusterSize & (clusterSize - 1)) != 0 ||
            !IsRecordSize(mftRecordSize) || !IsRecordSize(indexBufferSize))
        {
            throw new JournalException("The NTFS boot sector has invalid volume or record sizes");
        }

        var totalClusters = Math.Min(bpb.TotalSectors64 / bpb.SectorsPerCluster, volume.Length / clusterSize);
        if (bpb.MftCluster <= 0 || bpb.MftCluster >= totalClusters)
        {
            throw new JournalException("The NTFS boot sector places $MFT outside the volume");
        }

        return new JournalVolumeLayout
        {
            BytesPerSector = bpb.BytesPerSector,
            BytesPerCluster = (int)clusterSize,
            MftRecordSize = (int)mftRecordSize,
            IndexBufferSize = (int)indexBufferSize,
            MftCluster = bpb.MftCluster,
            MftMirrorCluster = bpb.MftMirrorCluster,
            TotalClusters = totalClusters,
        };
    }

    private static long RecordSize(byte raw, long clusterSize) => raw <= 0x80 ? raw * clusterSize :
        raw >= 256 - 21 ? 1L << (256 - raw) : 0;
}
