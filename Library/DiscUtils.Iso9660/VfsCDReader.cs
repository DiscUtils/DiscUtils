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
using DiscUtils.Vfs;

namespace DiscUtils.Iso9660
{
    internal class VfsCDReader : VfsReadOnlyFileSystem<ReaderDirEntry, File, ReaderDirectory, IsoContext>,
                                 IClusterBasedFileSystem, IUnixFileSystem
    {
        private static readonly Iso9660Variant[] DefaultVariantsNoJoliet = { Iso9660Variant.RockRidge, Iso9660Variant.Iso9660 };

        private static readonly Iso9660Variant[] DefaultVariantsWithJoliet = { Iso9660Variant.Joliet, Iso9660Variant.RockRidge, Iso9660Variant.Iso9660 };

        private byte[] _bootCatalog;
        private readonly BootVolumeDescriptor _bootVolDesc;

        private readonly Stream _data;
        private readonly bool _hideVersions;

        /// <summary>
        /// Initializes a new instance of the VfsCDReader class.
        /// </summary>
        /// <param name="data">The stream to read the ISO image from.</param>
        /// <param name="joliet">Whether to read Joliet extensions.</param>
        /// <param name="hideVersions">Hides version numbers (e.g. ";1") from the end of files.</param>
        public VfsCDReader(Stream data, bool joliet, bool hideVersions)
            : this(data, joliet ? DefaultVariantsWithJoliet : DefaultVariantsNoJoliet, hideVersions) {}

        /// <summary>
        /// Initializes a new instance of the VfsCDReader class.
        /// </summary>
        /// <param name="data">The stream to read the ISO image from.</param>
        /// <param name="variantPriorities">Which possible file system variants to use, and with which priority.</param>
        /// <param name="hideVersions">Hides version numbers (e.g. ";1") from the end of files.</param>
        /// <remarks>
        /// <para>
        /// The implementation considers each of the file system variants in <c>variantProperties</c> and selects
        /// the first which is determined to be present.  In this example Joliet, then Rock Ridge, then vanilla
        /// Iso9660 will be considered:
        /// </para>
        /// <code lang="cs">
        /// VfsCDReader(stream, new Iso9660Variant[] {Joliet, RockRidge, Iso9660}, true);
        /// </code>
        /// <para>The Iso9660 variant should normally be specified as the final entry in the list.  Placing it earlier
        /// in the list will effectively mask later items and not including it may prevent some ISOs from being read.</para>
        /// </remarks>
        public VfsCDReader(Stream data, Iso9660Variant[] variantPriorities, bool hideVersions)
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

                if (bvd.StandardIdentifier != BaseVolumeDescriptor.Iso9660StandardIdentifier)
                {
                    throw new InvalidFileSystemException("Volume is not ISO-9660");
                }

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
            } while (bvd.VolumeDescriptorType != VolumeDescriptorType.SetTerminator);

            ActiveVariant = Iso9660Variant.None;
            foreach (Iso9660Variant variant in variantPriorities)
            {
                switch (variant)
                {
                    case Iso9660Variant.Joliet:
                        if (svdPos != 0)
                        {
                            data.Position = svdPos;
                            data.Read(buffer, 0, IsoUtilities.SectorSize);
                            SupplementaryVolumeDescriptor volDesc = new SupplementaryVolumeDescriptor(buffer, 0);

                            Context = new IsoContext { VolumeDescriptor = volDesc, DataStream = _data };
                            RootDirectory = new ReaderDirectory(Context,
                                new ReaderDirEntry(Context, volDesc.RootDirectory));
                            ActiveVariant = Iso9660Variant.Iso9660;
                        }

                        break;

                    case Iso9660Variant.RockRidge:
                    case Iso9660Variant.Iso9660:
                        if (pvdPos != 0)
                        {
                            data.Position = pvdPos;
                            data.Read(buffer, 0, IsoUtilities.SectorSize);
                            PrimaryVolumeDescriptor volDesc = new PrimaryVolumeDescriptor(buffer, 0);

                            IsoContext context = new IsoContext { VolumeDescriptor = volDesc, DataStream = _data };
                            DirectoryRecord rootSelfRecord = ReadRootSelfRecord(context);

                            InitializeSusp(context, rootSelfRecord);

                            if (variant == Iso9660Variant.Iso9660
                                ||
                                (variant == Iso9660Variant.RockRidge &&
                                 !string.IsNullOrEmpty(context.RockRidgeIdentifier)))
                            {
                                Context = context;
                                RootDirectory = new ReaderDirectory(context, new ReaderDirEntry(context, rootSelfRecord));
                                ActiveVariant = variant;
                            }
                        }

                        break;
                }

                if (ActiveVariant != Iso9660Variant.None)
                {
                    break;
                }
            }

            if (ActiveVariant == Iso9660Variant.None)
            {
                throw new IOException("None of the permitted ISO9660 file system variants was detected");
            }
        }

        public Iso9660Variant ActiveVariant { get; }

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

        public long BootImageStart
        {
            get
            {
                BootInitialEntry initialEntry = GetBootInitialEntry();
                if (initialEntry != null)
                {
                    return initialEntry.ImageStart * IsoUtilities.SectorSize;
                }
                return 0;
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

        /// <summary>
        /// Provides the friendly name for the CD filesystem.
        /// </summary>
        public override string FriendlyName
        {
            get { return "ISO 9660 (CD-ROM)"; }
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

        /// <summary>
        /// Gets the Volume Identifier.
        /// </summary>
        public override string VolumeLabel
        {
            get { return Context.VolumeDescriptor.VolumeIdentifier; }
        }

        public long ClusterSize
        {
            get { return IsoUtilities.SectorSize; }
        }

        public long TotalClusters
        {
            get { return Context.VolumeDescriptor.VolumeSpaceSize; }
        }

        public long ClusterToOffset(long cluster)
        {
            return cluster * ClusterSize;
        }

        public long OffsetToCluster(long offset)
        {
            return offset / ClusterSize;
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

        public Range<long, long>[] PathToClusters(string path)
        {
            ReaderDirEntry entry = GetDirectoryEntry(path);
            if (entry == null)
            {
                throw new FileNotFoundException("File not found", path);
            }

            if (entry.Record.FileUnitSize != 0 || entry.Record.InterleaveGapSize != 0)
            {
                throw new NotSupportedException("Non-contiguous extents not supported");
            }

            return new[]
            {
                new Range<long, long>(entry.Record.LocationOfExtent,
                    MathUtilities.Ceil(entry.Record.DataLength, IsoUtilities.SectorSize))
            };
        }

        public StreamExtent[] PathToExtents(string path)
        {
            ReaderDirEntry entry = GetDirectoryEntry(path);
            if (entry == null)
            {
                throw new FileNotFoundException("File not found", path);
            }

            if (entry.Record.FileUnitSize != 0 || entry.Record.InterleaveGapSize != 0)
            {
                throw new NotSupportedException("Non-contiguous extents not supported");
            }

            return new[]
                { new StreamExtent(entry.Record.LocationOfExtent * IsoUtilities.SectorSize, entry.Record.DataLength) };
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
                        fileIdToPaths[entry.UniqueCacheId] = new[] { path };
                    }
                    else
                    {
                        string[] newPaths = new string[paths.Length + 1];
                        Array.Copy(paths, newPaths, paths.Length);
                        newPaths[paths.Length] = path;
                        fileIdToPaths[entry.UniqueCacheId] = newPaths;
                    }

                    if (entry.Record.FileUnitSize != 0 || entry.Record.InterleaveGapSize != 0)
                    {
                        throw new NotSupportedException("Non-contiguous extents not supported");
                    }

                    long clusters = MathUtilities.Ceil(entry.Record.DataLength, IsoUtilities.SectorSize);
                    for (long i = 0; i < clusters; ++i)
                    {
                        clusterToRole[i + entry.Record.LocationOfExtent] = ClusterRoles.DataFile;
                        clusterToFileId[i + entry.Record.LocationOfExtent] = entry.UniqueCacheId;
                    }
                });

            return new ClusterMap(clusterToRole, clusterToFileId, fileIdToPaths);
        }

        public UnixFileSystemInfo GetUnixFileInfo(string path)
        {
            File file = GetFile(path);
            return file.UnixFileInfo;
        }

        public Stream OpenBootImage()
        {
            BootInitialEntry initialEntry = GetBootInitialEntry();
            if (initialEntry != null)
            {
                return new SubStream(_data, initialEntry.ImageStart * IsoUtilities.SectorSize,
                    initialEntry.SectorCount * Sizes.Sector);
            }
            throw new InvalidOperationException("No valid boot image");
        }

        protected override File ConvertDirEntryToFile(ReaderDirEntry dirEntry)
        {
            if (dirEntry.IsDirectory)
            {
                return new ReaderDirectory(Context, dirEntry);
            }
            return new File(Context, dirEntry);
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

        private static void InitializeSusp(IsoContext context, DirectoryRecord rootSelfRecord)
        {
            // Stage 1 - SUSP present?
            List<SuspExtension> extensions = new List<SuspExtension>();
            if (!SuspRecords.DetectSharingProtocol(rootSelfRecord.SystemUseData, 0))
            {
                context.SuspExtensions = new List<SuspExtension>();
                context.SuspDetected = false;
                return;
            }
            context.SuspDetected = true;

            SuspRecords suspRecords = new SuspRecords(context, rootSelfRecord.SystemUseData, 0);

            // Stage 2 - Init general SUSP params
            SharingProtocolSystemUseEntry spEntry =
                (SharingProtocolSystemUseEntry)suspRecords.GetEntries(null, "SP")[0];
            context.SuspSkipBytes = spEntry.SystemAreaSkip;

            // Stage 3 - Init extensions
            List<SystemUseEntry> extensionEntries = suspRecords.GetEntries(null, "ER");
            if (extensionEntries != null)
            {
                foreach (ExtensionSystemUseEntry extension in extensionEntries)
                {
                    switch (extension.ExtensionIdentifier)
                    {
                        case "RRIP_1991A":
                        case "IEEE_P1282":
                        case "IEEE_1282":
                            extensions.Add(new RockRidgeExtension(extension.ExtensionIdentifier));
                            context.RockRidgeIdentifier = extension.ExtensionIdentifier;
                            break;

                        default:
                            extensions.Add(new GenericSuspExtension(extension.ExtensionIdentifier));
                            break;
                    }
                }
            }
            else if (suspRecords.GetEntries(null, "RR") != null)
            {
                // Some ISO creators don't add the 'ER' record for RockRidge, but write the (legacy)
                // RR record anyway
                extensions.Add(new RockRidgeExtension("RRIP_1991A"));
                context.RockRidgeIdentifier = "RRIP_1991A";
            }

            context.SuspExtensions = extensions;
        }

        private static DirectoryRecord ReadRootSelfRecord(IsoContext context)
        {
            context.DataStream.Position = context.VolumeDescriptor.RootDirectory.LocationOfExtent *
                                          context.VolumeDescriptor.LogicalBlockSize;
            byte[] firstSector = StreamUtilities.ReadExact(context.DataStream, context.VolumeDescriptor.LogicalBlockSize);

            DirectoryRecord rootSelfRecord;
            DirectoryRecord.ReadFrom(firstSector, 0, context.VolumeDescriptor.CharacterEncoding, out rootSelfRecord);
            return rootSelfRecord;
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
                _bootCatalog = StreamUtilities.ReadExact(_data, IsoUtilities.SectorSize);
            }

            return _bootCatalog;
        }
    }
}
