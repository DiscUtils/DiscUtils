//
// Copyright (c) 2008-2012, Kenneth Bell
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

using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.Vhdx
{
    /// <summary>
    /// Represents a chunk of blocks in the Block Allocation Table.
    /// </summary>
    /// <remarks>
    /// The BAT entries for a chunk are always present in the BAT, but the data blocks and
    /// sector bitmap blocks may (or may not) be present.
    /// </remarks>
    internal sealed class Chunk
    {
        private const ulong SectorBitmapPresent = 6;

        private readonly Stream _bat;
        private readonly byte[] _batData;
        private readonly int _blocksPerChunk;
        private readonly int _chunk;
        private readonly SparseStream _file;
        private readonly FileParameters _fileParameters;
        private readonly FreeSpaceTable _freeSpace;
        private byte[] _sectorBitmap;

        public Chunk(Stream bat, SparseStream file, FreeSpaceTable freeSpace, FileParameters fileParameters, int chunk,
                     int blocksPerChunk)
        {
            _bat = bat;
            _file = file;
            _freeSpace = freeSpace;
            _fileParameters = fileParameters;
            _chunk = chunk;
            _blocksPerChunk = blocksPerChunk;

            _bat.Position = _chunk * (_blocksPerChunk + 1) * 8;
            _batData = StreamUtilities.ReadExact(bat, (_blocksPerChunk + 1) * 8);
        }

        private bool HasSectorBitmap
        {
            get { return new BatEntry(_batData, _blocksPerChunk * 8).BitmapBlockPresent; }
        }

        private long SectorBitmapPos
        {
            get { return new BatEntry(_batData, _blocksPerChunk * 8).FileOffsetMB * Sizes.OneMiB; }

            set
            {
                BatEntry entry = new BatEntry();
                entry.BitmapBlockPresent = value != 0;
                entry.FileOffsetMB = value / Sizes.OneMiB;
                entry.WriteTo(_batData, _blocksPerChunk * 8);
            }
        }

        public long GetBlockPosition(int block)
        {
            return new BatEntry(_batData, block * 8).FileOffsetMB * Sizes.OneMiB;
        }

        public PayloadBlockStatus GetBlockStatus(int block)
        {
            return new BatEntry(_batData, block * 8).PayloadBlockStatus;
        }

        public BlockBitmap GetBlockBitmap(int block)
        {
            int bytesPerBlock = (int)(Sizes.OneMiB / _blocksPerChunk);
            int offset = bytesPerBlock * block;
            byte[] data = LoadSectorBitmap();
            return new BlockBitmap(data, offset, bytesPerBlock);
        }

        public void WriteBlockBitmap(int block)
        {
            int bytesPerBlock = (int)(Sizes.OneMiB / _blocksPerChunk);
            int offset = bytesPerBlock * block;

            _file.Position = SectorBitmapPos + offset;
            _file.Write(_sectorBitmap, offset, bytesPerBlock);
        }

        public PayloadBlockStatus AllocateSpaceForBlock(int block)
        {
            bool dataModified = false;

            BatEntry blockEntry = new BatEntry(_batData, block * 8);
            if (blockEntry.FileOffsetMB == 0)
            {
                blockEntry.FileOffsetMB = AllocateSpace((int)_fileParameters.BlockSize, false) / Sizes.OneMiB;
                dataModified = true;
            }

            if (blockEntry.PayloadBlockStatus != PayloadBlockStatus.FullyPresent
                && blockEntry.PayloadBlockStatus != PayloadBlockStatus.PartiallyPresent)
            {
                if ((_fileParameters.Flags & FileParametersFlags.HasParent) != 0)
                {
                    if (!HasSectorBitmap)
                    {
                        SectorBitmapPos = AllocateSpace((int)Sizes.OneMiB, true);
                    }

                    blockEntry.PayloadBlockStatus = PayloadBlockStatus.PartiallyPresent;
                }
                else
                {
                    blockEntry.PayloadBlockStatus = PayloadBlockStatus.FullyPresent;
                }

                dataModified = true;
            }

            if (dataModified)
            {
                blockEntry.WriteTo(_batData, block * 8);

                _bat.Position = _chunk * (_blocksPerChunk + 1) * 8;
                _bat.Write(_batData, 0, (_blocksPerChunk + 1) * 8);
            }

            return blockEntry.PayloadBlockStatus;
        }

        private byte[] LoadSectorBitmap()
        {
            if (_sectorBitmap == null)
            {
                _file.Position = SectorBitmapPos;
                _sectorBitmap = StreamUtilities.ReadExact(_file, (int)Sizes.OneMiB);
            }

            return _sectorBitmap;
        }

        private long AllocateSpace(int sizeBytes, bool zero)
        {
            long pos;
            if (!_freeSpace.TryAllocate(sizeBytes, out pos))
            {
                pos = MathUtilities.RoundUp(_file.Length, Sizes.OneMiB);
                _file.SetLength(pos + sizeBytes);
                _freeSpace.ExtendTo(pos + sizeBytes, false);
            }
            else if (zero)
            {
                _file.Position = pos;
                _file.Clear(sizeBytes);
            }

            return pos;
        }
    }
}