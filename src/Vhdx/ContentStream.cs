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

    internal sealed class ContentStream : MappedStream
    {
        private SparseStream _fileStream;
        private BlockAllocationTable _bat;
        private long _length;
        private SparseStream _parentStream;
        private Ownership _ownsParent;

        private long _position;
        private bool _atEof;

        public ContentStream(SparseStream fileStream, BlockAllocationTable bat, long length, SparseStream parentStream, Ownership ownsParent)
        {
            _fileStream = fileStream;
            _bat = bat;
            _length = length;
            _parentStream = parentStream;
            _ownsParent = ownsParent;
        }

        public override IEnumerable<StreamExtent> Extents
        {
            get
            {
                CheckDisposed();

                return StreamExtent.Union(_bat.StoredExtents(0, _length), _parentStream.Extents);
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

            return StreamExtent.Union(_bat.StoredExtents(start, count), _parentStream.GetExtentsInRange(start, count));
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

            SectorDisposition disposition = _bat.GetDisposition(_position);
            if (disposition == SectorDisposition.Stored)
            {
                int bytesPresent = (int)_bat.ContiguousBytes(_position, count);

                _fileStream.Position = _bat.GetFilePosition(_position);
                int read = (int)_fileStream.Read(buffer, offset, bytesPresent);

                _position += read;

                return read;
            }
            else if (disposition == SectorDisposition.Parent)
            {
                int bytesFromParent = (int)_bat.ContiguousBytes(_position, count);

                _parentStream.Position = _position;
                int read = _parentStream.Read(buffer, offset, bytesFromParent);

                _position += read;

                return read;
            }
            else
            {
                int bytesZero = (int)_bat.ContiguousBytes(_position, count);

                Array.Clear(buffer, offset, bytesZero);

                _position += bytesZero;

                return bytesZero;
            }
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

            int writtenSoFar = 0;

            while (writtenSoFar < count)
            {
                SectorDisposition disposition = _bat.GetDisposition(_position);
                if (disposition == SectorDisposition.Stored)
                {
                    int bytesPresent = (int)_bat.ContiguousBytes(_position, count - writtenSoFar);
                    int toWrite = Math.Min(bytesPresent, count - writtenSoFar);

                    _fileStream.Position = _bat.GetFilePosition(_position);
                    _fileStream.Write(buffer, offset, toWrite);

                    _position += toWrite;
                    writtenSoFar += count;
                }
                else if (disposition == SectorDisposition.Parent)
                {
                    throw new NotImplementedException();
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
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

        private void CheckDisposed()
        {
            if (_parentStream == null)
            {
                throw new ObjectDisposedException("ContentStream", "Attempt to use closed stream");
            }
        }
    }
}
