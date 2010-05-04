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
using System.Collections.Generic;
using System.IO;

namespace DiscUtils.Xva
{
    /// <summary>
    /// Minimal tar file format implementation needed for XVA file support.
    /// </summary>
    /// <remarks>This is not a complete implementation of the tar file format, it
    /// is just functional enough to make valid XVA files.</remarks>
    internal class TarFile
    {
        private Stream _fileStream;
        private Dictionary<string, FileRecord> _files;

        public TarFile(Stream fileStream)
        {
            _fileStream = fileStream;
            _files = new Dictionary<string, FileRecord>();

            TarHeader hdr = new TarHeader();
            byte[] hdrBuf = Utilities.ReadFully(_fileStream, TarHeader.Length);
            hdr.ReadFrom(hdrBuf, 0);
            while (hdr.FileLength != 0 || !string.IsNullOrEmpty(hdr.FileName))
            {
                FileRecord record = new FileRecord(hdr.FileName, _fileStream.Position, hdr.FileLength);
                _files.Add(record.Name, record);
                _fileStream.Position += ((hdr.FileLength + 511) / 512) * 512;

                hdrBuf = Utilities.ReadFully(_fileStream, TarHeader.Length);
                hdr.ReadFrom(hdrBuf, 0);
            }
        }

        internal bool TryOpenFile(string path, out Stream stream)
        {
            if (_files.ContainsKey(path))
            {
                FileRecord file = _files[path];
                stream = new SubStream(_fileStream, file.Start, file.Length);
                return true;
            }
            stream = null;
            return false;
        }

        internal Stream OpenFile(string path)
        {
            if (_files.ContainsKey(path))
            {
                FileRecord file = _files[path];
                return new SubStream(_fileStream, file.Start, file.Length);
            }
            throw new FileNotFoundException("File is not in archive", path);
        }

        internal bool FileExists(string path)
        {
            return _files.ContainsKey(path);
        }

        internal bool DirExists(string path)
        {
            string searchStr = path;
            searchStr = searchStr.Replace(@"\", "/");
            searchStr = (searchStr.EndsWith(@"/", StringComparison.Ordinal) ? searchStr : searchStr + @"/");

            foreach (string filePath in _files.Keys)
            {
                if (filePath.StartsWith(searchStr, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
