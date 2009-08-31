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
using System.IO;
using DiscUtils.Partitions;

namespace DiscUtils
{
    /// <summary>
    /// Base class representing virtual hard disks.
    /// </summary>
    public abstract class VirtualDisk : IDisposable
    {
        private static Dictionary<string, VirtualDiskFactory> s_diskFactories;

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
        }

        /// <summary>
        /// Gets the geometry of the disk.
        /// </summary>
        public abstract Geometry Geometry
        {
            get;
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
        /// Gets the set of disk formats supported as an array of file extensions.
        /// </summary>
        public static ICollection<string> SupportedDiskFormats
        {
            get { return DiskFactories.Keys; }
        }

        /// <summary>
        /// Opens an existing virtual disk.
        /// </summary>
        /// <param name="path">The path of the virtual disk to open</param>
        /// <param name="access">The desired access to the disk</param>
        /// <returns>The Virtual Disk, or <c>null</c> if an unknown disk format</returns>
        public static VirtualDisk OpenDisk(string path, FileAccess access)
        {
            string extension = Path.GetExtension(path).ToUpperInvariant();
            if (extension.StartsWith(".", StringComparison.Ordinal))
            {
                extension = extension.Substring(1);
            }

            VirtualDiskFactory factory;
            if (DiskFactories.TryGetValue(extension, out factory))
            {
                return factory.OpenDisk(path, access);
            }

            return null;
        }

        private static Dictionary<string, VirtualDiskFactory> DiskFactories
        {
            get
            {
                if (s_diskFactories == null)
                {
                    Dictionary<string, VirtualDiskFactory> factories = new Dictionary<string, VirtualDiskFactory>();

                    foreach (var type in typeof(VirtualDisk).Assembly.GetTypes())
                    {
                        foreach (VirtualDiskFactoryAttribute attr in Attribute.GetCustomAttributes(type, typeof(VirtualDiskFactoryAttribute), false))
                        {
                            factories.Add(attr.FileExtension.ToUpperInvariant(), (VirtualDiskFactory)Activator.CreateInstance(type));
                        }
                    }

                    s_diskFactories = factories;
                }

                return s_diskFactories;
            }
        }
    }
}
