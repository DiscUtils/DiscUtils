//
// Copyright (c) 2008, Kenneth Bell
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

namespace DiscUtils.Vhd
{
    internal class DynamicStream : Stream
    {
        private Stream _fileStream;
        private DynamicHeader _dynamicHeader;
        private long _length;

        private long _position;
        private bool _atEof;
        private uint[] _blockAllocationTable;
        private byte[][] _blockBitmaps;

        public DynamicStream(Stream fileStream, DynamicHeader dynamicHeader, long length)
        {
            _fileStream = fileStream;
            _dynamicHeader = dynamicHeader;
            _length = length;

            ReadBlockAllocationTable();

            _blockBitmaps = new byte[_dynamicHeader.MaxTableEntries][];
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return _fileStream.CanWrite; }
        }

        public override void Flush()
        {
        }

        public override long Length
        {
            get { return _length; }
        }

        public override long Position
        {
            get
            {
                return _position;
            }
            set
            {
                _atEof = false;
                _position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
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

            int numRead = 0;

            while (numRead < count)
            {
                long block = _position / _dynamicHeader.BlockSize;
                uint offsetInBlock = (uint)(_position % _dynamicHeader.BlockSize);

                if (PopulateBlockBitmap(block))
                {
                    int sectorInBlock = (int)(offsetInBlock / Utilities.SectorSize);
                    int offsetInSector = (int)(offsetInBlock % Utilities.SectorSize);
                    int toRead = Math.Min(count - numRead, 512 - offsetInSector);

                    byte mask = (byte)(1 << (7 - (sectorInBlock % 8)));
                    if ((_blockBitmaps[block][sectorInBlock / 8] & mask) != 0)
                    {
                        _fileStream.Position = (_blockAllocationTable[block] + sectorInBlock) * Utilities.SectorSize + _blockBitmaps[block].Length + offsetInSector;
                        if (Utilities.ReadFully(_fileStream, buffer, offset + numRead, toRead) != toRead)
                        {
                            throw new IOException("Failed to read entire sector");
                        }
                    }
                    else
                    {
                        Array.Clear(buffer, offset + numRead, toRead);
                    }
                    numRead += toRead;
                    _position += toRead;
                }
                else
                {
                    int toRead = Math.Min(count - numRead, (int)(_dynamicHeader.BlockSize - offsetInBlock));
                    Array.Clear(buffer, offset + numRead, toRead);
                    numRead += toRead;
                    _position += toRead;
                }
            }

            return numRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
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
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
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

                    long sectorStart = (_blockAllocationTable[block] + sectorInBlock) * Utilities.SectorSize + _blockBitmaps[block].Length;

                    // Get the existing sector data (if any)
                    byte[] sectorBuffer = new byte[Utilities.SectorSize];
                    if ((_blockBitmaps[block][sectorInBlock / 8] & sectorMask) != 0)
                    {
                        _fileStream.Position = sectorStart;
                        if (Utilities.ReadFully(_fileStream, sectorBuffer, 0, Utilities.SectorSize) != Utilities.SectorSize)
                        {
                            throw new IOException("Failed to read entire sector");
                        }
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

                    _fileStream.Position = (_blockAllocationTable[block] + sectorInBlock) * Utilities.SectorSize + _blockBitmaps[block].Length;
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
            int bitmapSize = (int)((_dynamicHeader.BlockSize / Utilities.SectorSize) / 8);
            _fileStream.Position = _blockAllocationTable[block] * Utilities.SectorSize;
            _blockBitmaps[block] = Utilities.ReadFully(_fileStream, bitmapSize);
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
            int bitmapSize = (int)((_dynamicHeader.BlockSize / Utilities.SectorSize) / 8);
            byte[] bitmap = new byte[bitmapSize];
            _fileStream.Position = newBlockStart;
            _fileStream.Write(bitmap, 0, bitmapSize);
            _blockBitmaps[block] = bitmap;

            // Write the new footer
            _fileStream.Position = newBlockStart + bitmapSize + _dynamicHeader.BlockSize;
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
            int bitmapSize = (int)((_dynamicHeader.BlockSize / Utilities.SectorSize) / 8);
            _fileStream.Position = _blockAllocationTable[block] * Utilities.SectorSize;
            _fileStream.Write(_blockBitmaps[block], 0, bitmapSize);
        }
    }
}
