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
using System.IO;

namespace DiscUtils.Registry
{
    /// <summary>
    /// An internal structure within registry files, bins are the major unit of allocation in a registry hive.
    /// </summary>
    /// <remarks>Bins are divided into multiple cells, that contain actual registry data.</remarks>
    internal class Bin
    {
        internal const int Size = 4096;

        private Stream _fileStream;
        private long _streamPos;

        private BinHeader _header;
        private byte[] _buffer;

        public Bin(Stream stream)
        {
            _fileStream = stream;
            _streamPos = stream.Position;

            stream.Position = _streamPos;
            byte[] buffer = Utilities.ReadFully(stream, 0x20);
            _header = new BinHeader();
            _header.ReadFrom(buffer, 0);

            _fileStream.Position = _streamPos;
            _buffer = Utilities.ReadFully(_fileStream, _header.BinSize);
        }

        public Cell this[int index]
        {
            get
            {
                return Cell.Parse(_buffer, index + 4);
            }
        }

        public byte[] RawCellData(int index, int maxBytes)
        {
            int len = Utilities.ToInt32LittleEndian(_buffer, index);
            byte[] result = new byte[Math.Min((-len) - 4, maxBytes)];
            Array.Copy(_buffer, index + 4, result, 0, result.Length);
            return result;
        }
    }
}
