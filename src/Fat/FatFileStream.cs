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

namespace DiscUtils.Fat
{
    internal class FatFileStream : Stream
    {
        private Directory _dir;
        private long _dirId;
        private ClusterStream _stream;

        private bool didWrite = false;

        public FatFileStream(FatFileSystem fileSystem, Directory dir, long fileId, FileAccess access)
        {
            _dir = dir;
            _dirId = fileId;

            DirectoryEntry dirEntry = _dir.GetEntry(_dirId);
            _stream = new ClusterStream(fileSystem, access, (uint)dirEntry.FirstCluster, (uint)dirEntry.FileSize);
            _stream.FirstClusterChanged += FirstClusterAllocatedHandler;
        }

        public override void Close()
        {
            if (_dir.FileSystem.CanWrite)
            {
                try
                {
                    DateTime now = _dir.FileSystem.ConvertFromUtc(DateTime.UtcNow);

                    DirectoryEntry dirEntry = _dir.GetEntry(_dirId);
                    dirEntry.LastAccessTime = now;
                    if (didWrite)
                    {
                        dirEntry.FileSize = (int)_stream.Length;
                        dirEntry.LastWriteTime = now;
                    }
                    _dir.UpdateEntry(_dirId, dirEntry);
                }
                finally
                {
                    base.Close();
                }
            }
        }

        public override long Position
        {
            get { return _stream.Position; }
            set { _stream.Position = value; }
        }

        public override void SetLength(long value)
        {
            didWrite = true;
            _stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            didWrite = true;
            _stream.Write(buffer, offset, count);
        }

        public override bool CanRead
        {
            get { return _stream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return _stream.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return _stream.CanWrite; }
        }

        public override void Flush()
        {
            _stream.Flush();
        }

        public override long Length
        {
            get { return _stream.Length; }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _stream.Seek(offset, origin);
        }

        private void FirstClusterAllocatedHandler(uint cluster)
        {
            DirectoryEntry dirEntry = _dir.GetEntry(_dirId);
            dirEntry.FirstCluster = cluster;
            _dir.UpdateEntry(_dirId, dirEntry);
        }
    }
}
