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

namespace DiscUtils
{
    internal abstract class FileLocator
    {
        public abstract Stream Open(string fileName, FileMode mode, FileAccess access, FileShare share);
    }

    internal sealed class LocalFileLocator : FileLocator
    {
        private string _dir;

        public LocalFileLocator(string dir)
        {
            _dir = dir;
        }

        public override Stream Open(string fileName, FileMode mode, FileAccess access, FileShare share)
        {
            return new FileStream(Path.Combine(_dir, fileName), mode, access, share);
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

        public override Stream Open(string fileName, FileMode mode, FileAccess access, FileShare share)
        {
            return _fileSystem.OpenFile(Path.Combine(_basePath, fileName), mode, access);
        }
    }

}
