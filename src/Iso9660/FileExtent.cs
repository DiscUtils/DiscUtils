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
using System.Text;

namespace DiscUtils.Iso9660
{
    internal class FileExtent : DiskRegion
    {
        private BuildFileInfo _fileInfo;

        private Stream _readStream;

        public FileExtent(BuildFileInfo fileInfo, long start)
            : base(start)
        {
            _fileInfo = fileInfo;
            DiskLength = ((fileInfo.GetDataSize(Encoding.ASCII) + 2047) / 2048) * 2048;
        }

        internal override void PrepareForRead()
        {
            _readStream = _fileInfo.OpenStream();
        }

        internal override void ReadLogicalBlock(long diskOffset, byte[] block, int offset)
        {
            long relPos = diskOffset - DiskStart;
            int totalRead = 0;

            // Don't arbitrarily set position, just in case stream implementation is
            // non-seeking, and we're doing sequential reads
            if (_readStream.Position != relPos)
            {
                _readStream.Position = relPos;
            }

            // Read up to 2048 bytes (or EOF)
            int numRead = _readStream.Read(block, offset, 2048);
            while (numRead > 0)
            {
                totalRead += numRead;
                numRead = _readStream.Read(block, offset + totalRead, 2048 - totalRead);
            }

            // Wipe any that couldn't be read (beyond end of file)
            if (numRead < 2048)
            {
                Array.Clear(block, offset + totalRead, 2048 - totalRead);
            }
        }

        internal override void DisposeReadState()
        {
            _fileInfo.CloseStream(_readStream);
            _readStream = null;
        }
    }

}
