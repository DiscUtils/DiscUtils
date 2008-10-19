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
    internal class ClusterStream : Stream
    {
        private FileAccess _access;
        private ClusterReader _reader;
        private FileAllocationTable _fat;
        private uint _length;

        private List<uint> _knownClusters;
        private long _position;

        private uint _currentCluster = 0;
        private byte[] _clusterBuffer;

        internal ClusterStream(FileAccess access, ClusterReader reader, FileAllocationTable fat, uint firstCluster, uint length)
        {
            _access = access;
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
            get { return _access == FileAccess.Read || _access == FileAccess.ReadWrite; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return _access == FileAccess.ReadWrite || _access == FileAccess.Write; }
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
            if (!CanRead)
            {
                throw new IOException("Attempt to read from file not opened for read");
            }

            if (_position > _length)
            {
                throw new IOException("Attempt to read beyond end of file");
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

            if (!TryLoadCurrentCluster())
            {
                throw new IOException("Attempt to read beyond known clusters");
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
            int bytesRemaining = count;

            if (!CanWrite)
            {
                throw new IOException("Attempting to write to file not opened for writing");
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count", count, "Attempting to write negative number of bytes");
            }

            if (offset > buffer.Length || (offset + count) > buffer.Length)
            {
                throw new IndexOutOfRangeException("Attempt to write bytes outside of the buffer");
            }

            // TODO: Free space check...

            try
            {
                while (bytesRemaining > 0)
                {
                    // Extend the stream until it encompasses _position
                    uint cluster;
                    while (!TryGetClusterByPosition(_position, out cluster))
                    {
                        cluster = ExtendChain();
                        _reader.WipeCluster(cluster);
                    }

                    // Fill this cluster with as much data as we can (WriteToCluster preserves existing cluster
                    // data, if necessary)
                    int numWritten = WriteToCluster(cluster, (int)(_position % _reader.ClusterSize), buffer, offset, bytesRemaining);
                    offset += numWritten;
                    bytesRemaining -= numWritten;
                    _position += numWritten;
                }

                _length = (uint)Math.Max(_length, _position);
            }
            finally
            {
                _fat.Flush();
            }
        }

        /// <summary>
        /// Writes up to the next cluster boundary, making sure to preserve existing data in the cluster
        /// that falls outside of the updated range.
        /// </summary>
        /// <param name="cluster">The cluster to write to.</param>
        /// <param name="pos">The file position of the write (within the cluster)</param>
        /// <param name="buffer">The buffer with the new data</param>
        /// <param name="offset">Offset into buffer of the first byte to write</param>
        /// <param name="count">The maximum number of bytes to write</param>
        /// <returns>The number of bytes written - either count, or the number that fit up to
        /// the cluster boundary.</returns>
        private int WriteToCluster(uint cluster, int pos, byte[] buffer, int offset, int count)
        {
            if (pos == 0 && count >= _reader.ClusterSize)
            {
                _currentCluster = cluster;
                Array.Copy(buffer, offset, _clusterBuffer, 0, _reader.ClusterSize);

                WriteCurrentCluster();

                return _reader.ClusterSize;
            }
            else
            {
                // Partial cluster, so need to read existing cluster data first
                LoadCluster(cluster);

                int copyLength = Math.Min(count, (int)(_reader.ClusterSize - (pos % _reader.ClusterSize)));
                Array.Copy(buffer, offset, _clusterBuffer, pos, copyLength);

                WriteCurrentCluster();

                return copyLength;
            }
        }

        /// <summary>
        /// Adds a new cluster to the end of the existing chain, by allocating a free cluster.
        /// </summary>
        /// <returns>The cluster allocated</returns>
        /// <remarks>This method does not initialize the data in the cluster, the caller should
        /// perform a write to ensure the cluster data is in known state.</remarks>
        private uint ExtendChain()
        {
            // Sanity check - make sure the final known cluster is the EOC marker
            if (!_fat.IsEndOfChain(_knownClusters[_knownClusters.Count - 1]))
            {
                throw new IOException("Corrupt file system: final cluster isn't End-of-Chain");
            }

            uint cluster;
            if (!_fat.TryGetFreeCluster(out cluster))
            {
                throw new IOException("Out of disk space");
            }

            _fat.SetEndOfChain(cluster);
            _fat.SetNext(_knownClusters[_knownClusters.Count - 2], cluster);
            _knownClusters[_knownClusters.Count - 1] = cluster;
            _knownClusters.Add(_fat.GetNext(cluster));

            return cluster;
        }

        private bool TryLoadCurrentCluster()
        {
            return TryLoadClusterByPosition(_position);
        }

        private bool TryLoadClusterByPosition(long pos)
        {
            uint cluster;
            if (!TryGetClusterByPosition(pos, out cluster))
            {
                return false;
            }

            // Read the cluster, it's different to the one currently loaded
            if (cluster != _currentCluster)
            {
                _reader.ReadCluster(cluster, _clusterBuffer, 0);
                _currentCluster = cluster;
            }

            return true;
        }

        private void LoadCluster(uint cluster)
        {
            // Read the cluster, it's different to the one currently loaded
            if (cluster != _currentCluster)
            {
                _reader.ReadCluster(cluster, _clusterBuffer, 0);
                _currentCluster = cluster;
            }
        }

        private void LoadClusterByPosition(long pos)
        {
            if (!TryLoadClusterByPosition(pos))
            {
                throw new IOException(string.Format("Failed to read existing cluster at 0x{0:X}", pos));
            }
        }

        private void WriteCurrentCluster()
        {
            _reader.WriteCluster(_currentCluster, _clusterBuffer, 0);
        }

        private bool TryGetClusterByPosition(long pos, out uint cluster)
        {
            int index = (int)(pos / _reader.ClusterSize);

            if (_knownClusters.Count <= index)
            {
                if (!TryPopulateKnownClusters(index))
                {
                    cluster = uint.MaxValue;
                    return false;
                }
            }

            // Chain is shorter than the current stream position
            if (_knownClusters.Count <= index)
            {
                cluster = uint.MaxValue;
                return false;
            }

            cluster = _knownClusters[(int)index];

            // This is the 'special' End-of-chain cluster identifer, so the stream position
            // is greater than the actual file length.
            if (_fat.IsEndOfChain(cluster))
            {
                return false;
            }

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
