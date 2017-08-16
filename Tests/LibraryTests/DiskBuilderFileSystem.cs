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
using DiscUtils;
using DiscUtils.Streams;

namespace LibraryTests
{
    /// <summary>
    /// Minimal implementation of DiscFileSystem, sufficient to support unit-testing of disk formats.
    /// </summary>
    class DiskBuilderFileSystem : DiscFileSystem
    {
        private IDictionary<string, DiskImageFileSpecification> _files;

        public DiskBuilderFileSystem(IEnumerable<DiskImageFileSpecification> fileSpecs)
        {
            _files = new Dictionary<string, DiskImageFileSpecification>();

            foreach (var fileSpec in fileSpecs)
            {
                _files.Add(fileSpec.Name, fileSpec);
            }
        }

        public override string FriendlyName
        {
            get { throw new NotImplementedException(); }
        }

        public override bool CanWrite
        {
            get { throw new NotImplementedException(); }
        }

        public override long Size => throw new NotImplementedException();

        public override long UsedSpace => throw new NotImplementedException();

        public override long AvailableSpace => throw new NotImplementedException();

        public override void CopyFile(string sourceFile, string destinationFile, bool overwrite)
        {
            throw new NotImplementedException();
        }

        public override void CreateDirectory(string path)
        {
            throw new NotImplementedException();
        }

        public override void DeleteDirectory(string path)
        {
            throw new NotImplementedException();
        }

        public override void DeleteFile(string path)
        {
            throw new NotImplementedException();
        }

        public override bool DirectoryExists(string path)
        {
            throw new NotImplementedException();
        }

        public override bool FileExists(string path)
        {
            throw new NotImplementedException();
        }

        public override string[] GetDirectories(string path, string searchPattern, SearchOption searchOption)
        {
            throw new NotImplementedException();
        }

        public override string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
        {
            throw new NotImplementedException();
        }

        public override string[] GetFileSystemEntries(string path)
        {
            throw new NotImplementedException();
        }

        public override string[] GetFileSystemEntries(string path, string searchPattern)
        {
            throw new NotImplementedException();
        }

        public override void MoveDirectory(string sourceDirectoryName, string destinationDirectoryName)
        {
            throw new NotImplementedException();
        }

        public override void MoveFile(string sourceName, string destinationName, bool overwrite)
        {
            throw new NotImplementedException();
        }

        public override SparseStream OpenFile(string path, FileMode mode, FileAccess access)
        {
            if (_files.ContainsKey(path))
            {
                if(mode == FileMode.CreateNew)
                {
                    throw new IOException("File already exists");
                }
                return _files[path].OpenStream();
            }
            else
            {
                throw new FileNotFoundException();
            }
        }

        public override FileAttributes GetAttributes(string path)
        {
            throw new NotImplementedException();
        }

        public override void SetAttributes(string path, FileAttributes newValue)
        {
            throw new NotImplementedException();
        }

        public override DateTime GetCreationTimeUtc(string path)
        {
            throw new NotImplementedException();
        }

        public override void SetCreationTimeUtc(string path, DateTime newTime)
        {
            throw new NotImplementedException();
        }

        public override DateTime GetLastAccessTimeUtc(string path)
        {
            throw new NotImplementedException();
        }

        public override void SetLastAccessTimeUtc(string path, DateTime newTime)
        {
            throw new NotImplementedException();
        }

        public override DateTime GetLastWriteTimeUtc(string path)
        {
            throw new NotImplementedException();
        }

        public override void SetLastWriteTimeUtc(string path, DateTime newTime)
        {
            throw new NotImplementedException();
        }

        public override long GetFileLength(string path)
        {
            if (_files.ContainsKey(path))
            {
                using (Stream s = _files[path].OpenStream())
                {
                    return s.Length;
                }
            }
            else
            {
                throw new FileNotFoundException("No such file", path);
            }
        }
    }
}
