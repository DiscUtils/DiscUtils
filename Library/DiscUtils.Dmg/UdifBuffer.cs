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
using System.IO.Compression;
using DiscUtils.Compression;
using DiscUtils.Streams;
using Buffer=DiscUtils.Streams.Buffer;

namespace DiscUtils.Dmg
{
    internal class UdifBuffer : Buffer
    {
        private CompressedRun _activeRun;
        private long _activeRunOffset;

        private byte[] _decompBuffer;
        private readonly ResourceFork _resources;
        private readonly long _sectorCount;
        private readonly Stream _stream;

        public UdifBuffer(Stream stream, ResourceFork resources, long sectorCount)
        {
            _stream = stream;
            _resources = resources;
            _sectorCount = sectorCount;

            Blocks = new List<CompressedBlock>();

            foreach (Resource resource in _resources.GetAllResources("blkx"))
            {
                Blocks.Add(((BlkxResource)resource).Block);
            }
        }

        public List<CompressedBlock> Blocks { get; }

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

                int bufferOffset = (int)(currentPos - (_activeRunOffset + _activeRun.SectorStart * Sizes.Sector));
                int toCopy = (int)Math.Min(_activeRun.SectorCount * Sizes.Sector - bufferOffset, count - totalCopied);

                switch (_activeRun.Type)
                {
                    case RunType.Zeros:
                        Array.Clear(buffer, offset + totalCopied, toCopy);
                        break;

                    case RunType.Raw:
                        _stream.Position = _activeRun.CompOffset + bufferOffset;
                        StreamUtilities.ReadExact(_stream, buffer, offset + totalCopied, toCopy);
                        break;

                    case RunType.AdcCompressed:
                    case RunType.ZlibCompressed:
                    case RunType.BZlibCompressed:
                        Array.Copy(_decompBuffer, bufferOffset, buffer, offset + totalCopied, toCopy);
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
            StreamExtent lastRun = null;

            foreach (CompressedBlock block in Blocks)
            {
                if ((block.FirstSector + block.SectorCount) * Sizes.Sector < start)
                {
                    // Skip blocks before start of range
                    continue;
                }

                if (block.FirstSector * Sizes.Sector > start + count)
                {
                    // Skip blocks after end of range
                    continue;
                }

                foreach (CompressedRun run in block.Runs)
                {
                    if (run.SectorCount > 0 && run.Type != RunType.Zeros)
                    {
                        long thisRunStart = (block.FirstSector + run.SectorStart) * Sizes.Sector;
                        long thisRunEnd = thisRunStart + run.SectorCount * Sizes.Sector;

                        thisRunStart = Math.Max(thisRunStart, start);
                        thisRunEnd = Math.Min(thisRunEnd, start + count);

                        long thisRunLength = thisRunEnd - thisRunStart;

                        if (thisRunLength > 0)
                        {
                            if (lastRun != null && lastRun.Start + lastRun.Length == thisRunStart)
                            {
                                lastRun = new StreamExtent(lastRun.Start, lastRun.Length + thisRunLength);
                            }
                            else
                            {
                                if (lastRun != null)
                                {
                                    yield return lastRun;
                                }

                                lastRun = new StreamExtent(thisRunStart, thisRunLength);
                            }
                        }
                    }
                }
            }

            if (lastRun != null)
            {
                yield return lastRun;
            }
        }

        private static int ADCDecompress(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer,
                                         int outputOffset)
        {
            int consumed = 0;
            int written = 0;

            while (consumed < inputCount)
            {
                byte focusByte = inputBuffer[inputOffset + consumed];
                if ((focusByte & 0x80) != 0)
                {
                    // Data Run
                    int chunkSize = (focusByte & 0x7F) + 1;
                    Array.Copy(inputBuffer, inputOffset + consumed + 1, outputBuffer, outputOffset + written, chunkSize);

                    consumed += chunkSize + 1;
                    written += chunkSize;
                }
                else if ((focusByte & 0x40) != 0)
                {
                    // 3 byte code
                    int chunkSize = (focusByte & 0x3F) + 4;
                    int offset = EndianUtilities.ToUInt16BigEndian(inputBuffer, inputOffset + consumed + 1);

                    for (int i = 0; i < chunkSize; ++i)
                    {
                        outputBuffer[outputOffset + written + i] =
                            outputBuffer[outputOffset + written + i - offset - 1];
                    }

                    consumed += 3;
                    written += chunkSize;
                }
                else
                {
                    // 2 byte code
                    int chunkSize = ((focusByte & 0x3F) >> 2) + 3;
                    int offset = ((focusByte & 0x3) << 8) + (inputBuffer[inputOffset + consumed + 1] & 0xFF);

                    for (int i = 0; i < chunkSize; ++i)
                    {
                        outputBuffer[outputOffset + written + i] =
                            outputBuffer[outputOffset + written + i - offset - 1];
                    }

                    consumed += 2;
                    written += chunkSize;
                }
            }

            return written;
        }

        private void LoadRun(long pos)
        {
            if (_activeRun != null
                && pos >= _activeRunOffset + _activeRun.SectorStart * Sizes.Sector
                && pos < _activeRunOffset + (_activeRun.SectorStart + _activeRun.SectorCount) * Sizes.Sector)
            {
                return;
            }

            long findSector = pos / 512;
            foreach (CompressedBlock block in Blocks)
            {
                if (block.FirstSector <= findSector && block.FirstSector + block.SectorCount > findSector)
                {
                    // Make sure the decompression buffer is big enough
                    if (_decompBuffer == null || _decompBuffer.Length < block.DecompressBufferRequested * Sizes.Sector)
                    {
                        _decompBuffer = new byte[block.DecompressBufferRequested * Sizes.Sector];
                    }

                    foreach (CompressedRun run in block.Runs)
                    {
                        if (block.FirstSector + run.SectorStart <= findSector &&
                            block.FirstSector + run.SectorStart + run.SectorCount > findSector)
                        {
                            LoadRun(run);
                            _activeRunOffset = block.FirstSector * Sizes.Sector;
                            return;
                        }
                    }

                    throw new IOException("No run for sector " + findSector + " in block starting at " +
                                          block.FirstSector);
                }
            }

            throw new IOException("No block for sector " + findSector);
        }

        private void LoadRun(CompressedRun run)
        {
            int toCopy = (int)(run.SectorCount * Sizes.Sector);

            switch (run.Type)
            {
                case RunType.ZlibCompressed:
                    _stream.Position = run.CompOffset + 2; // 2 byte zlib header
                    using (DeflateStream ds = new DeflateStream(_stream, CompressionMode.Decompress, true))
                    {
                        StreamUtilities.ReadExact(ds, _decompBuffer, 0, toCopy);
                    }

                    break;

                case RunType.AdcCompressed:
                    _stream.Position = run.CompOffset;
                    byte[] compressed = StreamUtilities.ReadExact(_stream, (int)run.CompLength);
                    if (ADCDecompress(compressed, 0, compressed.Length, _decompBuffer, 0) != toCopy)
                    {
                        throw new InvalidDataException("Run too short when decompressed");
                    }

                    break;

                case RunType.BZlibCompressed:
                    using (
                        BZip2DecoderStream ds =
                            new BZip2DecoderStream(new SubStream(_stream, run.CompOffset, run.CompLength),
                                Ownership.None))
                    {
                        StreamUtilities.ReadExact(ds, _decompBuffer, 0, toCopy);
                    }

                    break;

                case RunType.Zeros:
                case RunType.Raw:
                    break;

                default:
                    throw new NotImplementedException("Unrecognized run type " + run.Type);
            }

            _activeRun = run;
        }
    }
}