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
using DiscUtils.Iso9660;
using DiscUtils.Streams;
using DiscUtils.Vfs;

namespace DiscUtils.Udf
{
    /// <summary>
    /// Class for accessing OSTA Universal Disk Format file systems.
    /// </summary>
    public sealed class UdfReader : VfsFileSystemFacade
    {
        /// <summary>
        /// Initializes a new instance of the UdfReader class.
        /// </summary>
        /// <param name="data">The stream containing the UDF file system.</param>
        public UdfReader(Stream data)
            : base(new VfsUdfReader(data)) {}

        /// <summary>
        /// Initializes a new instance of the UdfReader class.
        /// </summary>
        /// <param name="data">The stream containing the UDF file system.</param>
        /// <param name="sectorSize">The sector size of the physical media.</param>
        public UdfReader(Stream data, int sectorSize)
            : base(new VfsUdfReader(data, sectorSize)) {}

        /// <summary>
        /// Detects if a stream contains a valid UDF file system.
        /// </summary>
        /// <param name="data">The stream to inspect.</param>
        /// <returns><c>true</c> if the stream contains a UDF file system, else false.</returns>
        public static bool Detect(Stream data)
        {
            if (data.Length < IsoUtilities.SectorSize)
            {
                return false;
            }

            long vdpos = 0x8000; // Skip lead-in

            byte[] buffer = new byte[IsoUtilities.SectorSize];

            bool validDescriptor = true;
            bool foundUdfMarker = false;

            BaseVolumeDescriptor bvd;
            while (validDescriptor)
            {
                data.Position = vdpos;
                int numRead = StreamUtilities.ReadMaximum(data, buffer, 0, IsoUtilities.SectorSize);
                if (numRead != IsoUtilities.SectorSize)
                {
                    break;
                }

                bvd = new BaseVolumeDescriptor(buffer, 0);
                switch (bvd.StandardIdentifier)
                {
                    case "NSR02":
                    case "NSR03":
                        foundUdfMarker = true;
                        break;

                    case "BEA01":
                    case "BOOT2":
                    case "CD001":
                    case "CDW02":
                    case "TEA01":
                        break;

                    default:
                        validDescriptor = false;
                        break;
                }

                vdpos += IsoUtilities.SectorSize;
            }

            return foundUdfMarker;
        }

        /// <summary>
        /// Gets UDF extended attributes for a file or directory.
        /// </summary>
        /// <param name="path">Path to the file or directory.</param>
        /// <returns>Array of extended attributes, which may be empty or <c>null</c> if
        /// there are no extended attributes.</returns>
        public ExtendedAttribute[] GetExtendedAttributes(string path)
        {
            VfsUdfReader realFs = GetRealFileSystem<VfsUdfReader>();
            return realFs.GetExtendedAttributes(path);
        }

        private sealed class VfsUdfReader : VfsReadOnlyFileSystem<FileIdentifier, File, Directory, UdfContext>
        {
            private readonly Stream _data;
            private LogicalVolumeDescriptor _lvd;
            private PrimaryVolumeDescriptor _pvd;
            private readonly uint _sectorSize;

            public VfsUdfReader(Stream data)
                : base(null)
            {
                _data = data;

                if (!Detect(data))
                {
                    throw new InvalidDataException("Stream is not a recognized UDF format");
                }

                // Try a number of possible sector sizes, from most common.
                if (ProbeSectorSize(2048))
                {
                    _sectorSize = 2048;
                }
                else if (ProbeSectorSize(512))
                {
                    _sectorSize = 512;
                }
                else if (ProbeSectorSize(4096))
                {
                    _sectorSize = 4096;
                }
                else if (ProbeSectorSize(1024))
                {
                    _sectorSize = 1024;
                }
                else
                {
                    throw new InvalidDataException("Unable to detect physical media sector size");
                }

                Initialize();
            }

            public VfsUdfReader(Stream data, int sectorSize)
                : base(null)
            {
                _data = data;
                _sectorSize = (uint)sectorSize;

                if (!Detect(data))
                {
                    throw new InvalidDataException("Stream is not a recognized UDF format");
                }

                Initialize();
            }

            public override string FriendlyName
            {
                get { return "OSTA Universal Disk Format"; }
            }

            public override string VolumeLabel
            {
                get { return _lvd.LogicalVolumeIdentifier; }
            }

            public ExtendedAttribute[] GetExtendedAttributes(string path)
            {
                List<ExtendedAttribute> result = new List<ExtendedAttribute>();

                File file = GetFile(path);
                foreach (ExtendedAttributeRecord record in file.ExtendedAttributes)
                {
                    ImplementationUseExtendedAttributeRecord implRecord =
                        record as ImplementationUseExtendedAttributeRecord;
                    if (implRecord != null)
                    {
                        result.Add(new ExtendedAttribute(implRecord.ImplementationIdentifier.Identifier,
                            implRecord.ImplementationUseData));
                    }
                }

                return result.ToArray();
            }
 
            /// <summary>
            /// Size of the Filesystem in bytes
            /// </summary>
            public override long Size
            {
                get { throw new NotSupportedException("Filesystem size is not (yet) supported"); }
            }

            /// <summary>
            /// Used space of the Filesystem in bytes
            /// </summary>
            public override long UsedSpace
            {
                 get { throw new NotSupportedException("Filesystem size is not (yet) supported"); }
            }

            /// <summary>
            /// Available space of the Filesystem in bytes
            /// </summary>
            public override long AvailableSpace
            {
                get { throw new NotSupportedException("Filesystem size is not (yet) supported"); }
            }

            protected override File ConvertDirEntryToFile(FileIdentifier dirEntry)
            {
                return File.FromDescriptor(Context, dirEntry.FileLocation);
            }

            private void Initialize()
            {
                Context = new UdfContext
                {
                    PhysicalPartitions = new Dictionary<ushort, PhysicalPartition>(),
                    PhysicalSectorSize = (int)_sectorSize,
                    LogicalPartitions = new List<LogicalPartition>()
                };

                IBuffer dataBuffer = new StreamBuffer(_data, Ownership.None);

                AnchorVolumeDescriptorPointer avdp = AnchorVolumeDescriptorPointer.FromStream(_data, 256, _sectorSize);

                uint sector = avdp.MainDescriptorSequence.Location;
                bool terminatorFound = false;
                while (!terminatorFound)
                {
                    _data.Position = sector * (long)_sectorSize;

                    DescriptorTag dt;
                    if (!DescriptorTag.TryFromStream(_data, out dt))
                    {
                        break;
                    }

                    switch (dt.TagIdentifier)
                    {
                        case TagIdentifier.PrimaryVolumeDescriptor:
                            _pvd = PrimaryVolumeDescriptor.FromStream(_data, sector, _sectorSize);
                            break;

                        case TagIdentifier.ImplementationUseVolumeDescriptor:

                            // Not used
                            break;

                        case TagIdentifier.PartitionDescriptor:
                            PartitionDescriptor pd = PartitionDescriptor.FromStream(_data, sector, _sectorSize);
                            if (Context.PhysicalPartitions.ContainsKey(pd.PartitionNumber))
                            {
                                throw new IOException("Duplicate partition number reading UDF Partition Descriptor");
                            }

                            Context.PhysicalPartitions[pd.PartitionNumber] = new PhysicalPartition(pd, dataBuffer,
                                _sectorSize);
                            break;

                        case TagIdentifier.LogicalVolumeDescriptor:
                            _lvd = LogicalVolumeDescriptor.FromStream(_data, sector, _sectorSize);
                            break;

                        case TagIdentifier.UnallocatedSpaceDescriptor:

                            // Not used for reading
                            break;

                        case TagIdentifier.TerminatingDescriptor:
                            terminatorFound = true;
                            break;

                        default:
                            break;
                    }

                    sector++;
                }

                // Convert logical partition descriptors into actual partition objects
                for (int i = 0; i < _lvd.PartitionMaps.Length; ++i)
                {
                    Context.LogicalPartitions.Add(LogicalPartition.FromDescriptor(Context, _lvd, i));
                }

                byte[] fsdBuffer = UdfUtilities.ReadExtent(Context, _lvd.FileSetDescriptorLocation);
                if (DescriptorTag.IsValid(fsdBuffer, 0))
                {
                    FileSetDescriptor fsd = EndianUtilities.ToStruct<FileSetDescriptor>(fsdBuffer, 0);
                    RootDirectory = (Directory)File.FromDescriptor(Context, fsd.RootDirectoryIcb);
                }
            }

            private bool ProbeSectorSize(int size)
            {
                if (_data.Length < 257 * (long)size)
                {
                    return false;
                }

                _data.Position = 256 * (long)size;

                DescriptorTag dt;
                if (!DescriptorTag.TryFromStream(_data, out dt))
                {
                    return false;
                }

                return dt.TagIdentifier == TagIdentifier.AnchorVolumeDescriptorPointer
                       && dt.TagLocation == 256;
            }
        }
    }
}
