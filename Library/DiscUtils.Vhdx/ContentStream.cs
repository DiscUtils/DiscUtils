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

using System;
using System.Collections.Generic;
using System.IO;
using DiscUtils.Internal;
using DiscUtils.Streams;

namespace DiscUtils.Vhdx
{
    internal sealed class ContentStream : MappedStream
    {
        private bool _atEof;
        private readonly Stream _batStream;
        private readonly bool? _canWrite;

        private readonly ObjectCache<int, Chunk> _chunks;
        private readonly FileParameters _fileParameters;
        private readonly SparseStream _fileStream;
        private readonly FreeSpaceTable _freeSpaceTable;
        private readonly long _length;
        private readonly Metadata _metadata;
        private readonly Ownership _ownsParent;
        private SparseStream _parentStream;
        private long _position;

        public ContentStream(SparseStream fileStream, bool? canWrite, Stream batStream, FreeSpaceTable freeSpaceTable,
                             Metadata metadata, long length, SparseStream parentStream, Ownership ownsParent)
        {
            _fileStream = fileStream;
            _canWrite = canWrite;
            _batStream = batStream;
            _freeSpaceTable = freeSpaceTable;
            _metadata = metadata;
            _fileParameters = _metadata.FileParameters;
            _length = length;
            _parentStream = parentStream;
            _ownsParent = ownsParent;

            _chunks = new ObjectCache<int, Chunk>();
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

                return _canWrite ?? _fileStream.CanWrite;
            }
        }

        public override IEnumerable<StreamExtent> Extents
        {
            get
            {
                CheckDisposed();

                // For now, report the complete file contents
                return GetExtentsInRange(0, Length);
            }
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

        public override void Flush()
        {
            CheckDisposed();

            throw new NotImplementedException();
        }

        public override IEnumerable<StreamExtent> MapContent(long start, long length)
        {
            CheckDisposed();

            throw new NotImplementedException();
        }

        public override IEnumerable<StreamExtent> GetExtentsInRange(long start, long count)
        {
            CheckDisposed();

            return
                StreamExtent.Intersect(
                    StreamExtent.Union(GetExtentsRaw(start, count), _parentStream.GetExtentsInRange(start, count)),
                    new StreamExtent(start, count));
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

            if (_position % _metadata.LogicalSectorSize != 0 || count % _metadata.LogicalSectorSize != 0)
            {
                throw new IOException("Unaligned read");
            }

            int totalToRead = (int)Math.Min(_length - _position, count);
            int totalRead = 0;

            while (totalRead < totalToRead)
            {
                int chunkIndex;
                int blockIndex;
                int sectorIndex;
                Chunk chunk = GetChunk(_position + totalRead, out chunkIndex, out blockIndex, out sectorIndex);

                int blockOffset = (int)(sectorIndex * _metadata.LogicalSectorSize);
                int blockBytesRemaining = (int)(_fileParameters.BlockSize - blockOffset);

                PayloadBlockStatus blockStatus = chunk.GetBlockStatus(blockIndex);
                if (blockStatus == PayloadBlockStatus.FullyPresent)
                {
                    _fileStream.Position = chunk.GetBlockPosition(blockIndex) + blockOffset;
                    int read = StreamUtilities.ReadMaximum(_fileStream, buffer, offset + totalRead,
                        Math.Min(blockBytesRemaining, totalToRead - totalRead));

                    totalRead += read;
                }
                else if (blockStatus == PayloadBlockStatus.PartiallyPresent)
                {
                    BlockBitmap bitmap = chunk.GetBlockBitmap(blockIndex);

                    bool present;
                    int numSectors = bitmap.ContiguousSectors(sectorIndex, out present);
                    int toRead = (int)Math.Min(numSectors * _metadata.LogicalSectorSize, totalToRead - totalRead);
                    int read;

                    if (present)
                    {
                        _fileStream.Position = chunk.GetBlockPosition(blockIndex) + blockOffset;
                        read = StreamUtilities.ReadMaximum(_fileStream, buffer, offset + totalRead, toRead);
                    }
                    else
                    {
                        _parentStream.Position = _position + totalRead;
                        read = StreamUtilities.ReadMaximum(_parentStream, buffer, offset + totalRead, toRead);
                    }

                    totalRead += read;
                }
                else if (blockStatus == PayloadBlockStatus.NotPresent)
                {
                    _parentStream.Position = _position + totalRead;
                    int read = StreamUtilities.ReadMaximum(_parentStream, buffer, offset + totalRead,
                        Math.Min(blockBytesRemaining, totalToRead - totalRead));

                    totalRead += read;
                }
                else
                {
                    int zeroed = Math.Min(blockBytesRemaining, totalToRead - totalRead);
                    Array.Clear(buffer, offset + totalRead, zeroed);
                    totalRead += zeroed;
                }
            }

            _position += totalRead;
            return totalRead;
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
                throw new InvalidOperationException("Attempt to write to read-only VHDX");
            }

            if (_position % _metadata.LogicalSectorSize != 0 || count % _metadata.LogicalSectorSize != 0)
            {
                throw new IOException("Unaligned read");
            }

            int totalWritten = 0;

            while (totalWritten < count)
            {
                int chunkIndex;
                int blockIndex;
                int sectorIndex;
                Chunk chunk = GetChunk(_position + totalWritten, out chunkIndex, out blockIndex, out sectorIndex);

                int blockOffset = (int)(sectorIndex * _metadata.LogicalSectorSize);
                int blockBytesRemaining = (int)(_fileParameters.BlockSize - blockOffset);

                PayloadBlockStatus blockStatus = chunk.GetBlockStatus(blockIndex);
                if (blockStatus != PayloadBlockStatus.FullyPresent && blockStatus != PayloadBlockStatus.PartiallyPresent)
                {
                    blockStatus = chunk.AllocateSpaceForBlock(blockIndex);
                }

                int toWrite = Math.Min(blockBytesRemaining, count - totalWritten);
                _fileStream.Position = chunk.GetBlockPosition(blockIndex) + blockOffset;
                _fileStream.Write(buffer, offset + totalWritten, toWrite);

                if (blockStatus == PayloadBlockStatus.PartiallyPresent)
                {
                    BlockBitmap bitmap = chunk.GetBlockBitmap(blockIndex);
                    bool changed = bitmap.MarkSectorsPresent(sectorIndex, (int)(toWrite / _metadata.LogicalSectorSize));

                    if (changed)
                    {
                        chunk.WriteBlockBitmap(blockIndex);
                    }
                }

                totalWritten += toWrite;
            }

            _position += totalWritten;
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (_parentStream != null)
                {
                    if (_ownsParent == Ownership.Dispose)
                    {
                        _parentStream.Dispose();
                    }

                    _parentStream = null;
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private IEnumerable<StreamExtent> GetExtentsRaw(long start, long count)
        {
            long chunkSize = (1L << 23) * _metadata.LogicalSectorSize;
            int chunkRatio = (int)(chunkSize / _metadata.FileParameters.BlockSize);

            long pos = MathUtilities.RoundDown(start, chunkSize);

            while (pos < start + count)
            {
                int chunkIndex;
                int blockIndex;
                int sectorIndex;

                Chunk chunk = GetChunk(pos, out chunkIndex, out blockIndex, out sectorIndex);

                for (int i = 0; i < chunkRatio; ++i)
                {
                    switch (chunk.GetBlockStatus(i))
                    {
                        case PayloadBlockStatus.NotPresent:
                        case PayloadBlockStatus.Undefined:
                        case PayloadBlockStatus.Unmapped:
                        case PayloadBlockStatus.Zero:
                            break;
                        default:
                            yield return
                                new StreamExtent(pos + i * _metadata.FileParameters.BlockSize,
                                    _metadata.FileParameters.BlockSize);
                            break;
                    }
                }

                pos += chunkSize;
            }
        }

        private Chunk GetChunk(long position, out int chunk, out int block, out int sector)
        {
            long chunkSize = (1L << 23) * _metadata.LogicalSectorSize;
            int chunkRatio = (int)(chunkSize / _metadata.FileParameters.BlockSize);

            chunk = (int)(position / chunkSize);
            long chunkOffset = position % chunkSize;

            block = (int)(chunkOffset / _fileParameters.BlockSize);
            int blockOffset = (int)(chunkOffset % _fileParameters.BlockSize);

            sector = (int)(blockOffset / _metadata.LogicalSectorSize);

            Chunk result = _chunks[chunk];
            if (result == null)
            {
                result = new Chunk(_batStream, _fileStream, _freeSpaceTable, _fileParameters, chunk, chunkRatio);
                _chunks[chunk] = result;
            }

            return result;
        }

        private void CheckDisposed()
        {
            if (_parentStream == null)
            {
                throw new ObjectDisposedException("ContentStream", "Attempt to use closed stream");
            }
        }
    }
}