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
            get { return _header.Capacity * Sizes.Sector; }
        }

        public override long Position
        {
            get { return _position; }
            set { _position = value; }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int grainTable = (int)(_position / _gtCoverage);
            int grainTableOffset = (int)(_position - (grainTable * _gtCoverage));
            int numRead;

            if (!LoadGrainTable(grainTable))
            {
                // Read from parent stream, to at most the end of grain table's coverage
                _parentDiskStream.Position = _position + _diskOffset;
                numRead = _parentDiskStream.Read(buffer, offset, (int)Math.Min(count, _gtCoverage - grainTableOffset));
            }
            else
            {
                int grainSize = (int)(_header.GrainSize * Sizes.Sector);
                int grain = grainTableOffset / grainSize;
                int grainOffset = grainTableOffset - (grain * grainSize);

                int numToRead = Math.Min(count, grainSize - grainOffset);

                if (_grainTable[grain] == 0)
                {
                    _parentDiskStream.Position = _position + _diskOffset;
                    numRead = _parentDiskStream.Read(buffer, offset, numToRead);
                }
                else
                {
                    _fileStream.Position = (_grainTable[grain] * Sizes.Sector) + grainOffset;
                    numRead = _fileStream.Read(buffer, offset, numToRead);
                }
            }

            _position += numRead;
            return numRead;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
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
                effectiveOffset += _header.Capacity * Sizes.Sector;
            }

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
                // Note: For now we only go down to grain table granularity (large chunks), we don't inspect the
                // grain tables themselves to indicate which sectors are present.
                List<StreamExtent> extents = new List<StreamExtent>();

                long blockSize = _gtCoverage;
                int i = 0;
                while (i < _globalDirectory.Length)
                {
                    // Find next stored block
                    while (i < _globalDirectory.Length && _globalDirectory[i] == 0)
                    {
                        ++i;
                    }
                    int start = i;

                    // Find next absent block
                    while (i < _globalDirectory.Length && _globalDirectory[i] != 0)
                    {
                        ++i;
                    }

                    if (start != i)
                    {
                        extents.Add(new StreamExtent(start * blockSize, (i - start) * blockSize));
                    }
                }

                var parentExtents = StreamExtent.Intersect(_parentDiskStream.Extents, new StreamExtent[] { new StreamExtent(_diskOffset, Length) });
                parentExtents = StreamExtent.Offset(parentExtents, -_diskOffset);
                return StreamExtent.Union(extents, parentExtents);
            }
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

            _fileStream.Position = _globalDirectory[index] * Sizes.Sector;
            byte[] buffer = Utilities.ReadFully(_fileStream, (int)(_header.NumGTEsPerGT * 4));

            for (int i = 0; i < _header.NumGTEsPerGT; ++i)
            {
                newGrainTable[i] = Utilities.ToUInt32LittleEndian(buffer, i * 4);
            }

            _currentGrainTable = index;
            _grainTable = newGrainTable;
            return true;
        }
    }
}
