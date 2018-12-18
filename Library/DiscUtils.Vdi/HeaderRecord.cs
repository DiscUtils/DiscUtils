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
using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.Vdi
{
    internal class HeaderRecord
    {
        private FileVersion _fileVersion;
        public int BlockCount;
        public int BlockExtraSize;
        public int BlocksAllocated;
        public int BlockSize;
        public uint BlocksOffset;
        public string Comment;
        public uint DataOffset;
        public long DiskSize;
        public ImageFlags Flags;
        public uint HeaderSize;
        public ImageType ImageType;
        public GeometryRecord LChsGeometry;
        public GeometryRecord LegacyGeometry;
        public Guid ModificationId;
        public Guid ParentId;
        public Guid ParentModificationId;
        public Guid UniqueId;

        public static HeaderRecord Initialized(ImageType type, ImageFlags flags, long size, int blockSize,
                                               int blockExtra)
        {
            HeaderRecord result = new HeaderRecord();

            result._fileVersion = new FileVersion(0x00010001);
            result.HeaderSize = 400;
            result.ImageType = type;
            result.Flags = flags;
            result.Comment = "Created by .NET DiscUtils";
            result.LegacyGeometry = new GeometryRecord();
            result.DiskSize = size;
            result.BlockSize = blockSize;
            result.BlockExtraSize = blockExtra;
            result.BlockCount = (int)((size + blockSize - 1) / blockSize);
            result.BlocksAllocated = 0;

            result.BlocksOffset = (PreHeaderRecord.Size + result.HeaderSize + 511) / 512 * 512;
            result.DataOffset = (uint)((result.BlocksOffset + result.BlockCount * 4 + 511) / 512 * 512);

            result.UniqueId = Guid.NewGuid();
            result.ModificationId = Guid.NewGuid();

            result.LChsGeometry = new GeometryRecord();

            return result;
        }

        public void Read(FileVersion version, Stream s)
        {
            int headerSize;

            _fileVersion = version;

            // Determine header size...
            if (version.Major == 0)
            {
                headerSize = 348;
            }
            else
            {
                long savedPos = s.Position;
                headerSize = EndianUtilities.ToInt32LittleEndian(StreamUtilities.ReadExact(s, 4), 0);
                s.Position = savedPos;
            }

            byte[] buffer = StreamUtilities.ReadExact(s, headerSize);
            Read(version, buffer, 0);
        }

        public int Read(FileVersion version, byte[] buffer, int offset)
        {
            if (version.Major == 0)
            {
                ImageType = (ImageType)EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0);
                Flags = (ImageFlags)EndianUtilities.ToUInt32LittleEndian(buffer, offset + 4);
                Comment = EndianUtilities.BytesToString(buffer, offset + 8, 256).TrimEnd('\0');
                LegacyGeometry = new GeometryRecord();
                LegacyGeometry.Read(buffer, offset + 264);
                DiskSize = EndianUtilities.ToInt64LittleEndian(buffer, offset + 280);
                BlockSize = EndianUtilities.ToInt32LittleEndian(buffer, offset + 288);
                BlockCount = EndianUtilities.ToInt32LittleEndian(buffer, offset + 292);
                BlocksAllocated = EndianUtilities.ToInt32LittleEndian(buffer, offset + 296);
                UniqueId = EndianUtilities.ToGuidLittleEndian(buffer, offset + 300);
                ModificationId = EndianUtilities.ToGuidLittleEndian(buffer, offset + 316);
                ParentId = EndianUtilities.ToGuidLittleEndian(buffer, offset + 332);
                HeaderSize = 348;
                BlocksOffset = HeaderSize + PreHeaderRecord.Size;
                DataOffset = (uint)(BlocksOffset + BlockCount * 4);
                BlockExtraSize = 0;
                ParentModificationId = Guid.Empty;
            }
            else if (version.Major == 1 && version.Minor == 1)
            {
                HeaderSize = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0);
                ImageType = (ImageType)EndianUtilities.ToUInt32LittleEndian(buffer, offset + 4);
                Flags = (ImageFlags)EndianUtilities.ToUInt32LittleEndian(buffer, offset + 8);
                Comment = EndianUtilities.BytesToString(buffer, offset + 12, 256).TrimEnd('\0');
                BlocksOffset = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 268);
                DataOffset = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 272);
                LegacyGeometry = new GeometryRecord();
                LegacyGeometry.Read(buffer, offset + 276);
                DiskSize = EndianUtilities.ToInt64LittleEndian(buffer, offset + 296);
                BlockSize = EndianUtilities.ToInt32LittleEndian(buffer, offset + 304);
                BlockExtraSize = EndianUtilities.ToInt32LittleEndian(buffer, offset + 308);
                BlockCount = EndianUtilities.ToInt32LittleEndian(buffer, offset + 312);
                BlocksAllocated = EndianUtilities.ToInt32LittleEndian(buffer, offset + 316);
                UniqueId = EndianUtilities.ToGuidLittleEndian(buffer, offset + 320);
                ModificationId = EndianUtilities.ToGuidLittleEndian(buffer, offset + 336);
                ParentId = EndianUtilities.ToGuidLittleEndian(buffer, offset + 352);
                ParentModificationId = EndianUtilities.ToGuidLittleEndian(buffer, offset + 368);

                if (HeaderSize > 384)
                {
                    LChsGeometry = new GeometryRecord();
                    LChsGeometry.Read(buffer, offset + 384);
                }
            }
            else
            {
                throw new IOException("Unrecognized file version: " + version);
            }

            return (int)HeaderSize;
        }

        public void Write(Stream s)
        {
            byte[] buffer = new byte[HeaderSize];
            Write(buffer, 0);
            s.Write(buffer, 0, buffer.Length);
        }

        public int Write(byte[] buffer, int offset)
        {
            if (_fileVersion.Major == 0)
            {
                EndianUtilities.WriteBytesLittleEndian((uint)ImageType, buffer, offset + 0);
                EndianUtilities.WriteBytesLittleEndian((uint)Flags, buffer, offset + 4);
                EndianUtilities.StringToBytes(Comment, buffer, offset + 8, 256);
                LegacyGeometry.Write(buffer, offset + 264);
                EndianUtilities.WriteBytesLittleEndian(DiskSize, buffer, offset + 280);
                EndianUtilities.WriteBytesLittleEndian(BlockSize, buffer, offset + 288);
                EndianUtilities.WriteBytesLittleEndian(BlockCount, buffer, offset + 292);
                EndianUtilities.WriteBytesLittleEndian(BlocksAllocated, buffer, offset + 296);
                EndianUtilities.WriteBytesLittleEndian(UniqueId, buffer, offset + 300);
                EndianUtilities.WriteBytesLittleEndian(ModificationId, buffer, offset + 316);
                EndianUtilities.WriteBytesLittleEndian(ParentId, buffer, offset + 332);
            }
            else if (_fileVersion.Major == 1 && _fileVersion.Minor == 1)
            {
                EndianUtilities.WriteBytesLittleEndian(HeaderSize, buffer, offset + 0);
                EndianUtilities.WriteBytesLittleEndian((uint)ImageType, buffer, offset + 4);
                EndianUtilities.WriteBytesLittleEndian((uint)Flags, buffer, offset + 8);
                EndianUtilities.StringToBytes(Comment, buffer, offset + 12, 256);
                EndianUtilities.WriteBytesLittleEndian(BlocksOffset, buffer, offset + 268);
                EndianUtilities.WriteBytesLittleEndian(DataOffset, buffer, offset + 272);
                LegacyGeometry.Write(buffer, offset + 276);
                EndianUtilities.WriteBytesLittleEndian(DiskSize, buffer, offset + 296);
                EndianUtilities.WriteBytesLittleEndian(BlockSize, buffer, offset + 304);
                EndianUtilities.WriteBytesLittleEndian(BlockExtraSize, buffer, offset + 308);
                EndianUtilities.WriteBytesLittleEndian(BlockCount, buffer, offset + 312);
                EndianUtilities.WriteBytesLittleEndian(BlocksAllocated, buffer, offset + 316);
                EndianUtilities.WriteBytesLittleEndian(UniqueId, buffer, offset + 320);
                EndianUtilities.WriteBytesLittleEndian(ModificationId, buffer, offset + 336);
                EndianUtilities.WriteBytesLittleEndian(ParentId, buffer, offset + 352);
                EndianUtilities.WriteBytesLittleEndian(ParentModificationId, buffer, offset + 368);

                if (HeaderSize > 384)
                {
                    LChsGeometry.Write(buffer, offset + 384);
                }
            }
            else
            {
                throw new IOException("Unrecognized file version: " + _fileVersion);
            }

            return (int)HeaderSize;
        }
    }
}