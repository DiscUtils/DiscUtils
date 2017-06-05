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
using System.Globalization;
using System.IO;
using DiscUtils.Internal;
using DiscUtils.Streams;

namespace DiscUtils.Vhd
{
    /// <summary>
    /// Represents a VHD-backed disk.
    /// </summary>
    public sealed class Disk : VirtualDisk
    {
        /// <summary>
        /// The stream representing the disk's contents.
        /// </summary>
        private SparseStream _content;

        /// <summary>
        /// The list of files that make up the disk.
        /// </summary>
        private List<Tuple<DiskImageFile, Ownership>> _files;

        /// <summary>
        /// Initializes a new instance of the Disk class.  Differencing disks are not supported.
        /// </summary>
        /// <param name="stream">The stream to read.</param>
        /// <param name="ownsStream">Indicates if the new instance should control the lifetime of the stream.</param>
        public Disk(Stream stream, Ownership ownsStream)
        {
            _files = new List<Tuple<DiskImageFile, Ownership>>();
            _files.Add(new Tuple<DiskImageFile, Ownership>(new DiskImageFile(stream, ownsStream),
                Ownership.Dispose));

            if (_files[0].Item1.NeedsParent)
            {
                throw new NotSupportedException("Differencing disks cannot be opened from a stream");
            }
        }

        /// <summary>
        /// Initializes a new instance of the Disk class.  Differencing disks are supported.
        /// </summary>
        /// <param name="path">The path to the disk image.</param>
        public Disk(string path)
        {
            DiskImageFile file = new DiskImageFile(path, FileAccess.ReadWrite);
            _files = new List<Tuple<DiskImageFile, Ownership>>();
            _files.Add(new Tuple<DiskImageFile, Ownership>(file, Ownership.Dispose));
            ResolveFileChain();
        }

        /// <summary>
        /// Initializes a new instance of the Disk class.  Differencing disks are supported.
        /// </summary>
        /// <param name="path">The path to the disk image.</param>
        /// <param name="access">The access requested to the disk.</param>
        public Disk(string path, FileAccess access)
        {
            DiskImageFile file = new DiskImageFile(path, access);
            _files = new List<Tuple<DiskImageFile, Ownership>>();
            _files.Add(new Tuple<DiskImageFile, Ownership>(file, Ownership.Dispose));
            ResolveFileChain();
        }

        /// <summary>
        /// Initializes a new instance of the Disk class.  Differencing disks are supported.
        /// </summary>
        /// <param name="fileSystem">The file system containing the disk.</param>
        /// <param name="path">The file system relative path to the disk.</param>
        /// <param name="access">The access requested to the disk.</param>
        public Disk(DiscFileSystem fileSystem, string path, FileAccess access)
        {
            FileLocator fileLocator = new DiscFileLocator(fileSystem, Utilities.GetDirectoryFromPath(path));
            DiskImageFile file = new DiskImageFile(fileLocator, Utilities.GetFileFromPath(path), access);
            _files = new List<Tuple<DiskImageFile, Ownership>>();
            _files.Add(new Tuple<DiskImageFile, Ownership>(file, Ownership.Dispose));
            ResolveFileChain();
        }

        /// <summary>
        /// Initializes a new instance of the Disk class.
        /// </summary>
        /// <param name="files">The set of image files.</param>
        /// <param name="ownsFiles">Indicates if the new instance controls the lifetime of the image files.</param>
        /// <remarks>The disks should be ordered with the first file referencing the second, etc.  The final
        /// file must not require any parent.</remarks>
        public Disk(IList<DiskImageFile> files, Ownership ownsFiles)
        {
            if (files == null || files.Count == 0)
            {
                throw new ArgumentException("At least one file must be provided");
            }

            if (files[files.Count - 1].NeedsParent)
            {
                throw new ArgumentException("Final image file needs a parent");
            }

            List<Tuple<DiskImageFile, Ownership>> tempList =
                new List<Tuple<DiskImageFile, Ownership>>(files.Count);
            for (int i = 0; i < files.Count - 1; ++i)
            {
                if (!files[i].NeedsParent)
                {
                    throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,
                        "File at index {0} does not have a parent disk", i));
                }

                // Note: Can't do timestamp check, not a property on DiskImageFile.
                if (files[i].Information.DynamicParentUniqueId != files[i + 1].UniqueId)
                {
                    throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,
                        "File at index {0} is not the parent of file at index {1} - Unique Ids don't match", i + 1, i));
                }

                tempList.Add(new Tuple<DiskImageFile, Ownership>(files[i], ownsFiles));
            }

            tempList.Add(new Tuple<DiskImageFile, Ownership>(files[files.Count - 1], ownsFiles));

            _files = tempList;
        }

        /// <summary>
        /// Initializes a new instance of the Disk class.  Differencing disks are supported.
        /// </summary>
        /// <param name="locator">The locator to access relative files.</param>
        /// <param name="path">The path to the disk image.</param>
        /// <param name="access">The access requested to the disk.</param>
        internal Disk(FileLocator locator, string path, FileAccess access)
        {
            DiskImageFile file = new DiskImageFile(locator, path, access);
            _files = new List<Tuple<DiskImageFile, Ownership>>();
            _files.Add(new Tuple<DiskImageFile, Ownership>(file, Ownership.Dispose));
            ResolveFileChain();
        }

        /// <summary>
        /// Initializes a new instance of the Disk class.  Differencing disks are not supported.
        /// </summary>
        /// <param name="file">The file containing the disk.</param>
        /// <param name="ownsFile">Indicates if the new instance should control the lifetime of the file.</param>
        private Disk(DiskImageFile file, Ownership ownsFile)
        {
            _files = new List<Tuple<DiskImageFile, Ownership>>();
            _files.Add(new Tuple<DiskImageFile, Ownership>(file, ownsFile));
            ResolveFileChain();
        }

        /// <summary>
        /// Initializes a new instance of the Disk class.  Differencing disks are supported.
        /// </summary>
        /// <param name="file">The file containing the disk.</param>
        /// <param name="ownsFile">Indicates if the new instance should control the lifetime of the file.</param>
        /// <param name="parentLocator">Object used to locate the parent disk.</param>
        /// <param name="parentPath">Path to the parent disk (if required).</param>
        private Disk(DiskImageFile file, Ownership ownsFile, FileLocator parentLocator, string parentPath)
        {
            _files = new List<Tuple<DiskImageFile, Ownership>>();
            _files.Add(new Tuple<DiskImageFile, Ownership>(file, ownsFile));
            if (file.NeedsParent)
            {
                _files.Add(
                    new Tuple<DiskImageFile, Ownership>(
                        new DiskImageFile(parentLocator, parentPath, FileAccess.Read),
                        Ownership.Dispose));
                ResolveFileChain();
            }
        }

        /// <summary>
        /// Initializes a new instance of the Disk class.  Differencing disks are supported.
        /// </summary>
        /// <param name="file">The file containing the disk.</param>
        /// <param name="ownsFile">Indicates if the new instance should control the lifetime of the file.</param>
        /// <param name="parentFile">The file containing the disk's parent.</param>
        /// <param name="ownsParent">Indicates if the new instance should control the lifetime of the parentFile.</param>
        private Disk(DiskImageFile file, Ownership ownsFile, DiskImageFile parentFile, Ownership ownsParent)
        {
            _files = new List<Tuple<DiskImageFile, Ownership>>();
            _files.Add(new Tuple<DiskImageFile, Ownership>(file, ownsFile));
            if (file.NeedsParent)
            {
                _files.Add(new Tuple<DiskImageFile, Ownership>(parentFile, ownsParent));
                ResolveFileChain();
            }
            else
            {
                if (parentFile != null && ownsParent == Ownership.Dispose)
                {
                    parentFile.Dispose();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the VHD footer is written every time a new block is allocated.
        /// </summary>
        /// <remarks>
        /// This is enabled by default, disabling this can make write activity faster - however,
        /// some software may be unable to access the VHD file if Dispose is not called on this class.
        /// </remarks>
        public bool AutoCommitFooter
        {
            get
            {
                DynamicStream dynContent = Content as DynamicStream;
                if (dynContent == null)
                {
                    return true;
                }

                return dynContent.AutoCommitFooter;
            }

            set
            {
                DynamicStream dynContent = Content as DynamicStream;
                if (dynContent != null)
                {
                    dynContent.AutoCommitFooter = value;
                }
            }
        }

        /// <summary>
        /// Gets the capacity of the disk (in bytes).
        /// </summary>
        public override long Capacity
        {
            get { return _files[0].Item1.Capacity; }
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
            get { return DiskFactory.MakeDiskTypeInfo(_files[_files.Count - 1].Item1.IsSparse ? "dynamic" : "fixed"); }
        }

        /// <summary>
        /// Gets the geometry of the disk.
        /// </summary>
        public override Geometry Geometry
        {
            get { return _files[0].Item1.Geometry; }
        }

        /// <summary>
        /// Gets the layers that make up the disk.
        /// </summary>
        public override IEnumerable<VirtualDiskLayer> Layers
        {
            get
            {
                foreach (Tuple<DiskImageFile, Ownership> file in _files)
                {
                    yield return file.Item1;
                }
            }
        }

        /// <summary>
        /// Initializes a stream as a fixed-sized VHD file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="capacity">The desired capacity of the new disk.</param>
        /// <returns>An object that accesses the stream as a VHD file.</returns>
        public static Disk InitializeFixed(Stream stream, Ownership ownsStream, long capacity)
        {
            return InitializeFixed(stream, ownsStream, capacity, null);
        }

        /// <summary>
        /// Initializes a stream as a fixed-sized VHD file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="capacity">The desired capacity of the new disk.</param>
        /// <param name="geometry">The desired geometry of the new disk, or <c>null</c> for default.</param>
        /// <returns>An object that accesses the stream as a VHD file.</returns>
        public static Disk InitializeFixed(Stream stream, Ownership ownsStream, long capacity, Geometry geometry)
        {
            return new Disk(DiskImageFile.InitializeFixed(stream, ownsStream, capacity, geometry), Ownership.Dispose);
        }

        /// <summary>
        /// Initializes a stream as a dynamically-sized VHD file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="capacity">The desired capacity of the new disk.</param>
        /// <returns>An object that accesses the stream as a VHD file.</returns>
        public static Disk InitializeDynamic(Stream stream, Ownership ownsStream, long capacity)
        {
            return InitializeDynamic(stream, ownsStream, capacity, null);
        }

        /// <summary>
        /// Initializes a stream as a dynamically-sized VHD file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="capacity">The desired capacity of the new disk.</param>
        /// <param name="geometry">The desired geometry of the new disk, or <c>null</c> for default.</param>
        /// <returns>An object that accesses the stream as a VHD file.</returns>
        public static Disk InitializeDynamic(Stream stream, Ownership ownsStream, long capacity, Geometry geometry)
        {
            return new Disk(DiskImageFile.InitializeDynamic(stream, ownsStream, capacity, geometry), Ownership.Dispose);
        }

        /// <summary>
        /// Initializes a stream as a dynamically-sized VHD file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="capacity">The desired capacity of the new disk.</param>
        /// <param name="blockSize">The size of each block (unit of allocation).</param>
        /// <returns>An object that accesses the stream as a VHD file.</returns>
        public static Disk InitializeDynamic(Stream stream, Ownership ownsStream, long capacity, long blockSize)
        {
            return new Disk(DiskImageFile.InitializeDynamic(stream, ownsStream, capacity, blockSize), Ownership.Dispose);
        }

        /// <summary>
        /// Creates a new VHD differencing disk file.
        /// </summary>
        /// <param name="path">The path to the new disk file.</param>
        /// <param name="parentPath">The path to the parent disk file.</param>
        /// <returns>An object that accesses the new file as a Disk.</returns>
        public static Disk InitializeDifferencing(string path, string parentPath)
        {
            LocalFileLocator parentLocator = new LocalFileLocator(Path.GetDirectoryName(parentPath));
            string parentFileName = Path.GetFileName(parentPath);

            DiskImageFile newFile;
            using (DiskImageFile parent = new DiskImageFile(parentLocator, parentFileName, FileAccess.Read))
            {
                LocalFileLocator locator = new LocalFileLocator(Path.GetDirectoryName(path));
                newFile = parent.CreateDifferencing(locator, Path.GetFileName(path));
            }

            return new Disk(newFile, Ownership.Dispose, parentLocator, parentFileName);
        }

        /// <summary>
        /// Initializes a stream as a differencing disk VHD file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the <paramref name="stream"/>.</param>
        /// <param name="parent">The disk this file is a different from.</param>
        /// <param name="ownsParent">Indicates if the new instance controls the lifetime of the <paramref name="parent"/> file.</param>
        /// <param name="parentAbsolutePath">The full path to the parent disk.</param>
        /// <param name="parentRelativePath">The relative path from the new disk to the parent disk.</param>
        /// <param name="parentModificationTime">The time the parent disk's file was last modified (from file system).</param>
        /// <returns>An object that accesses the stream as a VHD file.</returns>
        public static Disk InitializeDifferencing(
            Stream stream,
            Ownership ownsStream,
            DiskImageFile parent,
            Ownership ownsParent,
            string parentAbsolutePath,
            string parentRelativePath,
            DateTime parentModificationTime)
        {
            DiskImageFile file = DiskImageFile.InitializeDifferencing(stream, ownsStream, parent, parentAbsolutePath,
                parentRelativePath, parentModificationTime);
            return new Disk(file, Ownership.Dispose, parent, ownsParent);
        }

        /// <summary>
        /// Create a new differencing disk, possibly within an existing disk.
        /// </summary>
        /// <param name="fileSystem">The file system to create the disk on.</param>
        /// <param name="path">The path (or URI) for the disk to create.</param>
        /// <returns>The newly created disk.</returns>
        public override VirtualDisk CreateDifferencingDisk(DiscFileSystem fileSystem, string path)
        {
            FileLocator locator = new DiscFileLocator(fileSystem, Utilities.GetDirectoryFromPath(path));
            DiskImageFile file = _files[0].Item1.CreateDifferencing(locator, Utilities.GetFileFromPath(path));
            return new Disk(file, Ownership.Dispose);
        }

        /// <summary>
        /// Create a new differencing disk.
        /// </summary>
        /// <param name="path">The path (or URI) for the disk to create.</param>
        /// <returns>The newly created disk.</returns>
        public override VirtualDisk CreateDifferencingDisk(string path)
        {
            FileLocator locator = new LocalFileLocator(Path.GetDirectoryName(path));
            DiskImageFile file = _files[0].Item1.CreateDifferencing(locator, Path.GetFileName(path));
            return new Disk(file, Ownership.Dispose);
        }

        internal static Disk InitializeFixed(FileLocator fileLocator, string path, long capacity, Geometry geometry)
        {
            return new Disk(DiskImageFile.InitializeFixed(fileLocator, path, capacity, geometry), Ownership.Dispose);
        }

        internal static Disk InitializeDynamic(FileLocator fileLocator, string path, long capacity, Geometry geometry,
                                               long blockSize)
        {
            return new Disk(DiskImageFile.InitializeDynamic(fileLocator, path, capacity, geometry, blockSize),
                Ownership.Dispose);
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

                    if (_files != null)
                    {
                        foreach (Tuple<DiskImageFile, Ownership> record in _files)
                        {
                            if (record.Item2 == Ownership.Dispose)
                            {
                                record.Item1.Dispose();
                            }
                        }

                        _files = null;
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private void ResolveFileChain()
        {
            DiskImageFile file = _files[_files.Count - 1].Item1;

            while (file.NeedsParent)
            {
                FileLocator fileLocator = file.RelativeFileLocator;
                bool found = false;
                foreach (string testPath in file.GetParentLocations())
                {
                    if (fileLocator.Exists(testPath))
                    {
                        DiskImageFile newFile = new DiskImageFile(fileLocator, testPath, FileAccess.Read);

                        if (newFile.UniqueId != file.ParentUniqueId)
                        {
                            throw new IOException(string.Format(CultureInfo.CurrentUICulture,
                                "Invalid disk chain found looking for parent with id {0}, found {1} with id {2}",
                                file.ParentUniqueId, newFile.FullPath, newFile.UniqueId));
                        }

                        file = newFile;
                        _files.Add(new Tuple<DiskImageFile, Ownership>(file, Ownership.Dispose));
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    throw new IOException(string.Format(CultureInfo.InvariantCulture,
                        "Failed to find parent for disk '{0}'", file.FullPath));
                }
            }
        }
    }
}