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

        /// <summary>
        /// The stream representing the content of this disk.
        /// </summary>
        private SparseStream _content;

        private string _path;


        /// <summary>
        /// Creates a new instance from a file on disk.
        /// </summary>
        /// <param name="path">The path to the disk</param>
        /// <param name="access">The access requested to the disk</param>
        public Disk(string path, FileAccess access)
            : this(new LocalFileLocator(Path.GetDirectoryName(path)), path, access)
        {
        }

        /// <summary>
        /// Creates a new instance from a file on a file system.
        /// </summary>
        /// <param name="fileSystem">The file system containing the disk.</param>
        /// <param name="path">The file system relative path to the disk.</param>
        /// <param name="access">The access requested to the disk.</param>
        public Disk(DiscFileSystem fileSystem, string path, FileAccess access)
        {
            _path = path;
            FileLocator fileLocator = new DiscFileLocator(fileSystem, Path.GetDirectoryName(path));
            _files = new List<Tuple<DiskImageFile, Ownership>>();
            _files.Add(new Tuple<DiskImageFile, Ownership>(new DiskImageFile(fileLocator, Path.GetFileName(path), access), Ownership.Dispose));
            ResolveFileChain();
        }

        /// <summary>
        /// Creates a new instance from a stream, only monolithic sparse streams are supported.
        /// </summary>
        /// <param name="stream">The stream containing the VMDK file</param>
        /// <param name="ownsStream">Indicates if the new instances owns the stream.</param>
        public Disk(Stream stream, Ownership ownsStream)
        {
            FileStream fileStream = stream as FileStream;
            if (fileStream != null)
            {
                _path = fileStream.Name;
            }

            _files = new List<Tuple<DiskImageFile, Ownership>>();
            _files.Add(new Tuple<DiskImageFile, Ownership>(new DiskImageFile(stream, ownsStream), Ownership.Dispose));
        }

        internal Disk(DiskImageFile file, Ownership ownsStream)
        {
            _files = new List<Tuple<DiskImageFile, Ownership>>();
            _files.Add(new Tuple<DiskImageFile, Ownership>(file, ownsStream));
            ResolveFileChain();
        }

        internal Disk(FileLocator layerLocator, string path, FileAccess access)
        {
            _path = path;
            _files = new List<Tuple<DiskImageFile, Ownership>>();
            _files.Add(new Tuple<DiskImageFile, Ownership>(new DiskImageFile(layerLocator, path, access), Ownership.Dispose));
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
        /// <param name="parameters">The desired parameters for the new disk.</param>
        /// <returns>The newly created disk image</returns>
        public static Disk Initialize(string path, DiskParameters parameters)
        {
            return new Disk(DiskImageFile.Initialize(path, parameters), Ownership.Dispose);
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
            return Initialize(path, capacity, null, type);
        }

        /// <summary>
        /// Creates a new virtual disk at the specified path.
        /// </summary>
        /// <param name="path">The name of the VMDK to create.</param>
        /// <param name="capacity">The desired capacity of the new disk</param>
        /// <param name="geometry">The desired geometry of the new disk, or <c>null</c> for default</param>
        /// <param name="type">The type of virtual disk to create</param>
        /// <returns>The newly created disk image</returns>
        public static Disk Initialize(string path, long capacity, Geometry geometry, DiskCreateType type)
        {
            return new Disk(DiskImageFile.Initialize(path, capacity, geometry, type), Ownership.Dispose);
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
        /// Creates a new virtual disk at the specified path.
        /// </summary>
        /// <param name="path">The name of the VMDK to create.</param>
        /// <param name="capacity">The desired capacity of the new disk</param>
        /// <param name="type">The type of virtual disk to create</param>
        /// <param name="adapterType">The type of virtual disk adapter</param>
        /// <returns>The newly created disk image</returns>
        public static Disk Initialize(string path, long capacity, DiskCreateType type, DiskAdapterType adapterType)
        {
            return Initialize(path, capacity, null, type, adapterType);
        }

        /// <summary>
        /// Creates a new virtual disk at the specified path.
        /// </summary>
        /// <param name="path">The name of the VMDK to create.</param>
        /// <param name="capacity">The desired capacity of the new disk</param>
        /// <param name="geometry">The desired geometry of the new disk, or <c>null</c> for default</param>
        /// <param name="type">The type of virtual disk to create</param>
        /// <param name="adapterType">The type of virtual disk adapter</param>
        /// <returns>The newly created disk image</returns>
        public static Disk Initialize(string path, long capacity, Geometry geometry, DiskCreateType type, DiskAdapterType adapterType)
        {
            return new Disk(DiskImageFile.Initialize(path, capacity, geometry, type, adapterType), Ownership.Dispose);
        }

        /// <summary>
        /// Creates a new virtual disk at the specified location on a file system.
        /// </summary>
        /// <param name="fileSystem">The file system to contain the disk</param>
        /// <param name="path">The file system path to the disk</param>
        /// <param name="capacity">The desired capacity of the new disk</param>
        /// <param name="type">The type of virtual disk to create</param>
        /// <param name="adapterType">The type of virtual disk adapter</param>
        /// <returns>The newly created disk image</returns>
        public static Disk Initialize(DiscFileSystem fileSystem, string path, long capacity, DiskCreateType type, DiskAdapterType adapterType)
        {
            return new Disk(DiskImageFile.Initialize(fileSystem, path, capacity, type, adapterType), Ownership.Dispose);
        }

        internal static Disk Initialize(FileLocator fileLocator, string path, DiskParameters parameters)
        {
            return new Disk(DiskImageFile.Initialize(fileLocator, path, parameters), Ownership.Dispose);
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
            return new Disk(DiskImageFile.InitializeDifferencing(fileSystem, path, type, parentPath), Ownership.Dispose);
        }

        /// <summary>
        /// Gets the Geometry of this disk.
        /// </summary>
        public override Geometry Geometry
        {
            get { return _files[_files.Count - 1].First.Geometry; }
        }

        /// <summary>
        /// Gets the geometry of the disk as it is anticipated a hypervisor BIOS will represent it.
        /// </summary>
        public override Geometry BiosGeometry
        {
            get
            {
                DiskImageFile file = _files[_files.Count - 1].First;
                Geometry result = file.BiosGeometry;
                return file.BiosGeometry ?? Geometry.MakeBiosSafe(file.Geometry, Capacity);
            }
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
        /// <remarks>Note the returned stream is not guaranteed to be at any particular position.  The actual position
        /// will depend on the last partition table/file system activity, since all access to the disk contents pass
        /// through a single stream instance.  Set the stream position before accessing the stream.</remarks>
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
        public override IEnumerable<VirtualDiskLayer> Layers
        {
            get
            {
                foreach (var file in _files)
                {
                    yield return (file.First as VirtualDiskLayer);
                }
            }
        }

        /// <summary>
        /// Gets the links that make up the disk (type-safe version of Layers).
        /// </summary>
        public IEnumerable<DiskImageFile> Links
        {
            get
            {
                foreach (var file in _files)
                {
                    yield return file.First;
                }
            }
        }

        /// <summary>
        /// Create a new differencing disk, possibly within an existing disk.
        /// </summary>
        /// <param name="fileSystem">The file system to create the disk on</param>
        /// <param name="path">The path (or URI) for the disk to create</param>
        /// <returns>The newly created disk</returns>
        public override VirtualDisk CreateDifferencingDisk(DiscFileSystem fileSystem, string path)
        {
            return InitializeDifferencing(fileSystem, path, DiffDiskCreateType(_files[0].First.CreateType), _path);
        }

        /// <summary>
        /// Create a new differencing disk.
        /// </summary>
        /// <param name="path">The path (or URI) for the disk to create</param>
        /// <returns>The newly created disk</returns>
        public override VirtualDisk CreateDifferencingDisk(string path)
        {
            var firstLayer = _files[0].First;
            return InitializeDifferencing(path, DiffDiskCreateType(firstLayer.CreateType), firstLayer.RelativeFileLocator.GetFullPath(_path));
        }

        private static DiskCreateType DiffDiskCreateType(DiskCreateType diskCreateType)
        {
            switch (diskCreateType)
            {
                case DiskCreateType.FullDevice:
                case DiskCreateType.MonolithicFlat:
                case DiskCreateType.MonolithicSparse:
                case DiskCreateType.PartitionedDevice:
                case DiskCreateType.StreamOptimized:
                case DiskCreateType.TwoGbMaxExtentFlat:
                case DiskCreateType.TwoGbMaxExtentSparse:
                    return DiskCreateType.MonolithicSparse;
                default:
                    return DiskCreateType.VmfsSparse;
            }
        }

        private void ResolveFileChain()
        {
            DiskImageFile file = _files[_files.Count - 1].First;

            while (file.NeedsParent)
            {
                file = new DiskImageFile(file.RelativeFileLocator, file.ParentLocation, FileAccess.Read);
                _files.Add(new Tuple<DiskImageFile, Ownership>(file, Ownership.Dispose));
            }
        }
    }
}
