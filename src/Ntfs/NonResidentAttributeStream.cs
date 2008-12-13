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
using System.IO;

namespace DiscUtils.Ntfs
{
    internal class NonResidentAttributeStream : Stream
    {
        private Stream _fsStream;
        private long _bytesPerCluster;
        private DataRun[] _runs;

        private FileAccess _access;
        private long _length;
        private long _position;

        private bool _atEOF;

        public NonResidentAttributeStream(Stream fsStream, long bytesPerCluster, FileAccess access, NonResidentFileAttributeRecord record)
        {
            if (record.Flags != FileAttributeFlags.None)
            {
                throw new NotImplementedException("No support for sparse, compressed, or encrypted attributes");
            }

            _fsStream = fsStream;
            _bytesPerCluster = bytesPerCluster;
            _runs = record.DataRuns;
            _access = access;
            _length = record.DataLength;
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
            get { return _access == FileAccess.Write || _access == FileAccess.ReadWrite; }
        }

        public override void Flush()
        {
            ;
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
                _position = value;
                _atEOF = false;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!CanRead)
            {
                throw new IOException("Attempt to read from file not opened for read");
            }

            if (_atEOF)
            {
                throw new IOException("Attempt to read beyond end of file");
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count", "Attempt to read negative number of bytes");
            }

            if (_position >= _length)
            {
                _atEOF = true;
                return 0;
            }

            ulong vcn = (ulong)(_position / _bytesPerCluster);
            ulong clusterOffset = (ulong)(_position % _bytesPerCluster);

            ulong maxValid;
            ulong lcn = FindVirtualCluster(vcn, out maxValid);

            int toRead = (int)Math.Min((ulong)(maxValid * (ulong)_bytesPerCluster), (ulong)count);

            _fsStream.Position = (long)(lcn * (ulong)_bytesPerCluster + clusterOffset);
            int numRead = _fsStream.Read(buffer, offset, toRead);

            _position += numRead;

            return numRead;
        }

        private ulong FindVirtualCluster(ulong targetVcn, out ulong maxValidClusters)
        {
            ulong vc = 0;
            long lcnOffset = _runs[0].RunOffset;

            int i = 0;
            while (vc + _runs[i].RunLength <= targetVcn)
            {
                vc += _runs[i].RunLength;
                ++i;
                lcnOffset += _runs[i].RunOffset;

                if (lcnOffset < 0)
                {
                    throw new IOException("Invalid data run, negative net offset");
                }
            }

            ulong runOffset = targetVcn - vc;

            maxValidClusters = _runs[i].RunLength - runOffset;
            return (ulong)lcnOffset + runOffset;
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
            _atEOF = false;
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
    }
}
