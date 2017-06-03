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
using DiscUtils.Streams;

namespace DiscUtils.Vmdk
{
    /// <summary>
    /// Creates new VMDK disks by wrapping existing streams.
    /// </summary>
    /// <remarks>Using this method for creating virtual disks avoids consuming
    /// large amounts of memory, or going via the local file system when the aim
    /// is simply to present a VMDK version of an existing disk.</remarks>
    public sealed class DiskBuilder : DiskImageBuilder
    {
        /// <summary>
        /// Initializes a new instance of the DiskBuilder class.
        /// </summary>
        public DiskBuilder()
        {
            DiskType = DiskCreateType.Vmfs;
            AdapterType = DiskAdapterType.LsiLogicScsi;
        }

        /// <summary>
        /// Gets or sets the specific VMware disk adapter type to embed in the VMDK.
        /// </summary>
        public DiskAdapterType AdapterType { get; set; }

        /// <summary>
        /// Gets or sets the type of VMDK disk file required.
        /// </summary>
        public DiskCreateType DiskType { get; set; }

        /// <summary>
        /// Gets or sets the adaptor type for created virtual disk, setting to SCSI implies LSI logic adapter.
        /// </summary>
        public override GenericDiskAdapterType GenericAdapterType
        {
            get { return AdapterType == DiskAdapterType.Ide ? GenericDiskAdapterType.Ide : GenericDiskAdapterType.Scsi; }

            set
            {
                if (value == GenericDiskAdapterType.Ide)
                {
                    AdapterType = DiskAdapterType.Ide;
                }
                else if (AdapterType == DiskAdapterType.Ide)
                {
                    AdapterType = DiskAdapterType.LsiLogicScsi;
                }
            }
        }

        /// <summary>
        /// Gets whether this file format preserves BIOS geometry information.
        /// </summary>
        public override bool PreservesBiosGeometry
        {
            get { return true; }
        }

        /// <summary>
        /// Initiates the build process.
        /// </summary>
        /// <param name="baseName">The base name for the VMDK, for example 'foo' to create 'foo.vmdk'.</param>
        /// <returns>A set of one or more logical files that constitute the VMDK.  The first file is
        /// the 'primary' file that is normally attached to VMs.</returns>
        public override DiskImageFileSpecification[] Build(string baseName)
        {
            if (string.IsNullOrEmpty(baseName))
            {
                throw new ArgumentException("Invalid base file name", nameof(baseName));
            }

            if (Content == null)
            {
                throw new InvalidOperationException("No content stream specified");
            }

            if (DiskType != DiskCreateType.Vmfs && DiskType != DiskCreateType.VmfsSparse &&
                DiskType != DiskCreateType.MonolithicSparse)
            {
                throw new NotImplementedException("Only MonolithicSparse, Vmfs and VmfsSparse disks implemented");
            }

            List<DiskImageFileSpecification> fileSpecs = new List<DiskImageFileSpecification>();

            Geometry geometry = Geometry ?? DiskImageFile.DefaultGeometry(Content.Length);
            Geometry biosGeometry = BiosGeometry ?? Geometry.LbaAssistedBiosGeometry(Content.Length);

            DescriptorFile baseDescriptor = DiskImageFile.CreateSimpleDiskDescriptor(geometry, biosGeometry, DiskType,
                AdapterType);

            if (DiskType == DiskCreateType.Vmfs)
            {
                ExtentDescriptor extent = new ExtentDescriptor(ExtentAccess.ReadWrite, Content.Length / 512,
                    ExtentType.Vmfs, baseName + "-flat.vmdk", 0);
                baseDescriptor.Extents.Add(extent);

                MemoryStream ms = new MemoryStream();
                baseDescriptor.Write(ms);

                fileSpecs.Add(new DiskImageFileSpecification(baseName + ".vmdk", new PassthroughStreamBuilder(ms)));
                fileSpecs.Add(new DiskImageFileSpecification(baseName + "-flat.vmdk",
                    new PassthroughStreamBuilder(Content)));
            }
            else if (DiskType == DiskCreateType.VmfsSparse)
            {
                ExtentDescriptor extent = new ExtentDescriptor(ExtentAccess.ReadWrite, Content.Length / 512,
                    ExtentType.VmfsSparse, baseName + "-sparse.vmdk", 0);
                baseDescriptor.Extents.Add(extent);

                MemoryStream ms = new MemoryStream();
                baseDescriptor.Write(ms);

                fileSpecs.Add(new DiskImageFileSpecification(baseName + ".vmdk", new PassthroughStreamBuilder(ms)));
                fileSpecs.Add(new DiskImageFileSpecification(baseName + "-sparse.vmdk",
                    new VmfsSparseExtentBuilder(Content)));
            }
            else if (DiskType == DiskCreateType.MonolithicSparse)
            {
                ExtentDescriptor extent = new ExtentDescriptor(ExtentAccess.ReadWrite, Content.Length / 512,
                    ExtentType.Sparse, baseName + ".vmdk", 0);
                baseDescriptor.Extents.Add(extent);
                fileSpecs.Add(new DiskImageFileSpecification(baseName + ".vmdk",
                    new MonolithicSparseExtentBuilder(Content, baseDescriptor)));
            }

            return fileSpecs.ToArray();
        }
    }
}