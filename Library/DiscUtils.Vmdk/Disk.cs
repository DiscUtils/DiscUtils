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
using System.Collections.Generic;
using System.IO;
using DiscUtils.Internal;
using DiscUtils.Streams;

namespace DiscUtils.Vmdk
{
    /// <summary>
    /// Represents a VMDK-backed disk.
    /// </summary>
    public sealed class Disk : VirtualDisk
    {
        internal const string ExtendedParameterKeyAdapterType = "VMDK.AdapterType";
        internal const string ExtendedParameterKeyCreateType = "VMDK.CreateType";

        /// <summary>
        /// The stream representing the content of this disk.
        /// </summary>
        private SparseStream _content;

        /// <summary>
        /// The list of files that make up the disk.
        /// </summary>
        private readonly List<Tuple<VirtualDiskLayer, Ownership>> _files;

        private readonly string _path;

        /// <summary>
        /// Initializes a new instance of the Disk class.
        /// </summary>
        /// <param name="path">The path to the disk.</param>
        /// <param name="access">The access requested to the disk.</param>
        public Disk(string path, FileAccess access)
            : this(new LocalFileLocator(Path.GetDirectoryName(path)), path, access) {}

        /// <summary>
        /// Initializes a new instance of the Disk class.
        /// </summary>
        /// <param name="fileSystem">The file system containing the disk.</param>
        /// <param name="path">The file system relative path to the disk.</param>
        /// <param name="access">The access requested to the disk.</param>
        public Disk(DiscFileSystem fileSystem, string path, FileAccess access)
        {
            _path = path;
            FileLocator fileLocator = new DiscFileLocator(fileSystem, Utilities.GetDirectoryFromPath(path));
            _files = new List<Tuple<VirtualDiskLayer, Ownership>>();
            _files.Add(
                new Tuple<VirtualDiskLayer, Ownership>(
                    new DiskImageFile(fileLocator, Utilities.GetFileFromPath(path), access), Ownership.Dispose));
            ResolveFileChain();
        }

        /// <summary>
        /// Initializes a new instance of the Disk class.  Only monolithic sparse streams are supported.
        /// </summary>
        /// <param name="stream">The stream containing the VMDK file.</param>
        /// <param name="ownsStream">Indicates if the new instances owns the stream.</param>
        public Disk(Stream stream, Ownership ownsStream)
        {
            FileStream fileStream = stream as FileStream;
            if (fileStream != null)
            {
                _path = fileStream.Name;
            }

            _files = new List<Tuple<VirtualDiskLayer, Ownership>>();
            _files.Add(new Tuple<VirtualDiskLayer, Ownership>(new DiskImageFile(stream, ownsStream),
                Ownership.Dispose));
        }

        internal Disk(DiskImageFile file, Ownership ownsStream)
        {
            _files = new List<Tuple<VirtualDiskLayer, Ownership>>();
            _files.Add(new Tuple<VirtualDiskLayer, Ownership>(file, ownsStream));
            ResolveFileChain();
        }

        internal Disk(FileLocator layerLocator, string path, FileAccess access)
        {
            _path = path;
            _files = new List<Tuple<VirtualDiskLayer, Ownership>>();
            _files.Add(new Tuple<VirtualDiskLayer, Ownership>(new DiskImageFile(layerLocator, path, access),
                Ownership.Dispose));
            ResolveFileChain();
        }

        /// <summary>
        /// Gets the geometry of the disk as it is anticipated a hypervisor BIOS will represent it.
        /// </summary>
        public override Geometry BiosGeometry
        {
            get
            {
                DiskImageFile file = _files[_files.Count - 1].Item1 as DiskImageFile;
                Geometry result = file != null ? file.BiosGeometry : null;
                return result ?? Geometry.MakeBiosSafe(_files[_files.Count - 1].Item1.Geometry, Capacity);
            }
        }

        /// <summary>
        /// Gets the capacity of this disk (in bytes).
        /// </summary>
        public override long Capacity
        {
            get { return _files[0].Item1.Capacity; }
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
                        stream = _files[i].Item1.OpenContent(stream, Ownership.Dispose);
                    }

                    _content = stream;
                }

                return _content;
            }
        }

        /// <summary>
        /// Gets the type of disk represented by this object.
        /// </summary>
        public override VirtualDiskClass DiskClass
        {
            get { return VirtualDiskClass.HardDisk; }
        }

        /// <summary>
        /// Gets information about the type of disk.
        /// </summary>
        /// <remarks>This property provides access to meta-data about the disk format, for example whether the
        /// BIOS geometry is preserved in the disk file.</remarks>
        public override VirtualDiskTypeInfo DiskTypeInfo
        {
            get { return DiskFactory.MakeDiskTypeInfo(((DiskImageFile)_files[_files.Count - 1].Item1).CreateType); }
        }

        /// <summary>
        /// Gets the Geometry of this disk.
        /// </summary>
        public override Geometry Geometry
        {
            get { return _files[_files.Count - 1].Item1.Geometry; }
        }

        /// <summary>
        /// Gets the layers that make up the disk.
        /// </summary>
        public override IEnumerable<VirtualDiskLayer> Layers
        {
            get
            {
                foreach (Tuple<VirtualDiskLayer, Ownership> file in _files)
                {
                    yield return file.Item1;
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
                foreach (Tuple<VirtualDiskLayer, Ownership> file in _files)
                {
                    yield return (DiskImageFile)file.Item1;
                }
            }
        }

        /// <summary>
        /// Gets the parameters of the disk.
        /// </summary>
        /// <remarks>Most of the parameters are also available individually, such as DiskType and Capacity.</remarks>
        public override VirtualDiskParameters Parameters
        {
            get
            {
                DiskImageFile file = (DiskImageFile)_files[_files.Count - 1].Item1;

                VirtualDiskParameters diskParams = new VirtualDiskParameters
                {
                    DiskType = DiskClass,
                    Capacity = Capacity,
                    Geometry = Geometry,
                    BiosGeometry = BiosGeometry,
                    AdapterType =
                        file.AdapterType == DiskAdapterType.Ide
                            ? GenericDiskAdapterType.Ide
                            : GenericDiskAdapterType.Scsi
                };

                diskParams.ExtendedParameters[ExtendedParameterKeyAdapterType] = file.AdapterType.ToString();
                diskParams.ExtendedParameters[ExtendedParameterKeyCreateType] = file.CreateType.ToString();

                return diskParams;
            }
        }

        /// <summary>
        /// Creates a new virtual disk at the specified path.
        /// </summary>
        /// <param name="path">The name of the VMDK to create.</param>
        /// <param name="parameters">The desired parameters for the new disk.</param>
        /// <returns>The newly created disk image.</returns>
        public static Disk Initialize(string path, DiskParameters parameters)
        {
            return new Disk(DiskImageFile.Initialize(path, parameters), Ownership.Dispose);
        }

        /// <summary>
        /// Creates a new virtual disk at the specified path.
        /// </summary>
        /// <param name="path">The name of the VMDK to create.</param>
        /// <param name="capacity">The desired capacity of the new disk.</param>
        /// <param name="type">The type of virtual disk to create.</param>
        /// <returns>The newly created disk image.</returns>
        public static Disk Initialize(string path, long capacity, DiskCreateType type)
        {
            return Initialize(path, capacity, null, type);
        }

        /// <summary>
        /// Creates a new virtual disk at the specified path.
        /// </summary>
        /// <param name="path">The name of the VMDK to create.</param>
        /// <param name="capacity">The desired capacity of the new disk.</param>
        /// <param name="geometry">The desired geometry of the new disk, or <c>null</c> for default.</param>
        /// <param name="type">The type of virtual disk to create.</param>
        /// <returns>The newly created disk image.</returns>
        public static Disk Initialize(string path, long capacity, Geometry geometry, DiskCreateType type)
        {
            return new Disk(DiskImageFile.Initialize(path, capacity, geometry, type), Ownership.Dispose);
        }

        /// <summary>
        /// Creates a new virtual disk at the specified location on a file system.
        /// </summary>
        /// <param name="fileSystem">The file system to contain the disk.</param>
        /// <param name="path">The file system path to the disk.</param>
        /// <param name="capacity">The desired capacity of the new disk.</param>
        /// <param name="type">The type of virtual disk to create.</param>
        /// <returns>The newly created disk image.</returns>
        public static Disk Initialize(DiscFileSystem fileSystem, string path, long capacity, DiskCreateType type)
        {
            return new Disk(DiskImageFile.Initialize(fileSystem, path, capacity, type), Ownership.Dispose);
        }

        /// <summary>
        /// Creates a new virtual disk at the specified path.
        /// </summary>
        /// <param name="path">The name of the VMDK to create.</param>
        /// <param name="capacity">The desired capacity of the new disk.</param>
        /// <param name="type">The type of virtual disk to create.</param>
        /// <param name="adapterType">The type of virtual disk adapter.</param>
        /// <returns>The newly created disk image.</returns>
        public static Disk Initialize(string path, long capacity, DiskCreateType type, DiskAdapterType adapterType)
        {
            return Initialize(path, capacity, null, type, adapterType);
        }

        /// <summary>
        /// Creates a new virtual disk at the specified path.
        /// </summary>
        /// <param name="path">The name of the VMDK to create.</param>
        /// <param name="capacity">The desired capacity of the new disk.</param>
        /// <param name="geometry">The desired geometry of the new disk, or <c>null</c> for default.</param>
        /// <param name="type">The type of virtual disk to create.</param>
        /// <param name="adapterType">The type of virtual disk adapter.</param>
        /// <returns>The newly created disk image.</returns>
        public static Disk Initialize(string path, long capacity, Geometry geometry, DiskCreateType type,
                                      DiskAdapterType adapterType)
        {
            return new Disk(DiskImageFile.Initialize(path, capacity, geometry, type, adapterType), Ownership.Dispose);
        }

        /// <summary>
        /// Creates a new virtual disk at the specified location on a file system.
        /// </summary>
        /// <param name="fileSystem">The file system to contain the disk.</param>
        /// <param name="path">The file system path to the disk.</param>
        /// <param name="capacity">The desired capacity of the new disk.</param>
        /// <param name="type">The type of virtual disk to create.</param>
        /// <param name="adapterType">The type of virtual disk adapter.</param>
        /// <returns>The newly created disk image.</returns>
        public static Disk Initialize(DiscFileSystem fileSystem, string path, long capacity, DiskCreateType type,
                                      DiskAdapterType adapterType)
        {
            return new Disk(DiskImageFile.Initialize(fileSystem, path, capacity, type, adapterType), Ownership.Dispose);
        }

        /// <summary>
        /// Creates a new virtual disk as a thin clone of an existing disk.
        /// </summary>
        /// <param name="path">The path to the new disk.</param>
        /// <param name="type">The type of disk to create.</param>
        /// <param name="parentPath">The path to the parent disk.</param>
        /// <returns>The new disk.</returns>
        public static Disk InitializeDifferencing(string path, DiskCreateType type, string parentPath)
        {
            return new Disk(DiskImageFile.InitializeDifferencing(path, type, parentPath), Ownership.Dispose);
        }

        /// <summary>
        /// Creates a new virtual disk as a thin clone of an existing disk.
        /// </summary>
        /// <param name="fileSystem">The file system to contain the disk.</param>
        /// <param name="path">The path to the new disk.</param>
        /// <param name="type">The type of disk to create.</param>
        /// <param name="parentPath">The path to the parent disk.</param>
        /// <returns>The new disk.</returns>
        public static Disk InitializeDifferencing(DiscFileSystem fileSystem, string path, DiskCreateType type,
                                                  string parentPath)
        {
            return new Disk(DiskImageFile.InitializeDifferencing(fileSystem, path, type, parentPath), Ownership.Dispose);
        }

        /// <summary>
        /// Create a new differencing disk, possibly within an existing disk.
        /// </summary>
        /// <param name="fileSystem">The file system to create the disk on.</param>
        /// <param name="path">The path (or URI) for the disk to create.</param>
        /// <returns>The newly created disk.</returns>
        public override VirtualDisk CreateDifferencingDisk(DiscFileSystem fileSystem, string path)
        {
            return InitializeDifferencing(fileSystem, path, DiffDiskCreateType(_files[0].Item1), _path);
        }

        /// <summary>
        /// Create a new differencing disk.
        /// </summary>
        /// <param name="path">The path (or URI) for the disk to create.</param>
        /// <returns>The newly created disk.</returns>
        public override VirtualDisk CreateDifferencingDisk(string path)
        {
            VirtualDiskLayer firstLayer = _files[0].Item1;
            return InitializeDifferencing(path, DiffDiskCreateType(firstLayer),
                firstLayer.RelativeFileLocator.GetFullPath(_path));
        }

        internal static Disk Initialize(FileLocator fileLocator, string path, DiskParameters parameters)
        {
            return new Disk(DiskImageFile.Initialize(fileLocator, path, parameters), Ownership.Dispose);
        }

        /// <summary>
        /// Disposes of this instance.
        /// </summary>
        /// <param name="disposing"><c>true</c> if disposing, <c>false</c> if in destructor.</param>
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

                    foreach (Tuple<VirtualDiskLayer, Ownership> file in _files)
                    {
                        if (file.Item2 == Ownership.Dispose)
                        {
                            file.Item1.Dispose();
                        }
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private static DiskCreateType DiffDiskCreateType(VirtualDiskLayer layer)
        {
            DiskImageFile vmdkLayer = layer as DiskImageFile;
            if (vmdkLayer != null)
            {
                switch (vmdkLayer.CreateType)
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
            return DiskCreateType.MonolithicSparse;
        }

        private void ResolveFileChain()
        {
            VirtualDiskLayer file = _files[_files.Count - 1].Item1;

            while (file.NeedsParent)
            {
                bool foundParent = false;
                FileLocator locator = file.RelativeFileLocator;

                foreach (string posParent in file.GetParentLocations())
                {
                    if (locator.Exists(posParent))
                    {
                        file = OpenDiskLayer(file.RelativeFileLocator, posParent, FileAccess.Read);
                        _files.Add(new Tuple<VirtualDiskLayer, Ownership>(file, Ownership.Dispose));
                        foundParent = true;
                        break;
                    }
                }

                if (!foundParent)
                {
                    throw new IOException("Parent disk not found");
                }
            }
        }
    }
}