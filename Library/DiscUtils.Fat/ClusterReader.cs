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
using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.Fat
{
    internal sealed class ClusterReader
    {
        private readonly int _bytesPerSector;

        /// <summary>
        /// Pre-calculated value because of number of uses of this externally.
        /// </summary>
        private readonly int _clusterSize;

        private readonly int _firstDataSector;
        private readonly int _sectorsPerCluster;
        private readonly Stream _stream;

        public ClusterReader(Stream stream, int firstDataSector, int sectorsPerCluster, int bytesPerSector)
        {
            _stream = stream;
            _firstDataSector = firstDataSector;
            _sectorsPerCluster = sectorsPerCluster;
            _bytesPerSector = bytesPerSector;

            _clusterSize = _sectorsPerCluster * _bytesPerSector;
        }

        public int ClusterSize
        {
            get { return _clusterSize; }
        }

        public void ReadCluster(uint cluster, byte[] buffer, int offset)
        {
            if (offset + ClusterSize > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset),
                    "buffer is too small - cluster would overflow buffer");
            }

            uint firstSector = (uint)((cluster - 2) * _sectorsPerCluster + _firstDataSector);

            _stream.Position = firstSector * _bytesPerSector;
            StreamUtilities.ReadExact(_stream, buffer, offset, _clusterSize);
        }

        internal void WriteCluster(uint cluster, byte[] buffer, int offset)
        {
            if (offset + ClusterSize > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset),
                    "buffer is too small - cluster would overflow buffer");
            }

            uint firstSector = (uint)((cluster - 2) * _sectorsPerCluster + _firstDataSector);

            _stream.Position = firstSector * _bytesPerSector;

            _stream.Write(buffer, offset, _clusterSize);
        }

        internal void WipeCluster(uint cluster)
        {
            uint firstSector = (uint)((cluster - 2) * _sectorsPerCluster + _firstDataSector);

            _stream.Position = firstSector * _bytesPerSector;

            _stream.Write(new byte[_clusterSize], 0, _clusterSize);
        }
    }
}