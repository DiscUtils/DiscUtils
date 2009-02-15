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
using System.Collections.ObjectModel;
using System.IO;

namespace DiscUtils.Vmdk
{
    /// <summary>
    /// Represents a VMDK-backed disk.
    /// </summary>
    public class Disk : VirtualDisk
    {
        private DiskImageFile _file;
        private SparseStream _content;

        /// <summary>
        /// Creates a new instance from a file on disk.
        /// </summary>
        /// <param name="path">The path to the disk</param>
        /// <param name="access">The access requested to the disk</param>
        public Disk(string path, FileAccess access)
        {
            _file = new DiskImageFile(path, access);
        }

        /// <summary>
        /// Disposes of this instance.
        /// </summary>
        /// <param name="disposing"><c>true</c> if disposing, <c>false</c> if in destructor</param>
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    if (_content != null)
                    {
                        _content.Dispose();
                        _content = null;
                    }

                    if (_file != null)
                    {
                        _file.Dispose();
                        _file = null;
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Gets the Geometry of this disk.
        /// </summary>
        public override Geometry Geometry
        {
            get { return _file.Geometry; }
        }

        /// <summary>
        /// Gets the capacity of this disk (in bytes).
        /// </summary>
        public override long Capacity
        {
            get { return _file.Capacity; }
        }

        /// <summary>
        /// Gets the contents of this disk as a stream.
        /// </summary>
        public override SparseStream Content
        {
            get
            {
                if (_content == null)
                {
                    _content = _file.OpenContent(null, false);
                }
                return _content;
            }
        }

        /// <summary>
        /// Gets the layers that make up the disk.
        /// </summary>
        public override ReadOnlyCollection<VirtualDiskLayer> Layers
        {
            get
            {
                return new ReadOnlyCollection<VirtualDiskLayer>(new VirtualDiskLayer[] { _file });
            }
        }

    }
}
