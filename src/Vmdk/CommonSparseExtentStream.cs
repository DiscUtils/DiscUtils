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
using System.Linq;
using System.Text;
using System.IO;

namespace DiscUtils.Vmdk
{
    internal abstract class CommonSparseExtentStream : SparseStream
    {
        /// <summary>
        /// Stream containing the sparse extent.
        /// </summary>
        protected Stream _fileStream;

        /// <summary>
        /// Indicates if this object controls the lifetime of _fileStream.
        /// </summary>
        protected Ownership _ownsFileStream;

        /// <summary>
        /// Offset of this extent within the disk.
        /// </summary>
        protected long _diskOffset;

        /// <summary>
        /// The stream containing the unstored bytes.
        /// </summary>
        protected SparseStream _parentDiskStream;

        /// <summary>
        /// Indicates if this object controls the lifetime of _parentDiskStream.
        /// </summary>
        protected Ownership _ownsParentDiskStream;

        /// <summary>
        /// The Global Directory for this extent.
        /// </summary>
        protected uint[] _globalDirectory;

        /// <summary>
        /// The Redundant Global Directory for this extent.
        /// </summary>
        protected uint[] _redundantGlobalDirectory;

        /// <summary>
        /// The header from the start of the extent.
        /// </summary>
        protected CommonSparseExtentHeader _header;

        /// <summary>
        /// The number of bytes controlled by a single grain table.
        /// </summary>
        protected long _gtCoverage;

        /// <summary>
        /// The current grain that's loaded into _grainTable.
        /// </summary>
        protected int _currentGrainTable;

        /// <summary>
        /// The data corresponding to the current grain (or null).
        /// </summary>
        protected uint[] _grainTable;

        /// <summary>
        /// Current position in the extent.
        /// </summary>
        protected long _position;

        /// <summary>
        /// Indicator to whether end-of-stream has been reached.
        /// </summary>
        protected bool _atEof;

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    if (_ownsFileStream == Ownership.Dispose && _fileStream != null)
                    {
                        _fileStream.Dispose();
                        _fileStream = null;
                    }

                    if (_ownsParentDiskStream == Ownership.Dispose && _parentDiskStream != null)
                    {
                        _parentDiskStream.Dispose();
                        _parentDiskStream = null;
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
                return _header.Capacity * Sizes.Sector;
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

        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckDisposed();

            if (_position > Length)
            {
                _atEof = true;
                throw new IOException("Attempt to read beyond end of stream");
            }

            if (_position == Length)
            {
                if (_atEof)
                {
                    throw new IOException("Attempt to read beyond end of stream");
                }
                else
                {
                    _atEof = true;
                    return 0;
                }
            }


            int maxToRead = (int)Math.Min(count, Length - _position);
            int totalRead = 0;
            int numRead;

            do
            {
                int grainTable = (int)(_position / _gtCoverage);
                int grainTableOffset = (int)(_position - (((long)grainTable) * _gtCoverage));
                numRead = 0;

                if (!LoadGrainTable(grainTable))
                {
                    // Read from parent stream, to at most the end of grain table's coverage
                    _parentDiskStream.Position = _position + _diskOffset;
                    numRead = _parentDiskStream.Read(buffer, offset + totalRead, (int)Math.Min(maxToRead - totalRead, _gtCoverage - grainTableOffset));
                }
                else
                {
                    int grainSize = (int)(_header.GrainSize * Sizes.Sector);
                    int grain = grainTableOffset / grainSize;
                    int grainOffset = grainTableOffset - (grain * grainSize);

                    int numToRead = Math.Min(maxToRead - totalRead, grainSize - grainOffset);

                    if (_grainTable[grain] == 0)
                    {
                        _parentDiskStream.Position = _position + _diskOffset;
                        numRead = _parentDiskStream.Read(buffer, offset + totalRead, numToRead);
                    }
                    else
                    {
                        int bufferOffset = offset + totalRead;
                        long grainStart = ((long)_grainTable[grain]) * Sizes.Sector;
                        numRead = ReadGrain(buffer, bufferOffset, grainStart, grainOffset, numToRead);
                    }
                }

                _position += numRead;
                totalRead += numRead;
            } while (numRead != 0 && totalRead < maxToRead);

            return totalRead;
        }

        public override void SetLength(long value)
        {
            CheckDisposed();

            throw new NotSupportedException();
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
                effectiveOffset += _header.Capacity * Sizes.Sector;
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

        public override IEnumerable<StreamExtent> Extents
        {
            get
            {
                CheckDisposed();

                var parentExtents = StreamExtent.Intersect(_parentDiskStream.Extents, new StreamExtent[] { new StreamExtent(_diskOffset, Length) });
                parentExtents = StreamExtent.Offset(parentExtents, -_diskOffset);
                return StreamExtent.Union(LayerExtents(), parentExtents);
            }
        }

        private IEnumerable<StreamExtent> LayerExtents()
        {
            long length = Length;
            long start = FindNextPresentGrain(0);
            while (start < length)
            {
                long end = FindNextAbsentGrain(start);
                yield return new StreamExtent(start, end - start);

                start = FindNextPresentGrain(end);
            }
        }

        private long FindNextPresentGrain(long pos)
        {
            long length = Length;
            int grainSize = (int)(_header.GrainSize * Sizes.Sector);

            bool foundStart = false;
            while (pos < length && !foundStart)
            {
                int grainTable = (int)(pos / _gtCoverage);

                if (!LoadGrainTable(grainTable))
                {
                    pos += _gtCoverage;
                }
                else
                {
                    int grainTableOffset = (int)(pos - (grainTable * _gtCoverage));

                    int grain = grainTableOffset / grainSize;

                    if (_grainTable[grain] == 0)
                    {
                        pos += grainSize;
                    }
                    else
                    {
                        foundStart = true;
                    }
                }
            }
            long start = pos;
            return start;
        }

        private long FindNextAbsentGrain(long pos)
        {
            long length = Length;
            int grainSize = (int)(_header.GrainSize * Sizes.Sector);

            bool foundEnd = false;
            while (pos < length && !foundEnd)
            {
                int grainTable = (int)(pos / _gtCoverage);

                if (!LoadGrainTable(grainTable))
                {
                    foundEnd = true;
                }
                else
                {
                    int grainTableOffset = (int)(pos - (grainTable * _gtCoverage));

                    int grain = grainTableOffset / grainSize;

                    if (_grainTable[grain] == 0)
                    {
                        foundEnd = true;
                    }
                    else
                    {
                        pos += grainSize;
                    }
                }
            }
            return pos;
        }

        protected virtual int ReadGrain(byte[] buffer, int bufferOffset, long grainStart, int grainOffset, int numToRead)
        {
            _fileStream.Position = grainStart + grainOffset;
            return _fileStream.Read(buffer, bufferOffset, numToRead);
        }

        protected virtual void LoadGlobalDirectory()
        {
            int numGTs = (int)Utilities.Ceil(_header.Capacity * Sizes.Sector, _gtCoverage);

            _globalDirectory = new uint[numGTs];
            _fileStream.Position = _header.GdOffset * Sizes.Sector;
            byte[] gdAsBytes = Utilities.ReadFully(_fileStream, numGTs * 4);
            for (int i = 0; i < _globalDirectory.Length; ++i)
            {
                _globalDirectory[i] = Utilities.ToUInt32LittleEndian(gdAsBytes, i * 4);
            }
        }

        protected bool LoadGrainTable(int index)
        {
            if (_grainTable != null && _currentGrainTable == index)
            {
                return true;
            }

            // This grain table not present in grain directory, so can't load it...
            if (_globalDirectory[index] == 0)
            {
                return false;
            }

            uint[] newGrainTable = _grainTable;
            _grainTable = null;
            if (newGrainTable == null)
            {
                newGrainTable = new uint[_header.NumGTEsPerGT];
            }

            _fileStream.Position = ((long)_globalDirectory[index]) * Sizes.Sector;
            byte[] buffer = Utilities.ReadFully(_fileStream, (int)(_header.NumGTEsPerGT * 4));

            for (int i = 0; i < _header.NumGTEsPerGT; ++i)
            {
                newGrainTable[i] = Utilities.ToUInt32LittleEndian(buffer, i * 4);
            }

            _currentGrainTable = index;
            _grainTable = newGrainTable;
            return true;
        }

        protected void CheckDisposed()
        {
            if (_fileStream == null)
            {
                throw new ObjectDisposedException("CommonSparseExtentStream");
            }
        }
    }
}
