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
using System.Collections.Generic;
using System.IO;

namespace DiscUtils.Fat
{
    internal class ClusterFileStream : Stream
    {
        private ClusterReader _reader;
        private FileAllocationTable _fat;
        private uint _length;

        private List<uint> _knownClusters;
        private long _position;

        private uint _currentCluster = 0;
        private byte[] _clusterBuffer;

        internal ClusterFileStream(ClusterReader reader, FileAllocationTable fat, uint firstCluster, uint length)
        {
            _reader = reader;
            _fat = fat;
            _length = length;

            _knownClusters = new List<uint>();
            _knownClusters.Add(firstCluster);
            _position = 0;

            _currentCluster = uint.MaxValue;
            _clusterBuffer = new byte[_reader.ClusterSize];
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
            get { throw new NotImplementedException(); }
        }

        public override void Flush()
        {
            return;
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
                if (value >= 0)
                {
                    _position = value;
                }
                else
                {
                    throw new ArgumentOutOfRangeException("value", "Attempt to move before beginning of stream");
                }
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position > _length)
            {
                throw new IOException("Attempt to read beyond end of file");
            }

            if (!TryLoadCurrentCluster())
            {
                throw new IOException("Attempt to read beyond known clusters");
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count", "Attempt to read negative number of bytes");
            }

            int target = count;
            if (_length - _position < count)
            {
                target = (int)(_length - _position);
            }

            int numRead = 0;
            while (numRead < target)
            {
                int clusterOffset = (int)(_position % _reader.ClusterSize);
                int toCopy = Math.Min(_reader.ClusterSize - clusterOffset, target - numRead);
                Array.Copy(_clusterBuffer, clusterOffset, buffer, offset + numRead, toCopy);

                // Remember how many we've read in total
                numRead += toCopy;

                // Increment the position
                _position += toCopy;

                // Abort if we've hit the end of the file
                if (!TryLoadCurrentCluster())
                {
                    break;
                }
            }

            return numRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPos = offset;
            if (origin == SeekOrigin.Current)
            {
                newPos += _position;
            }
            else if (origin == SeekOrigin.End)
            {
                newPos += Length;
            }
            _position = newPos;
            return newPos;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        private bool TryLoadCurrentCluster()
        {
            int clusterIdx = (int)(_position / _reader.ClusterSize);

            if (_knownClusters.Count <= clusterIdx)
            {
                if (!TryPopulateKnownClusters(clusterIdx))
                {
                    return false;
                }
            }

            // Chain is shorter than the current stream position
            if (_knownClusters.Count <= clusterIdx)
            {
                return false;
            }

            uint cluster = _knownClusters[clusterIdx];

            // This is the 'special' End-of-chain cluster identifer, so the stream position
            // is greater than the actual file length.
            if (_fat.IsEndOfChain(cluster))
            {
                return false;
            }

            // Already have this cluster loaded, we're good
            if (cluster == _currentCluster)
            {
                return true;
            }

            // Read the cluster, it's different
            _reader.ReadCluster(cluster, _clusterBuffer, 0);
            _currentCluster = cluster;
            return true;
        }

        private bool TryPopulateKnownClusters(int index)
        {
            uint lastKnown = _knownClusters[_knownClusters.Count - 1];
            while (!_fat.IsEndOfChain(lastKnown) && _knownClusters.Count <= index)
            {
                lastKnown = _fat.GetNext(lastKnown);
                _knownClusters.Add(lastKnown);
            }

            return _knownClusters.Count > index;
        }
    }
}
