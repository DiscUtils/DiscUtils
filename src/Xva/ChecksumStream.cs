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
using System.IO;
using System.Security.Cryptography;

namespace DiscUtils.Xva
{
    internal class ChecksumStream : Stream
    {
        private HashAlgorithm _hashGenerator;

        private long _position;
        private byte[] _data;

        public ChecksumStream(HashAlgorithm hashGenerator)
        {
            _hashGenerator = hashGenerator;
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override long Length
        {
            get
            {
                // We output as ascii hex
                return (_hashGenerator.HashSize / 8) * 2;
            }
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
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            // We commit the hash on the first read
            if (_data == null)
            {
                _hashGenerator.TransformFinalBlock(new byte[0], 0, 0);
                byte[] hash = _hashGenerator.Hash;
                byte[] result = new byte[Length];
                for (int i = 0; i < hash.Length; ++i)
                {
                    result[i * 2] = (byte)"0123456789abcdef"[(hash[i] >> 4) & 0x0F];
                    result[i * 2 + 1] = (byte)"0123456789abcdef"[hash[i] & 0x0F];
                }
                _data = result;
            }

            int numToRead = (int)Math.Min(count, Length - _position);
            Array.Copy(_data, _position, buffer, offset, numToRead);
            return numToRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
