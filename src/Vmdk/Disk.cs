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
using System.Collections.ObjectModel;
using System.IO;

namespace DiscUtils.Vmdk
{
    /// <summary>
    /// Represents a VMDK-backed disk.
    /// </summary>
    public sealed class Disk : VirtualDisk
    {
        /// <summary>
        /// The list of files that make up the disk.
        /// </summary>
        private List<Tuple<DiskImageFile, Ownership>> _files;

        private SparseStream _content;
        private FileLocator _layerLocator;

        /// <summary>
        /// Creates a new instance from a file on disk.
        /// </summary>
        /// <param name="path">The path to the disk</param>
        /// <param name="access">The access requested to the disk</param>
        public Disk(string path, FileAccess access)
        {
            _layerLocator = new LocalFileLocator(Path.GetDirectoryName(path));
            _files = new List<Tuple<DiskImageFile, Ownership>>();
            _files.Add(new Tuple<DiskImageFile, Ownership>(new DiskImageFile(path, access), Ownership.Dispose));
            ResolveFileChain();
        }

        /// <summary>
        /// Creates a new instance from a file on a file system.
        /// </summary>
        /// <param name="fileSystem">The file system containing the disk.</param>
        /// <param name="path">The file system relative path to the disk.</param>
        /// <param name="access">The access requested to the disk.</param>
        public Disk(DiscFileSystem fileSystem, string path, FileAccess access)
        {
            _layerLocator = new DiscFileLocator(fileSystem, Path.GetDirectoryName(path));
            _files = new List<Tuple<DiskImageFile, Ownership>>();
            _files.Add(new Tuple<DiskImageFile, Ownership>(new DiskImageFile(_layerLocator, Path.GetFileName(path), access), Ownership.Dispose));
            ResolveFileChain();
        }

        /// <summary>
        /// Creates a new instance from a stream, only monolithic sparse streams are supported.
        /// </summary>
        /// <param name="stream">The stream containing the VMDK file</param>
        /// <param name="ownsStream">Indicates if the new instances owns the stream.</param>
        public Disk(Stream stream, Ownership ownsStream)
        {
            _files = new List<Tuple<DiskImageFile, Ownership>>();
            _files.Add(new Tuple<DiskImageFile, Ownership>(new DiskImageFile(stream, ownsStream), Ownership.Dispose));
        }

        internal Disk(DiskImageFile file, Ownership ownsStream)
        {
            if (file.NeedsParent)
            {
                throw new ArgumentException("Cannot create disk from file that needs parent");
            }

            _files = new List<Tuple<DiskImageFile, Ownership>>();
            _files.Add(new Tuple<DiskImageFile, Ownership>(file, ownsStream));
        }

        internal Disk(DiskImageFile file, Ownership ownsStream, FileLocator layerLocator)
        {
            _layerLocator = layerLocator;
            _files = new List<Tuple<DiskImageFile, Ownership>>();
            _files.Add(new Tuple<DiskImageFile, Ownership>(file, ownsStream));
            ResolveFileChain();
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

                    foreach (var file in _files)
                    {
                        if (file.Second == Ownership.Dispose)
                        {
                            file.First.Dispose();
                        }
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Creates a new virtual disk at the specified path.
        /// </summary>
        /// <param name="path">The name of the VMDK to create.</param>
        /// <param name="capacity">The desired capacity of the new disk</param>
        /// <param name="type">The type of virtual disk to create</param>
        /// <returns>The newly created disk image</returns>
        public static Disk Initialize(string path, long capacity, DiskCreateType type)
        {
            return new Disk(DiskImageFile.Initialize(path, capacity, type), Ownership.Dispose);
        }

        /// <summary>
        /// Creates a new virtual disk at the specified location on a file system.
        /// </summary>
        /// <param name="fileSystem">The file system to contain the disk</param>
        /// <param name="path">The file system path to the disk</param>
        /// <param name="capacity">The desired capacity of the new disk</param>
        /// <param name="type">The type of virtual disk to create</param>
        /// <returns>The newly created disk image</returns>
        public static Disk Initialize(DiscFileSystem fileSystem, string path, long capacity, DiskCreateType type)
        {
            return new Disk(DiskImageFile.Initialize(fileSystem, path, capacity, type), Ownership.Dispose);
        }

        /// <summary>
        /// Creates a new virtual disk as a thin clone of an existing disk.
        /// </summary>
        /// <param name="path">The path to the new disk.</param>
        /// <param name="type">The type of disk to create</param>
        /// <param name="parentPath">The path to the parent disk.</param>
        /// <returns>The new disk.</returns>
        public static Disk InitializeDifferencing(string path, DiskCreateType type, string parentPath)
        {
            return new Disk(DiskImageFile.InitializeDifferencing(path, type, parentPath), Ownership.Dispose);
        }

        /// <summary>
        /// Creates a new virtual disk as a thin clone of an existing disk.
        /// </summary>
        /// <param name="fileSystem">The file system to contain the disk</param>
        /// <param name="path">The path to the new disk.</param>
        /// <param name="type">The type of disk to create</param>
        /// <param name="parentPath">The path to the parent disk.</param>
        /// <returns>The new disk.</returns>
        public static Disk InitializeDifferencing(DiscFileSystem fileSystem, string path, DiskCreateType type, string parentPath)
        {
            FileLocator fileLoc = new DiscFileLocator(fileSystem, Path.GetDirectoryName(path));
            return new Disk(DiskImageFile.InitializeDifferencing(fileSystem, path, type, parentPath), Ownership.Dispose, fileLoc);
        }

        /// <summary>
        /// Gets the Geometry of this disk.
        /// </summary>
        public override Geometry Geometry
        {
            get { return _files[_files.Count - 1].First.Geometry; }
        }

        /// <summary>
        /// Gets the capacity of this disk (in bytes).
        /// </summary>
        public override long Capacity
        {
            get { return _files[_files.Count - 1].First.Capacity; }
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
                    SparseStream stream = null;
                    for (int i = _files.Count - 1; i >= 0; --i)
                    {
                        stream = _files[i].First.OpenContent(stream, Ownership.Dispose);
                    }
                    _content = stream;
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
                VirtualDiskLayer[] layers = Utilities.Map<Tuple<DiskImageFile, Ownership>, VirtualDiskLayer>(_files, (f) => f.First);
                return new ReadOnlyCollection<VirtualDiskLayer>(layers);
            }
        }

        private void ResolveFileChain()
        {
            DiskImageFile file = _files[_files.Count - 1].First;

            while (file.NeedsParent)
            {
                Stream fileStream = _layerLocator.Open(file.ParentLocation, FileMode.Open, FileAccess.Read, FileShare.Read);
                file = new DiskImageFile(fileStream, Ownership.Dispose, _layerLocator.GetRelativeLocator(Path.GetDirectoryName(file.ParentLocation)));
                _files.Add(new Tuple<DiskImageFile, Ownership>(file, Ownership.Dispose));
            }
        }
    }
}
