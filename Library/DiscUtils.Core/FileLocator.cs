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
using DiscUtils.Setup;

namespace DiscUtils
{
    internal abstract class FileLocator
    {
        public abstract bool Exists(string fileName);

        public Stream Open(string fileName, FileMode mode, FileAccess access, FileShare share)
        {
            var args = new FileOpenEventArgs(fileName, mode, access, share, OpenFile);
            SetupHelper.OnOpeningFile(this, args);
            if (args.Result != null)
                return args.Result;
            return OpenFile(args.FileName, args.FileMode, args.FileAccess, args.FileShare);
        }

        protected abstract Stream OpenFile(string fileName, FileMode mode, FileAccess access, FileShare share);

        public abstract FileLocator GetRelativeLocator(string path);

        public abstract string GetFullPath(string path);

        public abstract string GetDirectoryFromPath(string path);

        public abstract string GetFileFromPath(string path);

        public abstract DateTime GetLastWriteTimeUtc(string path);

        public abstract bool HasCommonRoot(FileLocator other);

        public abstract string ResolveRelativePath(string path);

        internal string MakeRelativePath(FileLocator fileLocator, string path)
        {
            if (!HasCommonRoot(fileLocator))
            {
                return null;
            }

            string ourFullPath = GetFullPath(string.Empty) + @"\";
            string otherFullPath = fileLocator.GetFullPath(path);

            return Utilities.MakeRelativePath(otherFullPath, ourFullPath);
        }
    }
}