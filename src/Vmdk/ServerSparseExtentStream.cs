//
// Copyright (c) 2008-2009, Kenneth Bell
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
using System.Linq;
using System.Text;
using System.IO;

namespace DiscUtils.Vmdk
{
    internal class ServerSparseExtentStream : CommonSparseExtentStream
    {
        public ServerSparseExtentStream(Stream file, bool ownsFile, long diskOffset, SparseStream parentDiskStream, bool ownsParentDiskStream)
        {
            _fileStream = file;
            _ownsFileStream = ownsFile;
            _diskOffset = diskOffset;
            _parentDiskStream = parentDiskStream;
            _ownsParentDiskStream = ownsParentDiskStream;

            file.Position = 0;
            byte[] firstSector = Utilities.ReadFully(file, Sizes.Sector * 4);
            _header = ServerSparseExtentHeader.Read(firstSector, 0);

            _gtCoverage = _header.NumGTEsPerGT * _header.GrainSize * Sizes.Sector;

            LoadGlobalDirectory();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

    }
}
