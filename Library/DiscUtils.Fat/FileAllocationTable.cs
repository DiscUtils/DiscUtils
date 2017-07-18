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

using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.Fat
{
    internal class FileAllocationTable
    {
        private readonly FatBuffer _buffer;
        private readonly ushort _firstFatSector;
        private readonly byte _numFats;
        private readonly Stream _stream;

        public FileAllocationTable(FatType type, Stream stream, ushort firstFatSector, uint fatSize, byte numFats,
                                   byte activeFat)
        {
            _stream = stream;
            _firstFatSector = firstFatSector;
            _numFats = numFats;

            _stream.Position = (firstFatSector + fatSize * activeFat) * Sizes.Sector;
            _buffer = new FatBuffer(type, StreamUtilities.ReadExact(_stream, (int)(fatSize * Sizes.Sector)));
        }

        internal bool IsFree(uint val)
        {
            return _buffer.IsFree(val);
        }

        internal bool IsEndOfChain(uint val)
        {
            return _buffer.IsEndOfChain(val);
        }

        internal bool IsBadCluster(uint val)
        {
            return _buffer.IsBadCluster(val);
        }

        internal uint GetNext(uint cluster)
        {
            return _buffer.GetNext(cluster);
        }

        internal void SetEndOfChain(uint cluster)
        {
            _buffer.SetEndOfChain(cluster);
        }

        internal void SetBadCluster(uint cluster)
        {
            _buffer.SetBadCluster(cluster);
        }

        internal void SetNext(uint cluster, uint next)
        {
            _buffer.SetNext(cluster, next);
        }

        internal void Flush()
        {
            for (int i = 0; i < _numFats; ++i)
            {
                _buffer.WriteDirtyRegions(_stream, _firstFatSector * Sizes.Sector + _buffer.Size * i);
            }

            _buffer.ClearDirtyRegions();
        }

        internal bool TryGetFreeCluster(out uint cluster)
        {
            return _buffer.TryGetFreeCluster(out cluster);
        }

        internal void FreeChain(uint head)
        {
            _buffer.FreeChain(head);
        }
 
        internal int NumEntries
        {
            get { return _buffer.NumEntries; }
        }
    }
}
