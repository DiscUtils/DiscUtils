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

namespace DiscUtils.Vhdx
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    internal sealed class BlockAllocationTable
    {
        private const ulong PayloadBlockNotPresent = 0;
        private const ulong PayloadBlockUndefined = 1;
        private const ulong PayloadBlockZero = 2;
        private const ulong PayloadBlockUnmapped = 3;
        private const ulong PayloadBlockFullyPresent = 6;
        private const ulong PayloadBlockPartiallyPresent = 7;

        private Stream _stream;
        private Metadata _metadata;

        private long _logicalSectorSize;
        private long _chunkSize;
        private int _chunkRatio;

        public BlockAllocationTable(Stream stream, Metadata metadata)
        {
            _stream = stream;
            _metadata = metadata;

            _logicalSectorSize = _metadata.LogicalSectorSize;
            _chunkSize = (1L << 23) * _logicalSectorSize;
            _chunkRatio = (int)(_chunkSize / _metadata.FileParameters.BlockSize);

            long dataBlocksCount = (long)Utilities.Ceil(_metadata.DiskSize, _metadata.FileParameters.BlockSize);
            long sectorBitmapsBlocksCount = (long)Utilities.Ceil(dataBlocksCount, _chunkRatio);

        }

        public long ChunkSize
        {
            get { return _chunkSize; }
        }

        public long LogicalSectorSize
        {
            get { return _logicalSectorSize; }
        }

        public IEnumerable<StreamExtent> StoredExtents()
        {
            uint blockSize = _metadata.FileParameters.BlockSize;

            _stream.Position = 0;
            byte[] data = Utilities.ReadFully(_stream, (int)_stream.Length);

            long lastStart = 0;
            bool inExtent = false;

            long pos = 0;
            while (pos < (long)_metadata.DiskSize)
            {
                long chunk = pos / _chunkSize;
                long chunkOffset = pos % _chunkSize;
                int block = (int)(chunkOffset / blockSize);

                ulong entry = Utilities.ToUInt64LittleEndian(data, (int)((chunk * (_chunkRatio + 1)) + block) * 8);
                if(inExtent && !IsInExtent(entry))
                {
                    yield return new StreamExtent(lastStart, pos - lastStart);
                    inExtent = false;
                }
                else if (!inExtent && IsInExtent(entry))
                {
                    lastStart = pos;
                    inExtent = true;
                }

                pos += blockSize;
            }

            if (inExtent)
            {
                yield return new StreamExtent(lastStart, Math.Min((long)_metadata.DiskSize, pos) - lastStart);
            }
        }

        public SectorDisposition GetDisposition(long diskPosition)
        {
            uint blockSize = _metadata.FileParameters.BlockSize;

            long pos = diskPosition;
            long chunk = pos / _chunkSize;
            long chunkOffset = pos % _chunkSize;
            int block = (int)(chunkOffset / blockSize);

            _stream.Position = chunk * (_chunkRatio + 1) * 8;
            byte[] chunkData = Utilities.ReadFully(_stream, (_chunkRatio + 1) * 8);

            ulong bitmapEntry = Utilities.ToUInt64LittleEndian(chunkData, _chunkRatio * 8);

            ulong entry = Utilities.ToUInt64LittleEndian(chunkData, block * 8);
            return GetEntryDisposition(entry);
        }

        public long GetFilePosition(long diskPosition)
        {
            uint blockSize = _metadata.FileParameters.BlockSize;

            long pos = diskPosition;
            long chunk = pos / _chunkSize;
            long chunkOffset = pos % _chunkSize;
            int block = (int)(chunkOffset / blockSize);
            int blockOffset = (int)(chunkOffset % blockSize);

            _stream.Position = chunk * (_chunkRatio + 1) * 8;
            byte[] chunkData = Utilities.ReadFully(_stream, (_chunkRatio + 1) * 8);

            ulong bitmapEntry = Utilities.ToUInt64LittleEndian(chunkData, _chunkRatio * 8);

            ulong entry = Utilities.ToUInt64LittleEndian(chunkData, block * 8);

            long filePos = (long)((entry >> 20) & 0xFFFFFFFFFFF) * Sizes.OneMiB;
            if (filePos == 0)
            {
                throw new IOException("Attempt to read from unallocated block");
            }

            return filePos + blockOffset;
        }

        public long ContiguousBytes(long diskPosition, long max)
        {
            uint blockSize = _metadata.FileParameters.BlockSize;

            long pos = diskPosition;
            long chunk = pos / _chunkSize;
            long chunkOffset = pos % _chunkSize;
            int block = (int)(chunkOffset / blockSize);

            _stream.Position = chunk * (_chunkRatio + 1) * 8;
            byte[] chunkData = Utilities.ReadFully(_stream, (_chunkRatio + 1) * 8);


            ulong entry = Utilities.ToUInt64LittleEndian(chunkData, block * 8);
            if ((entry & 0x07) == PayloadBlockPartiallyPresent)
            {
                ulong bitmapEntry = Utilities.ToUInt64LittleEndian(chunkData, _chunkRatio * 8);
                throw new NotImplementedException();
            }
            else
            {
                long blockOffset = chunkOffset % blockSize;
                return Math.Min(blockSize - blockOffset, max);
            }
        }

        private static SectorDisposition GetEntryDisposition(ulong entry)
        {
            switch (entry & 0x7)
            {
                case PayloadBlockNotPresent:
                    return SectorDisposition.Parent;

                case PayloadBlockUndefined:
                case PayloadBlockZero:
                case PayloadBlockUnmapped:
                    return SectorDisposition.Zero;

                case PayloadBlockFullyPresent:
                    return SectorDisposition.Stored;

                case PayloadBlockPartiallyPresent:
                    throw new NotImplementedException();

                default:
                    throw new IOException("Unexpected BAT entry state: " + (entry & 0x7));
            }
        }

        private static bool IsInExtent(ulong entry)
        {
            switch (entry & 0x7)
            {
                case PayloadBlockNotPresent:
                case PayloadBlockUndefined:
                case PayloadBlockZero:
                case PayloadBlockUnmapped:
                    return false;

                case PayloadBlockFullyPresent:
                case PayloadBlockPartiallyPresent:
                    return true;

                default:
                    throw new IOException("Unexpected BAT entry state: " + (entry & 0x7));
            }
        }
    }
}
