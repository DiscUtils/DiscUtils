//
// Copyright (c) 2008-2011, Kenneth Bell
// Adapted for EWF by Adam Bridge, 2013
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

namespace DiscUtils.Ewf
{
    /// <summary>
    /// Represents an EWF disk image.
    /// </summary>
    /// <remarks>This disk format is simply an uncompressed capture of all blocks on a disk</remarks>
    public sealed class Disk : VirtualDisk
    {
        /// <summary>
        /// The first segment file that makes up the disk.
        /// </summary>
        private Tuple<DiskImageFile, Ownership> _file;

        /// <summary>
        /// The stream representing the disk's contents.
        /// </summary>
        private SparseStream _content;

        /// <summary>
        /// Initializes a new instance of the Disk class.
        /// </summary>
        /// <param name="path">The path to the disk image</param>
        public Disk(string path)
        {
            DiskImageFile file = new DiskImageFile(path, FileAccess.Read);
            _file = new Tuple<DiskImageFile, Ownership>(file, Ownership.Dispose);
        }

        /// <summary>
        /// Initializes a new instance of the Disk class.
        /// </summary>
        /// <param name="file">The contents of the disk.</param>
        private Disk(DiskImageFile file)
        {
            _file = new Tuple<DiskImageFile, Ownership>(file, Ownership.Dispose);
        }

        /// <summary>
        /// Gets the geometry of the disk.
        /// </summary>
        public override Geometry Geometry
        {
            get { return _file.Item1.Geometry; }
        }

        /// <summary>
        /// Gets the type of disk represented by this object.
        /// </summary>
        public override VirtualDiskClass DiskClass
        {
            get { return _file.Item1.DiskType; }
        }

        /// <summary>
        /// Gets the capacity of the disk (in bytes).
        /// </summary>
        public override long Capacity
        {
            get { return _file.Item1.Capacity; }
        }

        /// <summary>
        /// Gets the content of the disk as a stream.
        /// </summary>
        /// <remarks>Note the returned stream is not guaranteed to be at any particular position.  The actual position
        /// will depend on the last partition table/file system activity, since all access to the disk contents pass
        /// through a single stream instance.  Set the stream position before accessing the stream.</remarks>
        public override SparseStream Content
        {
            get
            {
                if (_content == null)
                {
                    _content = _file.Item1.Content;
                }

                return _content;
            }
        }

        /// <summary>
        /// Gets the layers that make up the disk.
        /// </summary>
        public override IEnumerable<VirtualDiskLayer> Layers
        {
            get
            {
                yield return _file.Item1 as VirtualDiskLayer;
            }
        }

        /// <summary>
        /// Gets information about the type of disk.
        /// </summary>
        /// <remarks>This property provides access to meta-data about the disk format, for example whether the
        /// BIOS geometry is preserved in the disk file.</remarks>
        public override VirtualDiskTypeInfo DiskTypeInfo
        {
            get { return DiskFactory.MakeDiskTypeInfo(); }
        }

        /// <summary>
        /// Create a new differencing disk, possibly within an existing disk.
        /// </summary>
        /// <param name="fileSystem">The file system to create the disk on</param>
        /// <param name="path">The path (or URI) for the disk to create</param>
        /// <returns>The newly created disk</returns>
        public override VirtualDisk CreateDifferencingDisk(DiscFileSystem fileSystem, string path)
        {
            throw new NotSupportedException("Differencing disks not supported for EWF disks");
        }

        /// <summary>
        /// Create a new differencing disk.
        /// </summary>
        /// <param name="path">The path (or URI) for the disk to create</param>
        /// <returns>The newly created disk</returns>
        public override VirtualDisk CreateDifferencingDisk(string path)
        {
            throw new NotSupportedException("Differencing disks not supported for EWF disks");
        }

        /// <summary>
        /// Disposes of underlying resources.
        /// </summary>
        /// <param name="disposing">Set to <c>true</c> if called within Dispose(),
        /// else <c>false</c>.</param>
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
                        if (_file.Item2 == Ownership.Dispose)
                        {
                            _file.Item1.Dispose();
                        }

                        _file = null;
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        public object GetSection(string SectionName)
        {
            EWFStream es = (EWFStream)Content;
            return es.GetSection(SectionName);            
        }

        /// <summary>
        /// The list of segment files (e01, e02, ...) that make up the disk.
        /// </summary>
        public List<string> Files
        {
            get
            {
                EWFStream es = (EWFStream)Content;
                return es.Files;
            }
        }


        /// <summary>
        /// Gets the MD5 checksum as stored in the EWF file.
        /// </summary>
        public string MD5
        {
            get
            {
                EWFStream es = (EWFStream)Content;
                return es.MD5;
            }
        }

        /// <summary>
        /// Gets the SHA1 checksum as stored in the EWF file.
        /// </summary>
        public string SHA1
        {
            get
            {
                EWFStream es = (EWFStream)Content;
                return es.SHA1;
            }
        }
    }
}
