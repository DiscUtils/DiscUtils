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
using System.Collections.Generic;
using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.Nfs
{
    internal sealed class Nfs3FileStream : SparseStream
    {
        private readonly FileAccess _access;
        private readonly Nfs3Client _client;
        private readonly Nfs3FileHandle _handle;

        private long _length;
        private long _position;

        public Nfs3FileStream(Nfs3Client client, Nfs3FileHandle handle, FileAccess access)
        {
            _client = client;
            _handle = handle;
            _access = access;

            _length = _client.GetAttributes(_handle).Size;
        }

        public override bool CanRead
        {
            get { return _access != FileAccess.Write; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return _access != FileAccess.Read; }
        }

        public override IEnumerable<StreamExtent> Extents
        {
            get { return new[] { new StreamExtent(0, Length) }; }
        }

        public override long Length
        {
            get { return _length; }
        }

        public override long Position
        {
            get { return _position; }
            set { _position = value; }
        }

        public override void Flush() {}

        public override int Read(byte[] buffer, int offset, int count)
        {
            int numToRead = (int)Math.Min(_client.FileSystemInfo.ReadMaxBytes, count);
            Nfs3ReadResult readResult = _client.Read(_handle, _position, numToRead);

            int toCopy = Math.Min(count, readResult.Count);

            Array.Copy(readResult.Data, 0, buffer, offset, toCopy);

            if (readResult.Eof)
            {
                _length = _position + readResult.Count;
            }

            _position += toCopy;
            return toCopy;
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
            if (CanWrite)
            {
                _client.SetAttributes(_handle, new Nfs3SetAttributes { SetSize = true, Size = value });
                _length = value;
            }
            else
            {
                throw new InvalidOperationException("Attempt to change length of read-only file");
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            int totalWritten = 0;

            while (totalWritten < count)
            {
                int numToWrite = (int)Math.Min(_client.FileSystemInfo.WriteMaxBytes, (uint)(count - totalWritten));

                int numWritten = _client.Write(_handle, _position, buffer, offset + totalWritten, numToWrite);

                _position += numWritten;
                totalWritten += numWritten;
            }

            _length = Math.Max(_length, _position);
        }
    }
}