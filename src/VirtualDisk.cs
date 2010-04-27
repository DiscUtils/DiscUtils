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
using System.Globalization;
using System.IO;
using DiscUtils.Partitions;

namespace DiscUtils
{
    /// <summary>
    /// Base class representing virtual hard disks.
    /// </summary>
    public abstract class VirtualDisk : IDisposable
    {
        private static Dictionary<string, VirtualDiskFactory> s_extensionMap;
        private static Dictionary<string, VirtualDiskFactory> s_typeMap;
        private static Dictionary<string, VirtualDiskTransport> s_diskTransports;

        private VirtualDiskTransport _transport;

        /// <summary>
        /// Destroys this instance.
        /// </summary>
        ~VirtualDisk()
        {
            Dispose(false);
        }

        /// <summary>
        /// Disposes of this instance, freeing underlying resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of underlying resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> if running inside Dispose(), indicating
        /// graceful cleanup of all managed objects should be performed, or <c>false</c>
        /// if running inside destructor.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_transport != null)
                {
                    _transport.Dispose();
                }
                _transport = null;
            }
        }

        /// <summary>
        /// Gets the geometry of the disk.
        /// </summary>
        public abstract Geometry Geometry
        {
            get;
        }

        /// <summary>
        /// Gets the geometry of the disk as it is anticipated a hypervisor BIOS will represent it.
        /// </summary>
        public virtual Geometry BiosGeometry
        {
            get { return Geometry.MakeBiosSafe(Geometry, Capacity); }
        }

        /// <summary>
        /// Gets the capacity of the disk (in bytes).
        /// </summary>
        public abstract long Capacity
        {
            get;
        }

        /// <summary>
        /// Gets the size of the disk's logical blocks (in bytes).
        /// </summary>
        public virtual int BlockSize
        {
            get { return Sizes.Sector; }
        }

        /// <summary>
        /// Gets the content of the disk as a stream.
        /// </summary>
        /// <remarks>Note the returned stream is not guaranteed to be at any particular position.  The actual position
        /// will depend on the last partition table/file system activity, since all access to the disk contents pass
        /// through a single stream instance.  Set the stream position before accessing the stream.</remarks>
        public abstract SparseStream Content
        {
            get;
        }

        /// <summary>
        /// Gets the layers that make up the disk.
        /// </summary>
        public abstract IEnumerable<VirtualDiskLayer> Layers
        {
            get;
        }

        /// <summary>
        /// Reads the first sector of the disk, known as the Master Boot Record.
        /// </summary>
        /// <returns>The MBR as a byte array</returns>
        public virtual byte[] GetMasterBootRecord()
        {
            byte[] sector = new byte[Sizes.Sector];

            long oldPos = Content.Position;
            Content.Position = 0;
            Utilities.ReadFully(Content, sector, 0, Sizes.Sector);
            Content.Position = oldPos;

            return sector;
        }

        /// <summary>
        /// Overwrites the first sector of the disk, known as the Master Boot Record.
        /// </summary>
        /// <param name="data">The master boot record, must be 512 bytes in length.</param>
        public virtual void SetMasterBootRecord(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }
            else if (data.Length != Sizes.Sector)
            {
                throw new ArgumentException("The Master Boot Record must be exactly 512 bytes in length", "data");
            }

            long oldPos = Content.Position;
            Content.Position = 0;
            Content.Write(data, 0, Sizes.Sector);
            Content.Position = oldPos;
        }

        /// <summary>
        /// Gets the Windows disk signature of the disk, which uniquely identifies the disk.
        /// </summary>
        public virtual int Signature
        {
            get
            {
                return Utilities.ToInt32LittleEndian(GetMasterBootRecord(), 0x01B8);
            }

            set
            {
                byte[] mbr = GetMasterBootRecord();
                Utilities.WriteBytesLittleEndian(value, mbr, 0x01B8);
                SetMasterBootRecord(mbr);
            }
        }

        /// <summary>
        /// Gets a best guess as to whether the disk has a valid partition table.
        /// </summary>
        /// <remarks>There is no reliable way to determine whether a disk has a valid partition
        /// table.  The 'guess' consists of checking for basic indicators and looking for obviously
        /// invalid data, such as overlapping partitions.</remarks>
        public virtual bool IsPartitioned
        {
            get { return BiosPartitionTable.IsValid(Content); }
        }

        /// <summary>
        /// Gets the object that interprets the partition structure.
        /// </summary>
        public virtual PartitionTable Partitions
        {
            get
            {
                BiosPartitionTable table = new BiosPartitionTable(this);
                if (table.Count == 1 && table[0].BiosType == BiosPartitionTypes.GptProtective)
                {
                    return new GuidPartitionTable(this);
                }
                else
                {
                    return table;
                }
            }
        }

        /// <summary>
        /// Create a new differencing disk, possibly within an existing disk.
        /// </summary>
        /// <param name="fileSystem">The file system to create the disk on</param>
        /// <param name="path">The path (or URI) for the disk to create</param>
        /// <returns>The newly created disk</returns>
        public abstract VirtualDisk CreateDifferencingDisk(DiscFileSystem fileSystem, string path);

        /// <summary>
        /// Create a new differencing disk.
        /// </summary>
        /// <param name="path">The path (or URI) for the disk to create</param>
        /// <returns>The newly created disk</returns>
        public abstract VirtualDisk CreateDifferencingDisk(string path);

        /// <summary>
        /// Gets the set of disk formats supported as an array of file extensions.
        /// </summary>
        public static ICollection<string> SupportedDiskFormats
        {
            get { return ExtensionMap.Keys; }
        }

        /// <summary>
        /// Gets the set of disk types supported, as an array of identifiers.
        /// </summary>
        public static ICollection<string> SupportedDiskTypes
        {
            get { return TypeMap.Keys; }
        }

        /// <summary>
        /// Gets the set of supported variants of a type of virtual disk.
        /// </summary>
        /// <param name="type">A type, as returned by <see cref="SupportedDiskTypes"/></param>
        /// <returns>A collection of identifiers, or empty if there is no variant concept for this type of disk.</returns>
        public static ICollection<string> GetSupportedDiskVariants(string type)
        {
            return TypeMap[type].Variants;
        }

        /// <summary>
        /// Create a new virtual disk, possibly within an existing disk.
        /// </summary>
        /// <param name="fileSystem">The file system to create the disk on</param>
        /// <param name="type">The type of disk to create (see <see cref="SupportedDiskTypes"/>)</param>
        /// <param name="variant">The variant of the type to create (see <see cref="GetSupportedDiskVariants"/>)</param>
        /// <param name="path">The path (or URI) for the disk to create</param>
        /// <param name="capacity">The capacity of the new disk</param>
        /// <param name="geometry">The geometry of the new disk (or null).</param>
        /// <param name="parameters">Untyped parameters controlling the creation process (TBD)</param>
        /// <returns>The newly created disk</returns>
        public static VirtualDisk CreateDisk(DiscFileSystem fileSystem, string type, string variant, string path, long capacity, Geometry geometry, Dictionary<string, string> parameters)
        {
            VirtualDiskFactory factory = TypeMap[type];
            return factory.CreateDisk(new DiscFileLocator(fileSystem, Utilities.GetDirectoryFromPath(path)), variant.ToLowerInvariant(), Utilities.GetFileFromPath(path), capacity, geometry, parameters ?? new Dictionary<string, string>());
        }

        /// <summary>
        /// Create a new virtual disk.
        /// </summary>
        /// <param name="type">The type of disk to create (see <see cref="SupportedDiskTypes"/>)</param>
        /// <param name="variant">The variant of the type to create (see <see cref="GetSupportedDiskVariants"/>)</param>
        /// <param name="path">The path (or URI) for the disk to create</param>
        /// <param name="capacity">The capacity of the new disk</param>
        /// <param name="geometry">The geometry of the new disk (or null).</param>
        /// <param name="parameters">Untyped parameters controlling the creation process (TBD)</param>
        /// <returns>The newly created disk</returns>
        public static VirtualDisk CreateDisk(string type, string variant, string path, long capacity, Geometry geometry, Dictionary<string, string> parameters)
        {
            return CreateDisk(type, variant, path, capacity, geometry, null, null, parameters);
        }

        /// <summary>
        /// Create a new virtual disk.
        /// </summary>
        /// <param name="type">The type of disk to create (see <see cref="SupportedDiskTypes"/>)</param>
        /// <param name="variant">The variant of the type to create (see <see cref="GetSupportedDiskVariants"/>)</param>
        /// <param name="path">The path (or URI) for the disk to create</param>
        /// <param name="capacity">The capacity of the new disk</param>
        /// <param name="geometry">The geometry of the new disk (or null).</param>
        /// <param name="user">The user identity to use when accessing the <c>path</c> (or null)</param>
        /// <param name="password">The password to use when accessing the <c>path</c> (or null)</param>
        /// <param name="parameters">Untyped parameters controlling the creation process (TBD)</param>
        /// <returns>The newly created disk</returns>
        public static VirtualDisk CreateDisk(string type, string variant, string path, long capacity, Geometry geometry, string user, string password, Dictionary<string, string> parameters)
        {
            Uri uri = PathToUri(path);
            VirtualDisk result = null;

            VirtualDiskTransport transport;
            if (!DiskTransports.TryGetValue(uri.Scheme.ToUpperInvariant(), out transport))
            {
                throw new FileNotFoundException(string.Format(CultureInfo.InvariantCulture, "Unable to parse path '{0}'", path), path);
            }

            try
            {
                transport.Connect(uri, user, password);

                if (transport.IsRawDisk)
                {
                    result = transport.OpenDisk(FileAccess.ReadWrite);
                }
                else
                {
                    VirtualDiskFactory factory = TypeMap[type];

                    result = factory.CreateDisk(transport.GetFileLocator(), variant.ToLowerInvariant(), Utilities.GetFileFromPath(path), capacity, geometry, parameters ?? new Dictionary<string, string>());
                }

                if (result != null)
                {
                    result._transport = transport;
                    transport = null;
                }

                return result;
            }
            finally
            {
                if (transport != null)
                {
                    transport.Dispose();
                }
            }
        }

        /// <summary>
        /// Opens an existing virtual disk.
        /// </summary>
        /// <param name="path">The path of the virtual disk to open, can be a URI</param>
        /// <param name="access">The desired access to the disk</param>
        /// <returns>The Virtual Disk, or <c>null</c> if an unknown disk format</returns>
        public static VirtualDisk OpenDisk(string path, FileAccess access)
        {
            return OpenDisk(path, access, null, null);
        }

        /// <summary>
        /// Opens an existing virtual disk.
        /// </summary>
        /// <param name="path">The path of the virtual disk to open, can be a URI</param>
        /// <param name="access">The desired access to the disk</param>
        /// <param name="user">The user name to use for authentication (if necessary)</param>
        /// <param name="password">The password to use for authentication (if necessary)</param>
        /// <returns>The Virtual Disk, or <c>null</c> if an unknown disk format</returns>
        public static VirtualDisk OpenDisk(string path, FileAccess access, string user, string password)
        {
            Uri uri = PathToUri(path);
            VirtualDisk result = null;

            VirtualDiskTransport transport;
            if (!DiskTransports.TryGetValue(uri.Scheme.ToUpperInvariant(), out transport))
            {
                throw new FileNotFoundException(string.Format(CultureInfo.InvariantCulture, "Unable to parse path '{0}'", path), path);
            }

            try
            {
                transport.Connect(uri, user, password);

                if (transport.IsRawDisk)
                {
                    result = transport.OpenDisk(access);
                }
                else
                {
                    string extension = Path.GetExtension(uri.AbsolutePath).ToUpperInvariant();
                    if (extension.StartsWith(".", StringComparison.Ordinal))
                    {
                        extension = extension.Substring(1);
                    }

                    VirtualDiskFactory factory;
                    if (ExtensionMap.TryGetValue(extension, out factory))
                    {
                        result = factory.OpenDisk(transport.GetFileLocator(), transport.GetFileName(), access);
                    }
                }

                if (result != null)
                {
                    result._transport = transport;
                    transport = null;
                }

                return result;
            }
            finally
            {
                if (transport != null)
                {
                    transport.Dispose();
                }
            }

        }

        /// <summary>
        /// Opens an existing virtual disk, possibly from within an existing disk.
        /// </summary>
        /// <param name="fs">The file system to open the disk on</param>
        /// <param name="path">The path of the virtual disk to open</param>
        /// <param name="access">The desired access to the disk</param>
        /// <returns>The Virtual Disk, or <c>null</c> if an unknown disk format</returns>
        public static VirtualDisk OpenDisk(DiscFileSystem fs, string path, FileAccess access)
        {
            if (fs == null)
            {
                return OpenDisk(path, access);
            }

            string extension = Path.GetExtension(path).ToUpperInvariant();
            if (extension.StartsWith(".", StringComparison.Ordinal))
            {
                extension = extension.Substring(1);
            }

            VirtualDiskFactory factory;
            if (ExtensionMap.TryGetValue(extension, out factory))
            {
                return factory.OpenDisk(fs, path, access);
            }

            return null;
        }

        private static Uri PathToUri(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Path must not be null or empty", "path");
            }

            if (path.IndexOf(':') < 0 && !path.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase))
            {
                path = Path.GetFullPath(path);
            }

            return new Uri(path);
        }

        private static Dictionary<string, VirtualDiskFactory> ExtensionMap
        {
            get
            {
                if (s_extensionMap == null)
                {
                    InitializeMaps();
                }

                return s_extensionMap;
            }
        }

        private static Dictionary<string, VirtualDiskFactory> TypeMap
        {
            get
            {
                if (s_typeMap == null)
                {
                    InitializeMaps();
                }

                return s_typeMap;
            }
        }

        private static void InitializeMaps()
        {
            Dictionary<string, VirtualDiskFactory> typeMap = new Dictionary<string, VirtualDiskFactory>();
            Dictionary<string, VirtualDiskFactory> extensionMap = new Dictionary<string, VirtualDiskFactory>();

            foreach (var type in typeof(VirtualDisk).Assembly.GetTypes())
            {
                VirtualDiskFactoryAttribute attr = (VirtualDiskFactoryAttribute)Attribute.GetCustomAttribute(type, typeof(VirtualDiskFactoryAttribute), false);
                if (attr != null)
                {
                    VirtualDiskFactory factory = (VirtualDiskFactory)Activator.CreateInstance(type);
                    typeMap.Add(attr.Type, factory);
                    foreach (var extension in attr.FileExtensions)
                    {
                        extensionMap.Add(extension.ToUpperInvariant(), factory);
                    }
                }
            }

            s_typeMap = typeMap;
            s_extensionMap = extensionMap;
        }

        private static Dictionary<string, VirtualDiskTransport> DiskTransports
        {
            get
            {
                if (s_diskTransports == null)
                {
                    Dictionary<string, VirtualDiskTransport> transports = new Dictionary<string, VirtualDiskTransport>();

                    foreach (var type in typeof(VirtualDisk).Assembly.GetTypes())
                    {
                        foreach (VirtualDiskTransportAttribute attr in Attribute.GetCustomAttributes(type, typeof(VirtualDiskTransportAttribute), false))
                        {
                            transports.Add(attr.Scheme.ToUpperInvariant(), (VirtualDiskTransport)Activator.CreateInstance(type));
                        }
                    }

                    s_diskTransports = transports;
                }

                return s_diskTransports;
            }
        }
    }
}
