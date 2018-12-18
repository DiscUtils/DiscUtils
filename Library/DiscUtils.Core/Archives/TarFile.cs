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

namespace DiscUtils.Archives
{
    /// <summary>
    /// Minimal tar file format implementation.
    /// </summary>
    public sealed class TarFile
    {
        private readonly Dictionary<string, FileRecord> _files;
        private readonly Stream _fileStream;

        /// <summary>
        /// Initializes a new instance of the TarFile class.
        /// </summary>
        /// <param name="fileStream">The Tar file.</param>
        public TarFile(Stream fileStream)
        {
            _fileStream = fileStream;
            _files = new Dictionary<string, FileRecord>();

            TarHeader hdr = new TarHeader();
            byte[] hdrBuf = StreamUtilities.ReadExact(_fileStream, TarHeader.Length);
            hdr.ReadFrom(hdrBuf, 0);
            while (hdr.FileLength != 0 || !string.IsNullOrEmpty(hdr.FileName))
            {
                FileRecord record = new FileRecord(hdr.FileName, _fileStream.Position, hdr.FileLength);
                _files.Add(record.Name, record);
                _fileStream.Position += (hdr.FileLength + 511) / 512 * 512;

                hdrBuf = StreamUtilities.ReadExact(_fileStream, TarHeader.Length);
                hdr.ReadFrom(hdrBuf, 0);
            }
        }

        /// <summary>
        /// Tries to open a file contained in the archive, if it exists.
        /// </summary>
        /// <param name="path">The path to the file within the archive.</param>
        /// <param name="stream">A stream containing the file contents, or null.</param>
        /// <returns><c>true</c> if the file could be opened, else <c>false</c>.</returns>
        public bool TryOpenFile(string path, out Stream stream)
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

        /// <summary>
        /// Open a file contained in the archive.
        /// </summary>
        /// <param name="path">The path to the file within the archive.</param>
        /// <returns>A stream containing the file contents.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the file is not found.</exception>
        public Stream OpenFile(string path)
        {
            if (_files.ContainsKey(path))
            {
                FileRecord file = _files[path];
                return new SubStream(_fileStream, file.Start, file.Length);
            }

            throw new FileNotFoundException("File is not in archive", path);
        }

        /// <summary>
        /// Determines if a given file exists in the archive.
        /// </summary>
        /// <param name="path">The file path to test.</param>
        /// <returns><c>true</c> if the file is present, else <c>false</c>.</returns>
        public bool FileExists(string path)
        {
            return _files.ContainsKey(path);
        }

        /// <summary>
        /// Determines if a given directory exists in the archive.
        /// </summary>
        /// <param name="path">The file path to test.</param>
        /// <returns><c>true</c> if the directory is present, else <c>false</c>.</returns>
        public bool DirExists(string path)
        {
            string searchStr = path;
            searchStr = searchStr.Replace(@"\", "/");
            searchStr = searchStr.EndsWith(@"/", StringComparison.Ordinal) ? searchStr : searchStr + @"/";

            foreach (string filePath in _files.Keys)
            {
                if (filePath.StartsWith(searchStr, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        internal IEnumerable<FileRecord> GetFiles(string dir)
        {
            string searchStr = dir;
            searchStr = searchStr.Replace(@"\", "/");
            searchStr = searchStr.EndsWith(@"/", StringComparison.Ordinal) ? searchStr : searchStr + @"/";

            foreach (string filePath in _files.Keys)
            {
                if (filePath.StartsWith(searchStr, StringComparison.Ordinal))
                {
                    yield return _files[filePath];
                }
            }
        }
    }
}