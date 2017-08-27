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
using DiscUtils.Partitions;
using DiscUtils.Streams;

namespace DiscUtils
{
    /// <summary>
    /// Base class representing virtual hard disks.
    /// </summary>
    public abstract class VirtualDisk :
#if !NETCORE
        MarshalByRefObject, 
#endif
        IDisposable
    {
        private VirtualDiskTransport _transport;

        /// <summary>
        /// Finalizes an instance of the VirtualDisk class.
        /// </summary>
        ~VirtualDisk()
        {
            Dispose(false);
        }

        /// <summary>
        /// Gets the set of disk formats supported as an array of file extensions.
        /// </summary>
        [Obsolete("Use VirtualDiskManager.SupportedDiskFormats")]
        public static ICollection<string> SupportedDiskFormats
        {
            get { return VirtualDiskManager.SupportedDiskFormats; }
        }

        /// <summary>
        /// Gets the set of disk types supported, as an array of identifiers.
        /// </summary>
        [Obsolete("Use VirtualDiskManager.SupportedDiskTypes")]
        public static ICollection<string> SupportedDiskTypes
        {
            get { return VirtualDiskManager.SupportedDiskTypes; }
        }

        /// <summary>
        /// Gets the geometry of the disk.
        /// </summary>
        public abstract Geometry Geometry { get; }

        /// <summary>
        /// Gets the geometry of the disk as it is anticipated a hypervisor BIOS will represent it.
        /// </summary>
        public virtual Geometry BiosGeometry
        {
            get { return Geometry.MakeBiosSafe(Geometry, Capacity); }
        }

        /// <summary>
        /// Gets the type of disk represented by this object.
        /// </summary>
        public abstract VirtualDiskClass DiskClass { get; }

        /// <summary>
        /// Gets the capacity of the disk (in bytes).
        /// </summary>
        public abstract long Capacity { get; }

        /// <summary>
        /// Gets the size of the disk's logical blocks (aka sector size), in bytes.
        /// </summary>
        public virtual int BlockSize
        {
            get { return Sizes.Sector; }
        }

        /// <summary>
        /// Gets the logical sector size of the disk, in bytes.
        /// </summary>
        /// <remarks>This is an alias for the <c>BlockSize</c> property.</remarks>
        public int SectorSize
        {
            get { return BlockSize; }
        }

        /// <summary>
        /// Gets the content of the disk as a stream.
        /// </summary>
        /// <remarks>Note the returned stream is not guaranteed to be at any particular position.  The actual position
        /// will depend on the last partition table/file system activity, since all access to the disk contents pass
        /// through a single stream instance.  Set the stream position before accessing the stream.</remarks>
        public abstract SparseStream Content { get; }

        /// <summary>
        /// Gets the layers that make up the disk.
        /// </summary>
        public abstract IEnumerable<VirtualDiskLayer> Layers { get; }

        /// <summary>
        /// Gets or sets the Windows disk signature of the disk, which uniquely identifies the disk.
        /// </summary>
        public virtual int Signature
        {
            get { return EndianUtilities.ToInt32LittleEndian(GetMasterBootRecord(), 0x01B8); }

            set
            {
                byte[] mbr = GetMasterBootRecord();
                EndianUtilities.WriteBytesLittleEndian(value, mbr, 0x01B8);
                SetMasterBootRecord(mbr);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the disk appears to have a valid partition table.
        /// </summary>
        /// <remarks>There is no reliable way to determine whether a disk has a valid partition
        /// table.  The 'guess' consists of checking for basic indicators and looking for obviously
        /// invalid data, such as overlapping partitions.</remarks>
        public virtual bool IsPartitioned
        {
            get { return PartitionTable.IsPartitioned(Content); }
        }

        /// <summary>
        /// Gets the object that interprets the partition structure.
        /// </summary>
        /// <remarks>It is theoretically possible for a disk to contain two independent partition structures - a
        /// BIOS/GPT one and an Apple one, for example.  This method will return in order of preference,
        /// a GUID partition table, a BIOS partition table, then in undefined preference one of any other partition
        /// tables found.  See PartitionTable.GetPartitionTables to gain access to all the discovered partition
        /// tables on a disk.</remarks>
        public virtual PartitionTable Partitions
        {
            get
            {
                IList<PartitionTable> tables = PartitionTable.GetPartitionTables(this);
                if (tables == null || tables.Count == 0)
                {
                    return null;
                }
                if (tables.Count == 1)
                {
                    return tables[0];
                }
                PartitionTable best = null;
                int bestScore = -1;
                for (int i = 0; i < tables.Count; ++i)
                {
                    int newScore = 0;
                    if (tables[i] is GuidPartitionTable)
                    {
                        newScore = 2;
                    }
                    else if (tables[i] is BiosPartitionTable)
                    {
                        newScore = 1;
                    }

                    if (newScore > bestScore)
                    {
                        bestScore = newScore;
                        best = tables[i];
                    }
                }

                return best;
            }
        }

        /// <summary>
        /// Gets the parameters of the disk.
        /// </summary>
        /// <remarks>Most of the parameters are also available individually, such as DiskType and Capacity.</remarks>
        public virtual VirtualDiskParameters Parameters
        {
            get
            {
                return new VirtualDiskParameters
                {
                    DiskType = DiskClass,
                    Capacity = Capacity,
                    Geometry = Geometry,
                    BiosGeometry = BiosGeometry,
                    AdapterType = GenericDiskAdapterType.Ide
                };
            }
        }

        /// <summary>
        /// Gets information about the type of disk.
        /// </summary>
        /// <remarks>This property provides access to meta-data about the disk format, for example whether the
        /// BIOS geometry is preserved in the disk file.</remarks>
        public abstract VirtualDiskTypeInfo DiskTypeInfo { get; }

        /// <summary>
        /// Gets the set of supported variants of a type of virtual disk.
        /// </summary>
        /// <param name="type">A type, as returned by <see cref="SupportedDiskTypes"/>.</param>
        /// <returns>A collection of identifiers, or empty if there is no variant concept for this type of disk.</returns>
        public static ICollection<string> GetSupportedDiskVariants(string type)
        {
            return VirtualDiskManager.TypeMap[type].Variants;
        }

        /// <summary>
        /// Gets information about disk type.
        /// </summary>
        /// <param name="type">The disk type, as returned by <see cref="SupportedDiskTypes"/>.</param>
        /// <param name="variant">The variant of the disk type.</param>
        /// <returns>Information about the disk type.</returns>
        public static VirtualDiskTypeInfo GetDiskType(string type, string variant)
        {
            return VirtualDiskManager.TypeMap[type].GetDiskTypeInformation(variant);
        }

        /// <summary>
        /// Create a new virtual disk, possibly within an existing disk.
        /// </summary>
        /// <param name="fileSystem">The file system to create the disk on.</param>
        /// <param name="type">The type of disk to create (see <see cref="SupportedDiskTypes"/>).</param>
        /// <param name="variant">The variant of the type to create (see <see cref="GetSupportedDiskVariants"/>).</param>
        /// <param name="path">The path (or URI) for the disk to create.</param>
        /// <param name="capacity">The capacity of the new disk.</param>
        /// <param name="geometry">The geometry of the new disk (or null).</param>
        /// <param name="parameters">Untyped parameters controlling the creation process (TBD).</param>
        /// <returns>The newly created disk.</returns>
        public static VirtualDisk CreateDisk(DiscFileSystem fileSystem, string type, string variant, string path, long capacity, Geometry geometry, Dictionary<string, string> parameters)
        {
            VirtualDiskFactory factory = VirtualDiskManager.TypeMap[type];

            VirtualDiskParameters diskParams = new VirtualDiskParameters
            {
                AdapterType = GenericDiskAdapterType.Scsi,
                Capacity = capacity,
                Geometry = geometry
            };

            if (parameters != null)
            {
                foreach (string key in parameters.Keys)
                {
                    diskParams.ExtendedParameters[key] = parameters[key];
                }
            }

            return factory.CreateDisk(new DiscFileLocator(fileSystem, Utilities.GetDirectoryFromPath(path)), variant.ToLowerInvariant(), Utilities.GetFileFromPath(path), diskParams);
        }

        /// <summary>
        /// Create a new virtual disk.
        /// </summary>
        /// <param name="type">The type of disk to create (see <see cref="SupportedDiskTypes"/>).</param>
        /// <param name="variant">The variant of the type to create (see <see cref="GetSupportedDiskVariants"/>).</param>
        /// <param name="path">The path (or URI) for the disk to create.</param>
        /// <param name="capacity">The capacity of the new disk.</param>
        /// <param name="geometry">The geometry of the new disk (or null).</param>
        /// <param name="parameters">Untyped parameters controlling the creation process (TBD).</param>
        /// <returns>The newly created disk.</returns>
        public static VirtualDisk CreateDisk(string type, string variant, string path, long capacity, Geometry geometry, Dictionary<string, string> parameters)
        {
            return CreateDisk(type, variant, path, capacity, geometry, null, null, parameters);
        }

        /// <summary>
        /// Create a new virtual disk.
        /// </summary>
        /// <param name="type">The type of disk to create (see <see cref="SupportedDiskTypes"/>).</param>
        /// <param name="variant">The variant of the type to create (see <see cref="GetSupportedDiskVariants"/>).</param>
        /// <param name="path">The path (or URI) for the disk to create.</param>
        /// <param name="capacity">The capacity of the new disk.</param>
        /// <param name="geometry">The geometry of the new disk (or null).</param>
        /// <param name="user">The user identity to use when accessing the <c>path</c> (or null).</param>
        /// <param name="password">The password to use when accessing the <c>path</c> (or null).</param>
        /// <param name="parameters">Untyped parameters controlling the creation process (TBD).</param>
        /// <returns>The newly created disk.</returns>
        public static VirtualDisk CreateDisk(string type, string variant, string path, long capacity, Geometry geometry, string user, string password, Dictionary<string, string> parameters)
        {
            VirtualDiskParameters diskParams = new VirtualDiskParameters
            {
                AdapterType = GenericDiskAdapterType.Scsi,
                Capacity = capacity,
                Geometry = geometry
            };

            if (parameters != null)
            {
                foreach (string key in parameters.Keys)
                {
                    diskParams.ExtendedParameters[key] = parameters[key];
                }
            }

            return CreateDisk(type, variant, path, diskParams, user, password);
        }

        /// <summary>
        /// Create a new virtual disk.
        /// </summary>
        /// <param name="type">The type of disk to create (see <see cref="SupportedDiskTypes"/>).</param>
        /// <param name="variant">The variant of the type to create (see <see cref="GetSupportedDiskVariants"/>).</param>
        /// <param name="path">The path (or URI) for the disk to create.</param>
        /// <param name="diskParameters">Parameters controlling the capacity, geometry, etc of the new disk.</param>
        /// <param name="user">The user identity to use when accessing the <c>path</c> (or null).</param>
        /// <param name="password">The password to use when accessing the <c>path</c> (or null).</param>
        /// <returns>The newly created disk.</returns>
        public static VirtualDisk CreateDisk(string type, string variant, string path, VirtualDiskParameters diskParameters, string user, string password)
        {
            Uri uri = PathToUri(path);
            VirtualDisk result = null;

            Type transportType;
            if (!VirtualDiskManager.DiskTransports.TryGetValue(uri.Scheme.ToUpperInvariant(), out transportType))
            {
                throw new FileNotFoundException(string.Format(CultureInfo.InvariantCulture, "Unable to parse path '{0}'", path), path);
            }

            VirtualDiskTransport transport = (VirtualDiskTransport)Activator.CreateInstance(transportType);

            try
            {
                transport.Connect(uri, user, password);

                if (transport.IsRawDisk)
                {
                    result = transport.OpenDisk(FileAccess.ReadWrite);
                }
                else
                {
                    VirtualDiskFactory factory = VirtualDiskManager.TypeMap[type];

                    result = factory.CreateDisk(transport.GetFileLocator(), variant.ToLowerInvariant(), Utilities.GetFileFromPath(path), diskParameters);
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
        /// <param name="path">The path of the virtual disk to open, can be a URI.</param>
        /// <param name="access">The desired access to the disk.</param>
        /// <returns>The Virtual Disk, or <c>null</c> if an unknown disk format.</returns>
        public static VirtualDisk OpenDisk(string path, FileAccess access)
        {
            return OpenDisk(path, null, access, null, null);
        }

        /// <summary>
        /// Opens an existing virtual disk.
        /// </summary>
        /// <param name="path">The path of the virtual disk to open, can be a URI.</param>
        /// <param name="access">The desired access to the disk.</param>
        /// <param name="user">The user name to use for authentication (if necessary).</param>
        /// <param name="password">The password to use for authentication (if necessary).</param>
        /// <returns>The Virtual Disk, or <c>null</c> if an unknown disk format.</returns>
        public static VirtualDisk OpenDisk(string path, FileAccess access, string user, string password)
        {
            return OpenDisk(path, null, access, user, password);
        }

        /// <summary>
        /// Opens an existing virtual disk.
        /// </summary>
        /// <param name="path">The path of the virtual disk to open, can be a URI.</param>
        /// <param name="forceType">Force the detected disk type (<c>null</c> to detect).</param>
        /// <param name="access">The desired access to the disk.</param>
        /// <param name="user">The user name to use for authentication (if necessary).</param>
        /// <param name="password">The password to use for authentication (if necessary).</param>
        /// <returns>The Virtual Disk, or <c>null</c> if an unknown disk format.</returns>
        /// <remarks>
        /// The detected disk type can be forced by specifying a known disk type: 
        /// RAW, VHD, VMDK, etc.
        /// </remarks>
        public static VirtualDisk OpenDisk(string path, string forceType, FileAccess access, string user, string password)
        {
            Uri uri = PathToUri(path);
            VirtualDisk result = null;

            Type transportType;
            if (!VirtualDiskManager.DiskTransports.TryGetValue(uri.Scheme.ToUpperInvariant(), out transportType))
            {
                throw new FileNotFoundException(string.Format(CultureInfo.InvariantCulture, "Unable to parse path '{0}'", path), path);
            }

            VirtualDiskTransport transport = (VirtualDiskTransport)Activator.CreateInstance(transportType);

            try
            {
                transport.Connect(uri, user, password);

                if (transport.IsRawDisk)
                {
                    result = transport.OpenDisk(access);
                }
                else
                {
                    bool foundFactory;
                    VirtualDiskFactory factory;

                    if (!string.IsNullOrEmpty(forceType))
                    {
                        foundFactory = VirtualDiskManager.TypeMap.TryGetValue(forceType, out factory);
                    }
                    else
                    {
                        string extension = Path.GetExtension(uri.AbsolutePath).ToUpperInvariant();
                        if (extension.StartsWith(".", StringComparison.Ordinal))
                        {
                            extension = extension.Substring(1);
                        }

                        foundFactory = VirtualDiskManager.ExtensionMap.TryGetValue(extension, out factory);
                    }

                    if (foundFactory)
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
        /// <param name="fs">The file system to open the disk on.</param>
        /// <param name="path">The path of the virtual disk to open.</param>
        /// <param name="access">The desired access to the disk.</param>
        /// <returns>The Virtual Disk, or <c>null</c> if an unknown disk format.</returns>
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
            if (VirtualDiskManager.ExtensionMap.TryGetValue(extension, out factory))
            {
                return factory.OpenDisk(fs, path, access);
            }

            return null;
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
        /// Reads the first sector of the disk, known as the Master Boot Record.
        /// </summary>
        /// <returns>The MBR as a byte array.</returns>
        public virtual byte[] GetMasterBootRecord()
        {
            byte[] sector = new byte[Sizes.Sector];

            long oldPos = Content.Position;
            Content.Position = 0;
            StreamUtilities.ReadExact(Content, sector, 0, Sizes.Sector);
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
                throw new ArgumentNullException(nameof(data));
            }
            if (data.Length != Sizes.Sector)
            {
                throw new ArgumentException("The Master Boot Record must be exactly 512 bytes in length", nameof(data));
            }

            long oldPos = Content.Position;
            Content.Position = 0;
            Content.Write(data, 0, Sizes.Sector);
            Content.Position = oldPos;
        }

        /// <summary>
        /// Create a new differencing disk, possibly within an existing disk.
        /// </summary>
        /// <param name="fileSystem">The file system to create the disk on.</param>
        /// <param name="path">The path (or URI) for the disk to create.</param>
        /// <returns>The newly created disk.</returns>
        public abstract VirtualDisk CreateDifferencingDisk(DiscFileSystem fileSystem, string path);

        /// <summary>
        /// Create a new differencing disk.
        /// </summary>
        /// <param name="path">The path (or URI) for the disk to create.</param>
        /// <returns>The newly created disk.</returns>
        public abstract VirtualDisk CreateDifferencingDisk(string path);

        internal static VirtualDiskLayer OpenDiskLayer(FileLocator locator, string path, FileAccess access)
        {
            string extension = Path.GetExtension(path).ToUpperInvariant();
            if (extension.StartsWith(".", StringComparison.Ordinal))
            {
                extension = extension.Substring(1);
            }

            VirtualDiskFactory factory;
            if (VirtualDiskManager.ExtensionMap.TryGetValue(extension, out factory))
            {
                return factory.OpenDiskLayer(locator, path, access);
            }

            return null;
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

        private static Uri PathToUri(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Path must not be null or empty", nameof(path));
            }

            if (path.Contains("://"))
            {
                return new Uri(path);
            }

            if (!Path.IsPathRooted(path))
            {
                path = Path.GetFullPath(path);
            }

            // Built-in Uri class does cope well with query params on file Uris, so do some
            // parsing ourselves...
            if (path.Length >= 1 && path[0] == '\\')
            {
                UriBuilder builder = new UriBuilder("file:" + path.Replace('\\', '/'));
                return builder.Uri;
            }
            if (path.StartsWith("//", StringComparison.OrdinalIgnoreCase))
            {
                UriBuilder builder = new UriBuilder("file:" + path);
                return builder.Uri;
            }
            if (path.Length >= 2 && path[1] == ':')
            {
                UriBuilder builder = new UriBuilder("file:///" + path.Replace('\\', '/'));
                return builder.Uri;
            }
            return new Uri(path);
        }
    }
}