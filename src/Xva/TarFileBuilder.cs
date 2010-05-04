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

using System.Collections.Generic;
using System.IO;

namespace DiscUtils.Xva
{
    internal class TarFileBuilder : StreamBuilder
    {
        private List<BuildFileRecord> _files;

        public TarFileBuilder()
        {
            _files = new List<BuildFileRecord>();
        }

        public void AddFile(string name, byte[] buffer)
        {
            _files.Add(new BuildFileRecord(name, buffer));
        }

        public void AddFile(string name, Stream stream)
        {
            _files.Add(new BuildFileRecord(name, stream));
        }

        internal override List<BuilderExtent> FixExtents(out long totalLength)
        {
            List<BuilderExtent> result = new List<BuilderExtent>(_files.Count * 2 + 2);
            long pos = 0;

            foreach (BuildFileRecord file in _files)
            {
                BuilderExtent fileContentExtent = file.Fix(pos + TarHeader.Length);

                result.Add(new TarHeaderExtent(pos, file.Name, fileContentExtent.Length));
                pos += TarHeader.Length;

                result.Add(fileContentExtent);
                pos += Utilities.RoundUp(fileContentExtent.Length, 512);
            }

            // Two empty 512-byte blocks at end of tar file.
            result.Add(new BuilderBufferExtent(pos, new byte[1024]));

            totalLength = pos + 1024;
            return result;
        }
    }
}
