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

using System;
using System.Collections.Generic;
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
        protected byte[] _grainTable;

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
                    }
                    _fileStream = null;

                    if (_ownsParentDiskStream == Ownership.Dispose && _parentDiskStream != null)
                    {
                        _parentDiskStream.Dispose();
                    }
                    _parentDiskStream = null;
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

                    if (GetGrainTableEntry(grain) == 0)
                    {
                        _parentDiskStream.Position = _position + _diskOffset;
                        numRead = _parentDiskStream.Read(buffer, offset + totalRead, numToRead);
                    }
                    else
                    {
                        int bufferOffset = offset + totalRead;
                        long grainStart = ((long)GetGrainTableEntry(grain)) * Sizes.Sector;
                        numRead = ReadGrain(buffer, bufferOffset, grainStart, grainOffset, numToRead);
                    }
                }

                _position += numRead;
                totalRead += numRead;
            } while (numRead != 0 && totalRead < maxToRead);

            return totalRead;
        }

        protected uint GetGrainTableEntry(int grain)
        {
            return Utilities.ToUInt32LittleEndian(_grainTable, grain * 4);
        }

        protected void SetGrainTableEntry(int grain, uint value)
        {
            Utilities.WriteBytesLittleEndian(value, _grainTable, grain * 4);
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

        public override IEnumerable<StreamExtent> GetExtentsInRange(long start, long count)
        {
            CheckDisposed();

            long maxCount = Math.Min(Length, start + count) - start;
            if (maxCount < 0)
            {
                return new StreamExtent[0];
            }

            var parentExtents = _parentDiskStream.GetExtentsInRange(_diskOffset + start, maxCount);
            parentExtents = StreamExtent.Offset(parentExtents, -_diskOffset);

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
            long pos = FindNextPresentGrain(Utilities.RoundDown(start, _header.GrainSize * Sizes.Sector), maxPos);
            while (pos < maxPos)
            {
                long end = FindNextAbsentGrain(pos, maxPos);
                yield return new StreamExtent(pos, end - pos);

                pos = FindNextPresentGrain(end, maxPos);
            }
        }

        private long FindNextPresentGrain(long pos, long maxPos)
        {
            int grainSize = (int)(_header.GrainSize * Sizes.Sector);

            bool foundStart = false;
            while (pos < maxPos && !foundStart)
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

                    if (GetGrainTableEntry(grain) == 0)
                    {
                        pos += grainSize;
                    }
                    else
                    {
                        foundStart = true;
                    }
                }
            }
            return Math.Min(pos, maxPos);
        }

        private long FindNextAbsentGrain(long pos, long maxPos)
        {
            int grainSize = (int)(_header.GrainSize * Sizes.Sector);

            bool foundEnd = false;
            while (pos < maxPos && !foundEnd)
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

                    if (GetGrainTableEntry(grain) == 0)
                    {
                        foundEnd = true;
                    }
                    else
                    {
                        pos += grainSize;
                    }
                }
            }
            return Math.Min(pos, maxPos);
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

            byte[] newGrainTable = _grainTable;
            _grainTable = null;
            if (newGrainTable == null)
            {
                newGrainTable = new byte[_header.NumGTEsPerGT * 4];
            }

            _fileStream.Position = ((long)_globalDirectory[index]) * Sizes.Sector;
            Utilities.ReadFully(_fileStream, newGrainTable, 0, newGrainTable.Length);

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
