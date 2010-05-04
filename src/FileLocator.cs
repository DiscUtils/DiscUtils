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
using System.Text;
using System.IO;

namespace DiscUtils
{
    internal abstract class FileLocator
    {
        internal string MakeRelativePath(FileLocator fileLocator, string path)
        {
            if (!HasCommonRoot(fileLocator))
            {
                return null;
            }

            string ourFullPath = GetFullPath("") + @"\";
            string otherFullPath = fileLocator.GetFullPath(path);

            return Utilities.MakeRelativePath(otherFullPath, ourFullPath);
        }

        public abstract bool Exists(string fileName);

        public abstract Stream Open(string fileName, FileMode mode, FileAccess access, FileShare share);

        public abstract FileLocator GetRelativeLocator(string path);

        public abstract string GetFullPath(string path);

        public abstract DateTime GetLastWriteTimeUtc(string path);

        public abstract bool HasCommonRoot(FileLocator other);

        public abstract string ResolveRelativePath(string path);
    }

    internal sealed class LocalFileLocator : FileLocator
    {
        private string _dir;

        public LocalFileLocator(string dir)
        {
            _dir = dir;
        }

        public override bool Exists(string fileName)
        {
            return File.Exists(Path.Combine(_dir, fileName));
        }

        public override Stream Open(string fileName, FileMode mode, FileAccess access, FileShare share)
        {
            return new FileStream(Path.Combine(_dir, fileName), mode, access, share);
        }

        public override FileLocator GetRelativeLocator(string path)
        {
            return new LocalFileLocator(Path.Combine(_dir, path));
        }

        public override string GetFullPath(string path)
        {
            string combinedPath = Path.Combine(_dir, path);
            if (string.IsNullOrEmpty(combinedPath))
            {
                return Environment.CurrentDirectory;
            }
            else
            {
                return Path.GetFullPath(combinedPath);
            }
        }

        public override DateTime GetLastWriteTimeUtc(string path)
        {
            return File.GetLastWriteTimeUtc(Path.Combine(_dir, path));
        }

        public override bool HasCommonRoot(FileLocator other)
        {
            LocalFileLocator otherLocal = other as LocalFileLocator;
            if (otherLocal == null)
            {
                return false;
            }

            // If the paths have drive specifiers, then common root depends on them having a common
            // drive letter.
            string otherDir = otherLocal._dir;
            if (otherDir.Length >= 2 && _dir.Length >= 2)
            {
                if (otherDir[1] == ':' && _dir[1] == ':')
                {
                    return Char.ToUpperInvariant(otherDir[0]) == Char.ToUpperInvariant(_dir[0]);
                }
            }

            return true;
        }

        public override string ResolveRelativePath(string path)
        {
            return Utilities.ResolveRelativePath(_dir, path);
        }
    }

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
