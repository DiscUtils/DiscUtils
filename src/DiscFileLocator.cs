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

namespace DiscUtils
{
    using System;
    using System.IO;

    internal sealed class DiscFileLocator : FileLocator
    {
        private DiscFileSystem _fileSystem;
        private string _basePath;

        public DiscFileLocator(DiscFileSystem fileSystem, string basePath)
        {
            _fileSystem = fileSystem;
            _basePath = basePath;
        }

        public override bool Exists(string fileName)
        {
            return _fileSystem.FileExists(Path.Combine(_basePath, fileName));
        }

        public override Stream Open(string fileName, FileMode mode, FileAccess access, FileShare share)
        {
            return _fileSystem.OpenFile(Path.Combine(_basePath, fileName), mode, access);
        }

        public override FileLocator GetRelativeLocator(string path)
        {
            return new DiscFileLocator(_fileSystem, Path.Combine(_basePath, path));
        }

        public override string GetFullPath(string path)
        {
            return Path.Combine(_basePath, path);
        }

        public override DateTime GetLastWriteTimeUtc(string path)
        {
            return _fileSystem.GetLastWriteTimeUtc(Path.Combine(_basePath, path));
        }

        public override bool HasCommonRoot(FileLocator other)
        {
            DiscFileLocator otherDiscLocator = other as DiscFileLocator;

            if (otherDiscLocator == null)
            {
                return false;
            }

            // Common root if the same file system instance.
            return Object.ReferenceEquals(otherDiscLocator._fileSystem, _fileSystem);
        }

        public override string ResolveRelativePath(string path)
        {
            return Utilities.ResolveRelativePath(_basePath, path);
        }
    }
}
