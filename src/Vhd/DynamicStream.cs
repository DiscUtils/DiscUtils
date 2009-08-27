//
// Copyright (c) 2008-2009, Kenneth Bell
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

namespace DiscUtils.Vhd
{
    internal class DynamicStream : SparseStream
    {
        private Stream _fileStream;
        private DynamicHeader _dynamicHeader;
        private long _length;
        private SparseStream _parentStream;
        private Ownership _ownsParentStream;

        private long _position;
        private bool _atEof;
        private uint[] _blockAllocationTable;
        private byte[][] _blockBitmaps;
        private int _blockBitmapSize;

        public DynamicStream(Stream fileStream, DynamicHeader dynamicHeader, long length, SparseStream parentStream, Ownership ownsParentStream)
        {
            if (fileStream == null)
            {
                throw new ArgumentNullException("fileStream");
            }
            if (dynamicHeader == null)
            {
                throw new ArgumentNullException("dynamicHeader");
            }
            if (parentStream == null)
            {
                throw new ArgumentNullException("parentStream");
            }
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException("length", length, "Negative lengths not allowed");
            }

            _fileStream = fileStream;
            _dynamicHeader = dynamicHeader;
            _length = length;
            _parentStream = parentStream;
            _ownsParentStream = ownsParentStream;

            ReadBlockAllocationTable();

            _blockBitmaps = new byte[_dynamicHeader.MaxTableEntries][];
            _blockBitmapSize = (int)Utilities.RoundUp((_dynamicHeader.BlockSize / Utilities.SectorSize) / 8, Utilities.SectorSize);
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    if (_ownsParentStream == Ownership.Dispose && _parentStream != null)
                    {
                        _parentStream.Dispose();
                        _parentStream = null;
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        public override bool CanRead
        {
            get
            {
                CheckDisposed();
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                CheckDisposed();
                return true;
            }
        }

        public override bool CanWrite
        {
            get
            {
                CheckDisposed();
                return _fileStream.CanWrite;
            }
        }

        public override void Flush()
        {
            CheckDisposed();
        }

        public override long Length
        {
            get
            {
                CheckDisposed();
                return _length;
            }
        }

        public override long Position
        {
            get
            {
                CheckDisposed();
                return _position;
            }
            set
            {
                CheckDisposed();
                _atEof = false;
                _position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckDisposed();

            if (_atEof || _position > _length)
            {
                _atEof = true;
                throw new IOException("Attempt to read beyond end of file");
            }

            if (_position == _length)
            {
                _atEof = true;
                return 0;
            }

            int maxToRead = (int)Math.Min(count, _length - _position);
            int numRead = 0;

            while (numRead < maxToRead)
            {
                long block = _position / _dynamicHeader.BlockSize;
                uint offsetInBlock = (uint)(_position % _dynamicHeader.BlockSize);

                if (PopulateBlockBitmap(block))
                {
                    int sectorInBlock = (int)(offsetInBlock / Utilities.SectorSize);
                    int offsetInSector = (int)(offsetInBlock % Utilities.SectorSize);
                    int toRead = Math.Min(maxToRead - numRead, 512 - offsetInSector);

                    byte mask = (byte)(1 << (7 - (sectorInBlock % 8)));
                    if ((_blockBitmaps[block][sectorInBlock / 8] & mask) != 0)
                    {
                        _fileStream.Position = (((long)_blockAllocationTable[block]) + sectorInBlock) * Utilities.SectorSize + _blockBitmapSize + offsetInSector;
                        if (Utilities.ReadFully(_fileStream, buffer, offset + numRead, toRead) != toRead)
                        {
                            throw new IOException("Failed to read entire sector");
                        }
                    }
                    else
                    {
                        _parentStream.Position = _position;
                        Utilities.ReadFully(_parentStream, buffer, offset + numRead, toRead);
                    }
                    numRead += toRead;
                    _position += toRead;
                }
                else
                {
                    int toRead = Math.Min(maxToRead - numRead, (int)(_dynamicHeader.BlockSize - offsetInBlock));
                    _parentStream.Position = _position;
                    Utilities.ReadFully(_parentStream, buffer, offset + numRead, toRead);
                    numRead += toRead;
                    _position += toRead;
                }
            }

            return numRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            CheckDisposed();

            long effectiveOffset = offset;
            if (origin == SeekOrigin.Current)
            {
                effectiveOffset += _position;
            }
            else if (origin == SeekOrigin.End)
            {
                effectiveOffset += _length;
            }

            _atEof = false;

            if (offset < 0)
            {
                throw new IOException("Attempt to move before beginning of disk");
            }
            else
            {
                _position = effectiveOffset;
                return _position;
            }
        }

        public override void SetLength(long value)
        {
            CheckDisposed();

            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            CheckDisposed();

            if (!CanWrite)
            {
                throw new IOException("Attempt to write to read-only stream");
            }

            int numWritten = 0;

            while (numWritten < count)
            {
                long block = _position / _dynamicHeader.BlockSize;
                uint offsetInBlock = (uint)(_position % _dynamicHeader.BlockSize);

                if (!PopulateBlockBitmap(block))
                {
                    AllocateBlock(block);
                }

                int sectorInBlock = (int)(offsetInBlock / Utilities.SectorSize);
                int offsetInSector = (int)(offsetInBlock % Utilities.SectorSize);
                int toWrite = (int)Math.Min(count - numWritten, _dynamicHeader.BlockSize - offsetInBlock);

                // Need to read - we're not handling a full sector
                if (offsetInSector != 0 || toWrite < Utilities.SectorSize)
                {
                    // Reduce the write to just the end of the current sector
                    toWrite = Math.Min(count - numWritten, Utilities.SectorSize - offsetInSector);

                    byte sectorMask = (byte)(1 << (7 - (sectorInBlock % 8)));

                    long sectorStart = (((long)_blockAllocationTable[block]) + sectorInBlock) * Utilities.SectorSize + _blockBitmapSize;

                    // Get the existing sector data (if any), or otherwise the parent's content
                    byte[] sectorBuffer;
                    if ((_blockBitmaps[block][sectorInBlock / 8] & sectorMask) != 0)
                    {
                        _fileStream.Position = sectorStart;
                        sectorBuffer = Utilities.ReadFully(_fileStream, Utilities.SectorSize);
                    }
                    else
                    {
                        _parentStream.Position = ((_position / Utilities.SectorSize) * Utilities.SectorSize);
                        sectorBuffer = Utilities.ReadFully(_parentStream, Utilities.SectorSize);
                    }

                    // Overlay as much data as we have for this sector
                    Array.Copy(buffer, offset + numWritten, sectorBuffer, offsetInSector, toWrite);

                    // Write the sector back
                    _fileStream.Position = sectorStart;
                    _fileStream.Write(sectorBuffer, 0, Utilities.SectorSize);

                    // Update the in-memory block bitmap
                    _blockBitmaps[block][sectorInBlock / 8] |= sectorMask;
                }
                else
                {
                    // Processing at least one whole sector, just write (after making sure to trim any partial sectors from the end)...
                    toWrite = (toWrite / Utilities.SectorSize) * Utilities.SectorSize;

                    _fileStream.Position = (((long)_blockAllocationTable[block]) + sectorInBlock) * Utilities.SectorSize + _blockBitmapSize;
                    _fileStream.Write(buffer, offset + numWritten, toWrite);

                    // Update all of the bits in the block bitmap
                    for (int i = offset; i < offset + toWrite; i += Utilities.SectorSize)
                    {
                        byte sectorMask = (byte)(1 << (7 - (sectorInBlock % 8)));
                        _blockBitmaps[block][sectorInBlock / 8] |= sectorMask;
                        sectorInBlock++;
                    }

                }

                WriteBlockBitmap(block);

                numWritten += toWrite;
                _position += toWrite;
            }

            _atEof = false;
        }

        public override IEnumerable<StreamExtent> GetExtentsInRange(long start, long count)
        {
            CheckDisposed();

            long maxCount = Math.Min(Length, start + count) - start;
            if (maxCount < 0)
            {
                return new StreamExtent[0];
            }

            var parentExtents = _parentStream.GetExtentsInRange(start, maxCount);

            var result = StreamExtent.Union(LayerExtents(start, maxCount), parentExtents);
            result = StreamExtent.Intersect(result, new StreamExtent[] { new StreamExtent(start, maxCount) });
            return result;
        }

        public override IEnumerable<StreamExtent> Extents
        {
            get { return GetExtentsInRange(0, Length); }
        }

        private IEnumerable<StreamExtent> LayerExtents(long start, long count)
        {
            long maxPos = start + count;
            long pos = FindNextPresentSector(Utilities.RoundDown(start, Utilities.SectorSize), maxPos);
            while (pos < maxPos)
            {
                long end = FindNextAbsentSector(pos, maxPos);
                yield return new StreamExtent(pos, end - pos);

                pos = FindNextPresentSector(end, maxPos);
            }
        }

        private long FindNextPresentSector(long pos, long maxPos)
        {
            bool foundStart = false;
            while (pos < maxPos && !foundStart)
            {
                long block = pos / _dynamicHeader.BlockSize;

                if (!PopulateBlockBitmap(block))
                {
                    pos += _dynamicHeader.BlockSize;
                }
                else
                {
                    uint offsetInBlock = (uint)(pos % _dynamicHeader.BlockSize);
                    int sectorInBlock = (int)(offsetInBlock / Utilities.SectorSize);

                    if (_blockBitmaps[block][sectorInBlock / 8] == 0)
                    {
                        pos += (8 - (sectorInBlock % 8)) * Utilities.SectorSize;
                    }
                    else
                    {
                        byte mask = (byte)(1 << (7 - (sectorInBlock % 8)));
                        if ((_blockBitmaps[block][sectorInBlock / 8] & mask) != 0)
                        {
                            foundStart = true;
                        }
                        else
                        {
                            pos += Utilities.SectorSize;
                        }
                    }
                }
            }

            return Math.Min(pos, maxPos);
        }

        private long FindNextAbsentSector(long pos, long maxPos)
        {
            bool foundEnd = false;
            while (pos < maxPos && !foundEnd)
            {
                long block = pos / _dynamicHeader.BlockSize;

                if (!PopulateBlockBitmap(block))
                {
                    foundEnd = true;
                }
                else
                {
                    uint offsetInBlock = (uint)(pos % _dynamicHeader.BlockSize);
                    int sectorInBlock = (int)(offsetInBlock / Utilities.SectorSize);

                    if (_blockBitmaps[block][sectorInBlock / 8] == 0xFF)
                    {
                        pos += (8 - (sectorInBlock % 8)) * Utilities.SectorSize;
                    }
                    else
                    {
                        byte mask = (byte)(1 << (7 - (sectorInBlock % 8)));
                        if ((_blockBitmaps[block][sectorInBlock / 8] & mask) == 0)
                        {
                            foundEnd = true;
                        }
                        else
                        {
                            pos += Utilities.SectorSize;
                        }
                    }
                }
            }

            return Math.Min(pos, maxPos);
        }

        private void ReadBlockAllocationTable()
        {
            _fileStream.Position = _dynamicHeader.TableOffset;
            byte[] data = Utilities.ReadFully(_fileStream, _dynamicHeader.MaxTableEntries * 4);

            uint[] bat = new uint[_dynamicHeader.MaxTableEntries];
            for (int i = 0; i < _dynamicHeader.MaxTableEntries; ++i)
            {
                bat[i] = Utilities.ToUInt32BigEndian(data, i * 4);
            }

            _blockAllocationTable = bat;
        }

        private bool PopulateBlockBitmap(long block)
        {
            if (_blockBitmaps[block] != null)
            {
                // Nothing to do...
                return true;
            }

            if (_blockAllocationTable[block] == uint.MaxValue)
            {
                // No such block stored...
                return false;
            }

            // Read in bitmap
            _fileStream.Position = ((long)_blockAllocationTable[block]) * Utilities.SectorSize;
            _blockBitmaps[block] = Utilities.ReadFully(_fileStream, _blockBitmapSize);
            return true;
        }

        private void AllocateBlock(long block)
        {
            if (_blockAllocationTable[block] != uint.MaxValue)
            {
                throw new ArgumentException("Attempt to allocate existing block");
            }

            long newBlockStart = _fileStream.Length - 512;

            _fileStream.Position = newBlockStart;
            byte[] footer = Utilities.ReadFully(_fileStream, Utilities.SectorSize);
            if (Utilities.BytesToString(footer, 0, 8) != Footer.FileCookie)
            {
                // If the footer is invalid, assume it's just missing, so next block goes on
                // end of file, and we need the copy from the front of the file to put on the
                // end (after the new block)
                newBlockStart = _fileStream.Length;
                Utilities.ReadFully(_fileStream, footer, 0, Utilities.SectorSize);
            }

            // Create and write new sector bitmap
            byte[] bitmap = new byte[_blockBitmapSize];
            _fileStream.Position = newBlockStart;
            _fileStream.Write(bitmap, 0, _blockBitmapSize);
            _blockBitmaps[block] = bitmap;

            // Write the new footer
            _fileStream.Position = newBlockStart + _blockBitmapSize + _dynamicHeader.BlockSize;
            _fileStream.Write(footer, 0, footer.Length);

            // Update the BAT entry for the new block
            byte[] entryBuffer = new byte[4];
            Utilities.WriteBytesBigEndian((uint)(newBlockStart / 512), entryBuffer, 0);
            _fileStream.Position = _dynamicHeader.TableOffset + (block * 4);
            _fileStream.Write(entryBuffer, 0, 4);
            _blockAllocationTable[block] = (uint)(newBlockStart / 512);
        }

        private void WriteBlockBitmap(long block)
        {
            // Read in bitmap
            _fileStream.Position = ((long)_blockAllocationTable[block]) * Utilities.SectorSize;
            _fileStream.Write(_blockBitmaps[block], 0, _blockBitmapSize);
        }

        private void CheckDisposed()
        {
            if (_parentStream == null)
            {
                throw new ObjectDisposedException("DynamicStream", "Attempt to use closed stream");
            }
        }

    }
}
