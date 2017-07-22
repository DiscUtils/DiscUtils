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
using System.IO;
using DiscUtils.Internal;

namespace DiscUtils
{
    internal sealed class DiscFileLocator : FileLocator
    {
        private readonly string _basePath;
        private readonly DiscFileSystem _fileSystem;

        public DiscFileLocator(DiscFileSystem fileSystem, string basePath)
        {
            _fileSystem = fileSystem;
            _basePath = basePath;
        }

        public override bool Exists(string fileName)
        {
            return _fileSystem.FileExists(Utilities.CombinePaths(_basePath, fileName));
        }

        protected override Stream OpenFile(string fileName, FileMode mode, FileAccess access, FileShare share)
        {
            return _fileSystem.OpenFile(Utilities.CombinePaths(_basePath, fileName), mode, access);
        }

        public override FileLocator GetRelativeLocator(string path)
        {
            return new DiscFileLocator(_fileSystem, Utilities.CombinePaths(_basePath, path));
        }

        public override string GetFullPath(string path)
        {
            return Utilities.CombinePaths(_basePath, path);
        }

        public override string GetDirectoryFromPath(string path)
        {
            return Utilities.GetDirectoryFromPath(path);
        }

        public override string GetFileFromPath(string path)
        {
            return Utilities.GetFileFromPath(path);
        }

        public override DateTime GetLastWriteTimeUtc(string path)
        {
            return _fileSystem.GetLastWriteTimeUtc(Utilities.CombinePaths(_basePath, path));
        }

        public override bool HasCommonRoot(FileLocator other)
        {
            DiscFileLocator otherDiscLocator = other as DiscFileLocator;

            if (otherDiscLocator == null)
            {
                return false;
            }

            // Common root if the same file system instance.
            return ReferenceEquals(otherDiscLocator._fileSystem, _fileSystem);
        }

        public override string ResolveRelativePath(string path)
        {
            return Utilities.ResolveRelativePath(_basePath, path);
        }
    }
}