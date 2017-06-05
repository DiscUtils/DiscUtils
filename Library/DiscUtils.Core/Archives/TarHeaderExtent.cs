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
using DiscUtils.Streams;

namespace DiscUtils.Archives
{
    internal sealed class TarHeaderExtent : BuilderBufferExtent
    {
        private readonly long _fileLength;
        private readonly string _fileName;
        private readonly int _groupId;
        private readonly UnixFilePermissions _mode;
        private readonly DateTime _modificationTime;
        private readonly int _ownerId;

        public TarHeaderExtent(long start, string fileName, long fileLength, UnixFilePermissions mode, int ownerId,
                               int groupId, DateTime modificationTime)
            : base(start, 512)
        {
            _fileName = fileName;
            _fileLength = fileLength;
            _mode = mode;
            _ownerId = ownerId;
            _groupId = groupId;
            _modificationTime = modificationTime;
        }

        public TarHeaderExtent(long start, string fileName, long fileLength)
            : this(start, fileName, fileLength, 0, 0, 0, DateTimeOffsetExtensions.UnixEpoch) {}

        protected override byte[] GetBuffer()
        {
            byte[] buffer = new byte[TarHeader.Length];

            TarHeader header = new TarHeader();
            header.FileName = _fileName;
            header.FileLength = _fileLength;
            header.FileMode = _mode;
            header.OwnerId = _ownerId;
            header.GroupId = _groupId;
            header.ModificationTime = _modificationTime;
            header.WriteTo(buffer, 0);

            return buffer;
        }
    }
}