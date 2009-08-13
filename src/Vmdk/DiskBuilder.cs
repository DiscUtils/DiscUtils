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
using System.IO;

namespace DiscUtils.Vmdk
{
    /// <summary>
    /// Creates new VMDK disks by wrapping existing streams.
    /// </summary>
    /// <remarks>Using this method for creating virtual disks avoids consuming
    /// large amounts of memory, or going via the local file system when the aim
    /// is simply to present a VMDK version of an existing disk.</remarks>
    public class DiskBuilder
    {
        private Stream _content;
        private Geometry _diskGeometry;
        private DiskCreateType _diskType;
        private DiskAdapterType _adapterType;

        private static Random _rng = new Random();

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public DiskBuilder()
        {
            _diskType = DiskCreateType.Vmfs;
            _adapterType = DiskAdapterType.LsiLogicScsi;
        }

        /// <summary>
        /// Sets the content for this disk, implying the size of the disk.
        /// </summary>
        public Stream Content
        {
            set { _content = value; }
        }

        /// <summary>
        /// Sets the geometry of this disk, will be implied from the content stream if not set.
        /// </summary>
        public Geometry DiskGeometry
        {
            set { _diskGeometry = value; }
        }

        /// <summary>
        /// Sets the type of VMDK disk file required.
        /// </summary>
        public DiskCreateType DiskType
        {
            set { _diskType = value;}
        }

        /// <summary>
        /// Sets the disk adapter type to embed in the VMDK.
        /// </summary>
        public DiskAdapterType AdapterType
        {
            set { _adapterType = value; }
        }

        /// <summary>
        /// Initiates the build process.
        /// </summary>
        /// <param name="baseName">The base name for the VMDK, for example 'foo' to create 'foo.vmdk'.</param>
        /// <returns>An array of objects describing the logical files that comprise the VMDK.</returns>
        public FileSpecification[] Build(string baseName)
        {
            if (string.IsNullOrEmpty(baseName))
            {
                throw new ArgumentException("Invalid base file name", "baseName");
            }

            if (_content == null)
            {
                throw new InvalidOperationException("No content stream specified");
            }

            if (_diskType != DiskCreateType.Vmfs)
            {
                throw new NotImplementedException("Only flat VMFS disks implemented");
            }

            FileSpecification[] fileSpecs = new FileSpecification[2];

            Geometry geometry = _diskGeometry ?? Geometry.FromCapacity(_content.Length);


            ExtentDescriptor extent = new ExtentDescriptor(ExtentAccess.ReadWrite, _content.Length / 512, ExtentType.Vmfs, baseName + "-flat.vmdk", 0);
            DescriptorFile baseDescriptor = new DescriptorFile();
            baseDescriptor.DiskGeometry = geometry;
            baseDescriptor.ContentId = (uint)_rng.Next();
            baseDescriptor.CreateType = _diskType;
            baseDescriptor.UniqueId = Guid.NewGuid();
            baseDescriptor.HardwareVersion = "4";
            baseDescriptor.AdapterType = _adapterType;
            baseDescriptor.Extents.Add(extent);

            MemoryStream ms = new MemoryStream();
            baseDescriptor.Write(ms);

            fileSpecs[0] = new FileSpecification(baseName + ".vmdk", new PassthroughStreamBuilder(ms));
            fileSpecs[1] = new FileSpecification(baseName + "-flat.vmdk", new PassthroughStreamBuilder(_content));

            return fileSpecs;
        }
    }
}
