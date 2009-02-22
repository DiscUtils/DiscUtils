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
using System.Globalization;
using System.IO;

namespace DiscUtils.Vhd
{
    /// <summary>
    /// Represents a VHD-backed disk.
    /// </summary>
    public sealed class Disk : VirtualDisk
    {
        /// <summary>
        /// The list of files that make up the disk.
        /// </summary>
        private List<Tuple<DiskImageFile, Ownership>> _files;

        /// <summary>
        /// The stream representing the disk's contents.
        /// </summary>
        private SparseStream _content;

        /// <summary>
        /// Creates a new instance from an existing stream, differencing disks not supported.
        /// </summary>
        /// <param name="stream">The stream to read</param>
        /// <param name="ownsStream">Indicates if the new instance should control the lifetime of the stream.</param>
        public Disk(Stream stream, Ownership ownsStream)
        {
            _files = new List<Tuple<DiskImageFile, Ownership>>();
            _files.Add(new Tuple<DiskImageFile, Ownership>(new DiskImageFile(stream, ownsStream), Ownership.Dispose));

            if (_files[0].First.NeedsParent)
            {
                throw new NotSupportedException("Differencing disks cannot be opened from a stream");
            }
        }

        /// <summary>
        /// Creates a new instance from an existing file, differencing disks are supported.
        /// </summary>
        /// <param name="path">The path to the disk image</param>
        public Disk(string path)
        {
            DiskImageFile file = new DiskImageFile(new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None), Ownership.Dispose);
            _files = new List<Tuple<DiskImageFile, Ownership>>();
            _files.Add(new Tuple<DiskImageFile, Ownership>(file, Ownership.Dispose));
            ResolveFileChain(path);
        }

        /// <summary>
        /// Creates a new instance from an existing stream, differencing disks not supported.
        /// </summary>
        /// <param name="file">The file containing the disk</param>
        /// <param name="ownsFile">Indicates if the new instance should control the lifetime of the file.</param>
        private Disk(DiskImageFile file, Ownership ownsFile)
        {
            if (file.NeedsParent)
            {
                throw new NotSupportedException("Differencing disks cannot be opened from a single file");
            }

            _files = new List<Tuple<DiskImageFile, Ownership>>();
            _files.Add(new Tuple<DiskImageFile, Ownership>(file, ownsFile));
        }

        /// <summary>
        /// Creates a new instance from an existing stream, differencing disks supported.
        /// </summary>
        /// <param name="file">The file containing the disk</param>
        /// <param name="ownsFile">Indicates if the new instance should control the lifetime of the file.</param>
        /// <param name="parentPath">Path to the parent disk (if required)</param>
        private Disk(DiskImageFile file, Ownership ownsFile, string parentPath)
        {
            _files = new List<Tuple<DiskImageFile, Ownership>>();
            _files.Add(new Tuple<DiskImageFile, Ownership>(file, ownsFile));
            if (file.NeedsParent)
            {
                _files.Add(
                    new Tuple<DiskImageFile, Ownership>(
                        new DiskImageFile(
                            new FileStream(parentPath, FileMode.Open, FileAccess.Read, FileShare.Read),
                            Ownership.Dispose),
                        Ownership.Dispose));
                ResolveFileChain(parentPath);
            }
        }

        /// <summary>
        /// Creates a new instance from an existing stream, differencing disks supported.
        /// </summary>
        /// <param name="file">The file containing the disk</param>
        /// <param name="ownsFile">Indicates if the new instance should control the lifetime of the file.</param>
        /// <param name="parentFile">The file containing the disk's parent</param>
        /// <param name="ownsParent">Indicates if the new instance should control the lifetime of the parentFile</param>
        /// <param name="parentPath">Path to the parent disk (if required)</param>
        private Disk(DiskImageFile file, Ownership ownsFile, DiskImageFile parentFile, Ownership ownsParent, string parentPath)
        {
            _files = new List<Tuple<DiskImageFile, Ownership>>();
            _files.Add(new Tuple<DiskImageFile, Ownership>(file, ownsFile));
            if (file.NeedsParent)
            {
                _files.Add(new Tuple<DiskImageFile, Ownership>(parentFile, ownsParent));
                ResolveFileChain(parentPath);
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

                    foreach (var record in _files)
                    {
                        if (record.Second == Ownership.Dispose)
                        {
                            record.First.Dispose();
                        }
                    }
                    _files = null;
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Initializes a stream as a fixed-sized VHD file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="capacity">The desired capacity of the new disk</param>
        /// <returns>An object that accesses the stream as a VHD file</returns>
        public static Disk InitializeFixed(Stream stream, Ownership ownsStream, long capacity)
        {
            return new Disk(DiskImageFile.InitializeFixed(stream, ownsStream, capacity), Ownership.Dispose);
        }

        /// <summary>
        /// Initializes a stream as a dynamically-sized VHD file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="capacity">The desired capacity of the new disk</param>
        /// <returns>An object that accesses the stream as a VHD file</returns>
        public static Disk InitializeDynamic(Stream stream, Ownership ownsStream, long capacity)
        {
            return new Disk(DiskImageFile.InitializeDynamic(stream, ownsStream, capacity), Ownership.Dispose);
        }

        /// <summary>
        /// Creates a new VHD differencing disk file.
        /// </summary>
        /// <param name="path">The path to the new disk file</param>
        /// <param name="parentPath">The path to the parent disk file</param>
        /// <returns>An object that accesses the new file as a Disk</returns>
        public static Disk InitializeDifferencing(String path, string parentPath)
        {
            DiskImageFile parent = null;
            DiskImageFile newFile = null;

            string fullParentPath = Path.GetFullPath(parentPath);
            string fullNewFilePath = Path.GetFullPath(path);

            try
            {
                parent = new DiskImageFile(
                    new FileStream(fullParentPath, FileMode.Open, FileAccess.Read, FileShare.Read),
                    Ownership.Dispose);

                newFile = DiskImageFile.InitializeDifferencing(
                    new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None),
                    Ownership.Dispose,
                    parent,
                    fullParentPath,
                    Utilities.MakeRelativePath(fullParentPath, fullNewFilePath),
                    File.GetLastWriteTimeUtc(parentPath));

                Disk newDisk = new Disk(newFile, Ownership.Dispose, fullParentPath);
                newFile = null;
                return newDisk;
            }
            finally
            {
                if (parent != null)
                {
                    parent.Dispose();
                }

                if (newFile != null)
                {
                    newFile.Dispose();
                }
            }
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
        /// <returns>An object that accesses the stream as a VHD file</returns>
        public static Disk InitializeDifferencing(
            Stream stream, Ownership ownsStream, DiskImageFile parent, Ownership ownsParent,
            string parentAbsolutePath, string parentRelativePath, DateTime parentModificationTime)
        {
            DiskImageFile file = DiskImageFile.InitializeDifferencing(stream, ownsStream, parent, parentAbsolutePath, parentRelativePath, parentModificationTime);
            return new Disk(file, Ownership.Dispose, parent, ownsParent, parentAbsolutePath);
        }

        /// <summary>
        /// Gets the geometry of the disk.
        /// </summary>
        public override Geometry Geometry
        {
            get { return _files[0].First.Geometry; }
        }

        /// <summary>
        /// Gets the capacity of the disk (in bytes).
        /// </summary>
        public override long Capacity
        {
            get { return _files[0].First.Capacity; }
        }

        /// <summary>
        /// Gets the content of the disk as a stream.
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

        private void ResolveFileChain(string lastPath)
        {
            DiskImageFile file = _files[_files.Count - 1].First;
            string filePath = lastPath;

            while (file.NeedsParent)
            {
                bool found = false;
                foreach (string testPath in file.GetParentLocations(filePath))
                {
                    if (File.Exists(testPath))
                    {
                        filePath = Path.GetFullPath(testPath);
                        file = new DiskImageFile(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read), Ownership.Dispose);
                        _files.Add(new Tuple<DiskImageFile, Ownership>(file, Ownership.Dispose));
                        found = true;
                    }
                }

                if (!found)
                {
                    throw new IOException(string.Format(CultureInfo.InvariantCulture, "Failed to find parent for disk '{0}'", filePath));
                }
            }
        }
    }
}
