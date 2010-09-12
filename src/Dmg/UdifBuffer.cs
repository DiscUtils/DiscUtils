//
// Copyright (c) 2008-2010, Kenneth Bell
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

namespace DiscUtils.Dmg
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;

    internal class UdifBuffer : DiscUtils.Buffer
    {
        private Stream _stream;
        private ResourceFork _resources;
        private long _sectorCount;
        private List<CompressedBlock> _blocks;

        private byte[] _buffer;
        private CompressedRun _activeRun;
        private long _bufferStart;

        public UdifBuffer(Stream stream, ResourceFork resources, long sectorCount)
        {
            _stream = stream;
            _resources = resources;
            _sectorCount = sectorCount;

            _blocks = new List<CompressedBlock>();

            Resource resource;
            int i = -1;
            while (_resources.TryGetResource("blkx", i, out resource))
            {
                _blocks.Add(((BlkxResource)resource).Block);
                ++i;
            }
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override long Capacity
        {
            get { return _sectorCount * Sizes.Sector; }
        }

        public override int Read(long pos, byte[] buffer, int offset, int count)
        {
            int totalCopied = 0;
            long currentPos = pos;

            while (totalCopied < count && currentPos < Capacity)
            {
                LoadRun(currentPos);

                int bufferOffset = (int)(currentPos - _bufferStart);
                int toCopy = (int)Math.Min(((_activeRun.SectorCount * Sizes.Sector) - bufferOffset), count - totalCopied);

                switch (_activeRun.Type)
                {
                    case RunType.Zeros:
                        Array.Clear(buffer, offset + totalCopied, toCopy);
                        break;

                    case RunType.ZlibCompressed:
                        Array.Copy(_buffer, bufferOffset, buffer, offset + totalCopied, toCopy);
                        break;

                    default:
                        throw new NotImplementedException("Reading from run of type " + _activeRun.Type);
                }

                currentPos += toCopy;
                totalCopied += toCopy;
            }

            return totalCopied;
        }

        public override void Write(long pos, byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override void SetCapacity(long value)
        {
            throw new NotSupportedException();
        }

        public override IEnumerable<StreamExtent> GetExtentsInRange(long start, long count)
        {
            foreach (var block in _blocks)
            {
                foreach (var run in block.Runs)
                {
                    if (run.SectorCount > 0 && run.Type != RunType.Zeros)
                    {
                        yield return new StreamExtent((block.FirstSector + run.SectorStart) * Sizes.Sector, run.SectorCount * Sizes.Sector);
                    }
                }
            }
        }

        private void LoadRun(long pos)
        {
            if (_activeRun != null && pos >= _bufferStart && pos < _bufferStart + (_activeRun.SectorCount * Sizes.Sector))
            {
                return;
            }

            long findSector = pos / 512;
            foreach (var block in _blocks)
            {
                if (block.FirstSector <= findSector && (block.FirstSector + block.SectorCount) > findSector)
                {
                    // Make sure the decompression buffer is big enough
                    if (_buffer == null || _buffer.Length < block.DecompressBufferRequested * Sizes.Sector)
                    {
                        _buffer = new byte[block.DecompressBufferRequested * Sizes.Sector];
                    }

                    foreach (var run in block.Runs)
                    {
                        if ((block.FirstSector + run.SectorStart) <= findSector && ((block.FirstSector + run.SectorStart) + run.SectorCount) > findSector)
                        {
                            LoadRun(run, block.FirstSector);
                            return;
                        }
                    }

                    throw new IOException("No run for sector " + findSector + " in block starting at " + block.FirstSector);
                }
            }

            throw new IOException("No block for sector " + findSector);
        }

        private void LoadRun(CompressedRun run, long runOffset)
        {
            switch (run.Type)
            {
                case RunType.ZlibCompressed:
                    _stream.Position = run.CompOffset + 2; // 2 byte zlib header
                    int toCopy = (int)(run.SectorCount * Sizes.Sector);
                    using (DeflateStream ds = new DeflateStream(_stream, CompressionMode.Decompress, true))
                    {
                        Utilities.ReadFully(ds, _buffer, 0, toCopy);
                    }

                    break;

                case RunType.Zeros:
                    break;

                default:
                    throw new NotImplementedException("Unrecognized run type " + run.Type);
            }

            _activeRun = run;
            _bufferStart = (runOffset + run.SectorStart) * Sizes.Sector;
        }
    }
}
