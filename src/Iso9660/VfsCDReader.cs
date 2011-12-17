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

namespace DiscUtils.Iso9660
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using DiscUtils.Vfs;

    internal class VfsCDReader : VfsReadOnlyFileSystem<DirectoryRecord, File, ReaderDirectory, IsoContext>, IClusterBasedFileSystem
    {
        private Stream _data;
        private bool _hideVersions;
        private BootVolumeDescriptor _bootVolDesc;
        private byte[] _bootCatalog;

        /// <summary>
        /// Initializes a new instance of the VfsCDReader class.
        /// </summary>
        /// <param name="data">The stream to read the ISO image from.</param>
        /// <param name="joliet">Whether to read Joliet extensions.</param>
        /// <param name="hideVersions">Hides version numbers (e.g. ";1") from the end of files</param>
        public VfsCDReader(Stream data, bool joliet, bool hideVersions)
            : base(new DiscFileSystemOptions())
        {
            _data = data;
            _hideVersions = hideVersions;

            long vdpos = 0x8000; // Skip lead-in

            byte[] buffer = new byte[IsoUtilities.SectorSize];

            long pvdPos = 0;
            long svdPos = 0;

            BaseVolumeDescriptor bvd;
            do
            {
                data.Position = vdpos;
                int numRead = data.Read(buffer, 0, IsoUtilities.SectorSize);
                if (numRead != IsoUtilities.SectorSize)
                {
                    break;
                }

                bvd = new BaseVolumeDescriptor(buffer, 0);
                switch (bvd.VolumeDescriptorType)
                {
                    case VolumeDescriptorType.Boot:
                        _bootVolDesc = new BootVolumeDescriptor(buffer, 0);
                        if (_bootVolDesc.SystemId != BootVolumeDescriptor.ElToritoSystemIdentifier)
                        {
                            _bootVolDesc = null;
                        }

                        break;

                    case VolumeDescriptorType.Primary: // Primary Vol Descriptor
                        pvdPos = vdpos;
                        break;

                    case VolumeDescriptorType.Supplementary: // Supplementary Vol Descriptor
                        svdPos = vdpos;
                        break;

                    case VolumeDescriptorType.Partition: // Volume Partition Descriptor
                        break;
                    case VolumeDescriptorType.SetTerminator: // Volume Descriptor Set Terminator
                        break;
                }

                vdpos += IsoUtilities.SectorSize;
            }
            while (bvd.VolumeDescriptorType != VolumeDescriptorType.SetTerminator);

            CommonVolumeDescriptor volDesc;
            if (joliet && svdPos != 0)
            {
                data.Position = svdPos;
                data.Read(buffer, 0, IsoUtilities.SectorSize);
                volDesc = new SupplementaryVolumeDescriptor(buffer, 0);
            }
            else
            {
                data.Position = pvdPos;
                data.Read(buffer, 0, IsoUtilities.SectorSize);
                volDesc = new PrimaryVolumeDescriptor(buffer, 0);
            }

            Context = new IsoContext { VolumeDescriptor = volDesc, DataStream = _data };
            RootDirectory = new ReaderDirectory(Context, volDesc.RootDirectory);
        }

        /// <summary>
        /// Provides the friendly name for the CD filesystem.
        /// </summary>
        public override string FriendlyName
        {
            get { return "ISO 9660 (CD-ROM)"; }
        }

        /// <summary>
        /// Gets the Volume Identifier.
        /// </summary>
        public override string VolumeLabel
        {
            get { return Context.VolumeDescriptor.VolumeIdentifier; }
        }

        public bool HasBootImage
        {
            get
            {
                if (_bootVolDesc == null)
                {
                    return false;
                }

                byte[] bootCatalog = GetBootCatalog();
                if (bootCatalog == null)
                {
                    return false;
                }

                BootValidationEntry entry = new BootValidationEntry(bootCatalog, 0);
                return entry.ChecksumValid;
            }
        }

        public BootDeviceEmulation BootEmulation
        {
            get
            {
                BootInitialEntry initialEntry = GetBootInitialEntry();
                if (initialEntry != null)
                {
                    return initialEntry.BootMediaType;
                }

                return BootDeviceEmulation.NoEmulation;
            }
        }

        public int BootLoadSegment
        {
            get
            {
                BootInitialEntry initialEntry = GetBootInitialEntry();
                if (initialEntry != null)
                {
                    return initialEntry.LoadSegment;
                }

                return 0;
            }
        }

        public long BootImageStart
        {
            get
            {
                BootInitialEntry initialEntry = GetBootInitialEntry();
                if (initialEntry != null)
                {
                    return initialEntry.ImageStart * IsoUtilities.SectorSize;
                }
                else
                {
                    return 0;
                }
            }
        }

        public long ClusterSize
        {
            get { return IsoUtilities.SectorSize; }
        }

        public long TotalClusters
        {
            get { return Context.VolumeDescriptor.VolumeSpaceSize; }
        }

        public Stream OpenBootImage()
        {
            BootInitialEntry initialEntry = GetBootInitialEntry();
            if (initialEntry != null)
            {
                return new SubStream(_data, initialEntry.ImageStart * IsoUtilities.SectorSize, initialEntry.SectorCount * Sizes.Sector);
            }
            else
            {
                throw new InvalidOperationException("No valid boot image");
            }
        }

        public long ClusterToOffset(long cluster)
        {
            return cluster * ClusterSize;
        }

        public long OffsetToCluster(long offset)
        {
            return offset / ClusterSize;
        }

        public Range<long, long>[] PathToClusters(string path)
        {
            DirectoryRecord entry = GetDirectoryEntry(path);
            if (entry == null)
            {
                throw new FileNotFoundException("File not found", path);
            }

            if (entry.FileUnitSize != 0 || entry.InterleaveGapSize != 0)
            {
                throw new NotSupportedException("Non-contiguous extents not supported");
            }

            return new Range<long, long>[] { new Range<long, long>(entry.LocationOfExtent, Utilities.Ceil(entry.DataLength, IsoUtilities.SectorSize)) };
        }

        public StreamExtent[] PathToExtents(string path)
        {
            DirectoryRecord entry = GetDirectoryEntry(path);
            if (entry == null)
            {
                throw new FileNotFoundException("File not found", path);
            }

            if (entry.FileUnitSize != 0 || entry.InterleaveGapSize != 0)
            {
                throw new NotSupportedException("Non-contiguous extents not supported");
            }

            return new StreamExtent[] { new StreamExtent(entry.LocationOfExtent * IsoUtilities.SectorSize, entry.DataLength) };
        }

        public ClusterMap BuildClusterMap()
        {
            long totalClusters = TotalClusters;
            ClusterRoles[] clusterToRole = new ClusterRoles[totalClusters];
            object[] clusterToFileId = new object[totalClusters];
            Dictionary<object, string[]> fileIdToPaths = new Dictionary<object, string[]>();

            ForAllDirEntries(
                string.Empty,
                (path, entry) =>
                {
                    string[] paths = null;
                    if (fileIdToPaths.ContainsKey(entry.UniqueCacheId))
                    {
                        paths = fileIdToPaths[entry.UniqueCacheId];
                    }

                    if (paths == null)
                    {
                        fileIdToPaths[entry.UniqueCacheId] = new string[] { path };
                    }
                    else
                    {
                        string[] newPaths = new string[paths.Length + 1];
                        Array.Copy(paths, newPaths, paths.Length);
                        newPaths[paths.Length] = path;
                        fileIdToPaths[entry.UniqueCacheId] = newPaths;
                    }

                    if (entry.FileUnitSize != 0 || entry.InterleaveGapSize != 0)
                    {
                        throw new NotSupportedException("Non-contiguous extents not supported");
                    }

                    long clusters = Utilities.Ceil(entry.DataLength, IsoUtilities.SectorSize);
                    for (long i = 0; i < clusters; ++i)
                    {
                        clusterToRole[i + entry.LocationOfExtent] = ClusterRoles.DataFile;
                        clusterToFileId[i + entry.LocationOfExtent] = entry.UniqueCacheId;
                    }
                });

            return new ClusterMap(clusterToRole, clusterToFileId, fileIdToPaths);
        }

        protected override File ConvertDirEntryToFile(DirectoryRecord dirEntry)
        {
            if (dirEntry.IsDirectory)
            {
                return new ReaderDirectory(Context, dirEntry);
            }
            else
            {
                return new File(Context, dirEntry);
            }
        }

        protected override string FormatFileName(string name)
        {
            if (_hideVersions)
            {
                int pos = name.LastIndexOf(';');
                if (pos > 0)
                {
                    return name.Substring(0, pos);
                }
            }

            return name;
        }

        private BootInitialEntry GetBootInitialEntry()
        {
            byte[] bootCatalog = GetBootCatalog();
            if (bootCatalog == null)
            {
                return null;
            }

            BootValidationEntry validationEntry = new BootValidationEntry(bootCatalog, 0);
            if (!validationEntry.ChecksumValid)
            {
                return null;
            }

            return new BootInitialEntry(bootCatalog, 0x20);
        }

        private byte[] GetBootCatalog()
        {
            if (_bootCatalog == null && _bootVolDesc != null)
            {
                _data.Position = _bootVolDesc.CatalogSector * IsoUtilities.SectorSize;
                _bootCatalog = Utilities.ReadFully(_data, IsoUtilities.SectorSize);
            }

            return _bootCatalog;
        }
    }
}
