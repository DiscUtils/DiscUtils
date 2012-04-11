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

namespace DiscUtils.HfsPlus
{
    using System;
    using System.IO;
    using DiscUtils.Vfs;

    internal class File : IVfsFileWithStreams
    {
        private Context _context;
        private CatalogNodeId _nodeId;
        private CommonCatalogFileInfo _catalogInfo;

        public File(Context context, CatalogNodeId nodeId, CommonCatalogFileInfo catalogInfo)
        {
            _context = context;
            _nodeId = nodeId;
            _catalogInfo = catalogInfo;
        }

        public DateTime LastAccessTimeUtc
        {
            get
            {
                return _catalogInfo.AccessTime;
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public DateTime LastWriteTimeUtc
        {
            get
            {
                return _catalogInfo.ContentModifyTime;
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public DateTime CreationTimeUtc
        {
            get
            {
                return _catalogInfo.CreateTime;
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public FileAttributes FileAttributes
        {
            get
            {
                return Utilities.FileAttributesFromUnixFileType(_catalogInfo.FileSystemInfo.FileType);
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public long FileLength
        {
            get
            {
                CatalogFileInfo fileInfo = _catalogInfo as CatalogFileInfo;
                if (fileInfo == null)
                {
                    throw new InvalidOperationException();
                }

                return (long)fileInfo.DataFork.LogicalSize;
            }
        }

        public IBuffer FileContent
        {
            get
            {
                CatalogFileInfo fileInfo = _catalogInfo as CatalogFileInfo;
                if (fileInfo == null)
                {
                    throw new InvalidOperationException();
                }

                return new FileBuffer(_context, fileInfo.DataFork, fileInfo.FileId);
            }
        }

        protected Context Context
        {
            get { return _context; }
        }

        protected CatalogNodeId NodeId
        {
            get { return _nodeId; }
        }

        public SparseStream CreateStream(string name)
        {
            throw new NotSupportedException();
        }

        public SparseStream OpenExistingStream(string name)
        {
            throw new NotImplementedException();
        }
    }
}
