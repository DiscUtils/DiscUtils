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
using System.Globalization;
using System.IO;
using DiscUtils.Internal;

namespace DiscUtils.Nfs
{
    [VirtualDiskTransport("nfs")]
    internal sealed class DiskTransport : VirtualDiskTransport
    {
        private string _extraInfo;
        private NfsFileSystem _fileSystem;
        private string _path;

        public override bool IsRawDisk
        {
            get { return false; }
        }

        public override void Connect(Uri uri, string username, string password)
        {
            string fsPath = uri.AbsolutePath;

            // Find the best (least specific) export
            string bestRoot = null;
            int bestMatchLength = int.MaxValue;
            foreach (string export in NfsFileSystem.GetExports(uri.Host))
            {
                if (fsPath.Length >= export.Length)
                {
                    int matchLength = export.Length;
                    for (int i = 0; i < export.Length; ++i)
                    {
                        if (export[i] != fsPath[i])
                        {
                            matchLength = i;
                            break;
                        }
                    }

                    if (matchLength < bestMatchLength)
                    {
                        bestRoot = export;
                        bestMatchLength = matchLength;
                    }
                }
            }

            if (bestRoot == null)
            {
                throw new IOException(string.Format(CultureInfo.InvariantCulture,
                    "Unable to find an NFS export providing access to '{0}'", fsPath));
            }

            _fileSystem = new NfsFileSystem(uri.Host, bestRoot);
            _path = fsPath.Substring(bestRoot.Length).Replace('/', '\\');
            _extraInfo = uri.Fragment.TrimStart('#');
        }

        public override VirtualDisk OpenDisk(FileAccess access)
        {
            throw new NotSupportedException();
        }

        public override FileLocator GetFileLocator()
        {
            return new DiscFileLocator(_fileSystem, Utilities.GetDirectoryFromPath(_path));
        }

        public override string GetFileName()
        {
            return Utilities.GetFileFromPath(_path);
        }

        public override string GetExtraInfo()
        {
            return _extraInfo;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_fileSystem != null)
                {
                    _fileSystem.Dispose();
                    _fileSystem = null;
                }
            }

            base.Dispose(disposing);
        }
    }
}