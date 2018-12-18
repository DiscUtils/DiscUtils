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
using DiscUtils.Internal;
using DiscUtils.Streams;

namespace DiscUtils.Vdi
{
    internal class DiskStream : SparseStream
    {
        private const uint BlockFree = unchecked((uint)~0);
        private const uint BlockZero = unchecked((uint)~1);
        private bool _atEof;

        private uint[] _blockTable;
        private readonly HeaderRecord _fileHeader;

        private Stream _fileStream;

        private bool _isDisposed;
        private readonly Ownership _ownsStream;

        private long _position;
        private bool _writeNotified;

        public DiskStream(Stream fileStream, Ownership ownsStream, HeaderRecord fileHeader)
        {
            _fileStream = fileStream;
            _fileHeader = fileHeader;

            _ownsStream = ownsStream;

            ReadBlockTable();
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

        public override IEnumerable<StreamExtent> Extents
        {
            get
            {
                List<StreamExtent> extents = new List<StreamExtent>();

                long blockSize = _fileHeader.BlockSize;
                int i = 0;
                while (i < _blockTable.Length)
                {
                    // Find next stored block
                    while (i < _blockTable.Length && (_blockTable[i] == BlockZero || _blockTable[i] == BlockFree))
                    {
                        ++i;
                    }

                    int start = i;

                    // Find next absent block
                    while (i < _blockTable.Length && _blockTable[i] != BlockZero && _blockTable[i] != BlockFree)
                    {
                        ++i;
                    }

                    if (start != i)
                    {
                        extents.Add(new StreamExtent(start * blockSize, (i - start) * blockSize));
                    }
                }

                return extents;
            }
        }

        public override long Length
        {
            get
            {
                CheckDisposed();
                return _fileHeader.DiskSize;
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
                _position = value;
                _atEof = false;
            }
        }

        public event EventHandler WriteOccurred;

        public override void Flush()
        {
            CheckDisposed();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckDisposed();

            if (_atEof || _position > _fileHeader.DiskSize)
            {
                _atEof = true;
                throw new IOException("Attempt to read beyond end of file");
            }

            if (_position == _fileHeader.DiskSize)
            {
                _atEof = true;
                return 0;
            }

            int maxToRead = (int)Math.Min(count, _fileHeader.DiskSize - _position);
            int numRead = 0;

            while (numRead < maxToRead)
            {
                int block = (int)(_position / _fileHeader.BlockSize);
                int offsetInBlock = (int)(_position % _fileHeader.BlockSize);

                int toRead = Math.Min(maxToRead - numRead, _fileHeader.BlockSize - offsetInBlock);

                if (_blockTable[block] == BlockFree)
                {
                    // TODO: Use parent
                    Array.Clear(buffer, offset + numRead, toRead);
                }
                else if (_blockTable[block] == BlockZero)
                {
                    Array.Clear(buffer, offset + numRead, toRead);
                }
                else
                {
                    long blockOffset = _blockTable[block] * (_fileHeader.BlockSize + _fileHeader.BlockExtraSize);
                    long filePos = _fileHeader.DataOffset + _fileHeader.BlockExtraSize + blockOffset +
                                   offsetInBlock;
                    _fileStream.Position = filePos;
                    StreamUtilities.ReadExact(_fileStream, buffer, offset + numRead, toRead);
                }

                _position += toRead;
                numRead += toRead;
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
                effectiveOffset += _fileHeader.DiskSize;
            }

            _atEof = false;

            if (effectiveOffset < 0)
            {
                throw new IOException("Attempt to move before beginning of disk");
            }
            _position = effectiveOffset;
            return _position;
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

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "Attempt to write negative number of bytes");
            }

            if (_atEof || _position + count > _fileHeader.DiskSize)
            {
                _atEof = true;
                throw new IOException("Attempt to write beyond end of file");
            }

            // On first write, notify event listeners - they just get to find out that some
            // write occurred, not about each write.
            if (!_writeNotified)
            {
                OnWriteOccurred();
                _writeNotified = true;
            }

            int numWritten = 0;
            while (numWritten < count)
            {
                int block = (int)(_position / _fileHeader.BlockSize);
                int offsetInBlock = (int)(_position % _fileHeader.BlockSize);

                int toWrite = Math.Min(count - numWritten, _fileHeader.BlockSize - offsetInBlock);

                // Optimize away zero-writes
                if (_blockTable[block] == BlockZero
                    || (_blockTable[block] == BlockFree && toWrite == _fileHeader.BlockSize))
                {
                    if (Utilities.IsAllZeros(buffer, offset + numWritten, toWrite))
                    {
                        numWritten += toWrite;
                        _position += toWrite;
                        continue;
                    }
                }

                if (_blockTable[block] == BlockFree || _blockTable[block] == BlockZero)
                {
                    byte[] writeBuffer = buffer;
                    int writeBufferOffset = offset + numWritten;

                    if (toWrite != _fileHeader.BlockSize)
                    {
                        writeBuffer = new byte[_fileHeader.BlockSize];
                        if (_blockTable[block] == BlockFree)
                        {
                            // TODO: Use parent stream data...
                        }

                        // Copy actual data into temporary buffer, then this is a full block write.
                        Array.Copy(buffer, offset + numWritten, writeBuffer, offsetInBlock, toWrite);
                        writeBufferOffset = 0;
                    }

                    long blockOffset = (long)_fileHeader.BlocksAllocated *
                                       (_fileHeader.BlockSize + _fileHeader.BlockExtraSize);
                    long filePos = _fileHeader.DataOffset + _fileHeader.BlockExtraSize + blockOffset;

                    _fileStream.Position = filePos;
                    _fileStream.Write(writeBuffer, writeBufferOffset, _fileHeader.BlockSize);

                    _blockTable[block] = (uint)_fileHeader.BlocksAllocated;

                    // Update the file header on disk, to indicate where the next free block is
                    _fileHeader.BlocksAllocated++;
                    _fileStream.Position = PreHeaderRecord.Size;
                    _fileHeader.Write(_fileStream);

                    // Update the block table on disk, to indicate where this block is
                    WriteBlockTableEntry(block);
                }
                else
                {
                    // Existing block, simply overwrite the existing data
                    long blockOffset = _blockTable[block] * (_fileHeader.BlockSize + _fileHeader.BlockExtraSize);
                    long filePos = _fileHeader.DataOffset + _fileHeader.BlockExtraSize + blockOffset +
                                   offsetInBlock;
                    _fileStream.Position = filePos;
                    _fileStream.Write(buffer, offset + numWritten, toWrite);
                }

                numWritten += toWrite;
                _position += toWrite;
            }
        }

        protected override void Dispose(bool disposing)
        {
            _isDisposed = true;
            try
            {
                if (_ownsStream == Ownership.Dispose && _fileStream != null)
                {
                    _fileStream.Dispose();
                    _fileStream = null;
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        protected virtual void OnWriteOccurred()
        {
            EventHandler handler = WriteOccurred;
            if (handler != null)
            {
                handler(this, null);
            }
        }

        private void ReadBlockTable()
        {
            _fileStream.Position = _fileHeader.BlocksOffset;

            byte[] buffer = StreamUtilities.ReadExact(_fileStream, _fileHeader.BlockCount * 4);

            _blockTable = new uint[_fileHeader.BlockCount];
            for (int i = 0; i < _fileHeader.BlockCount; ++i)
            {
                _blockTable[i] = EndianUtilities.ToUInt32LittleEndian(buffer, i * 4);
            }
        }

        private void WriteBlockTableEntry(int block)
        {
            byte[] buffer = new byte[4];
            EndianUtilities.WriteBytesLittleEndian(_blockTable[block], buffer, 0);

            _fileStream.Position = _fileHeader.BlocksOffset + block * 4;
            _fileStream.Write(buffer, 0, 4);
        }

        private void CheckDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException("DiskStream", "Attempt to use disposed stream");
            }
        }
    }
}