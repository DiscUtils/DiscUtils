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
using System.Text.RegularExpressions;
using DiscUtils.Iso9660;

namespace DiscUtils.Udf
{
    public sealed class UdfReader : ReadOnlyDiscFileSystem
    {
        private Stream _data;
        private UdfContext _context;
        private uint _sectorSize;
        private Directory _rootDir;

        public UdfReader(Stream data)
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

        public UdfReader(Stream data, int sectorSize)
        {
            _data = data;
            _sectorSize = (uint)sectorSize;

            if (!Detect(data))
            {
                throw new InvalidDataException("Stream is not a recognized UDF format");
            }

            Initialize();
        }

        private void Initialize()
        {
            _context = new UdfContext()
            {
                PhysicalPartitions = new Dictionary<ushort, PhysicalPartition>(),
                PhysicalSectorSize = (int)_sectorSize,
                LogicalPartitions = new List<LogicalPartition>(),
            };

            IBuffer dataBuffer = new StreamBuffer(_data, Ownership.None);

            AnchorVolumeDescriptorPointer avdp = AnchorVolumeDescriptorPointer.FromStream(_data, 256, _sectorSize);


            PrimaryVolumeDescriptor pvd = null;
            ImplementationUseVolumeDescriptor iuvd = null;
            LogicalVolumeDescriptor lvd = null;
            UnallocatedSpaceDescriptor usd = null;

            uint sector = avdp.MainDescriptorSequence.Location;
            bool terminatorFound = false;
            while (!terminatorFound)
            {
                _data.Position = sector * _sectorSize;

                DescriptorTag dt;
                if (!DescriptorTag.TryFromStream(_data, out dt))
                {
                    break;
                }

                switch (dt.TagIdentifier)
                {
                    case TagIdentifier.PrimaryVolumeDescriptor:
                        pvd = PrimaryVolumeDescriptor.FromStream(_data, sector, _sectorSize);
                        break;

                    case TagIdentifier.ImplementationUseVolumeDescriptor:
                        iuvd = ImplementationUseVolumeDescriptor.FromStream(_data, sector, _sectorSize);
                        break;

                    case TagIdentifier.PartitionDescriptor:
                        PartitionDescriptor pd = PartitionDescriptor.FromStream(_data, sector, _sectorSize);
                        if (_context.PhysicalPartitions.ContainsKey(pd.PartitionNumber))
                        {
                            throw new IOException("Duplicate partition number reading UDF Partition Descriptor");
                        }
                        _context.PhysicalPartitions[pd.PartitionNumber] = new PhysicalPartition(pd, dataBuffer, _sectorSize);
                        break;

                    case TagIdentifier.LogicalVolumeDescriptor:
                        lvd = LogicalVolumeDescriptor.FromStream(_data, sector, _sectorSize);
                        break;

                    case TagIdentifier.UnallocatedSpaceDescriptor:
                        usd = UnallocatedSpaceDescriptor.FromStream(_data, sector, _sectorSize);
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
            for (int i = 0; i < lvd.PartitionMaps.Length; ++i)
            {
                _context.LogicalPartitions.Add(LogicalPartition.FromDescriptor(_context, lvd, i));
            }


            byte[] fsdBuffer = UdfUtilities.ReadExtent(_context, lvd.FileSetDescriptorLocation);
            if(DescriptorTag.IsValid(fsdBuffer, 0))
            {
                FileSetDescriptor fsd = Utilities.ToStruct<FileSetDescriptor>(fsdBuffer, 0);
                _rootDir = (Directory)File.FromDescriptor(_context, fsd.RootDirectoryIcb);
            }
        }

        public override string FriendlyName
        {
            get { return "OSTA Universal Disk Format"; }
        }

        public override bool DirectoryExists(string path)
        {
            FileIdentifier fileId = GetDirectoryEntry(path);

            if (fileId != null)
            {
                return (fileId.FileCharacteristics & FileCharacteristic.Directory) != 0;
            }

            return false;
        }

        public override bool FileExists(string path)
        {
            FileIdentifier fileId = GetDirectoryEntry(path);

            if (fileId != null)
            {
                return (fileId.FileCharacteristics & FileCharacteristic.Directory) == 0;
            }

            return false;
        }

        public override string[] GetDirectories(string path, string searchPattern, SearchOption searchOption)
        {
            Regex re = Utilities.ConvertWildcardsToRegEx(searchPattern);

            List<string> dirs = new List<string>();
            DoSearch(dirs, path, re, searchOption == SearchOption.AllDirectories, true, false);
            return dirs.ToArray();
        }

        public override string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
        {
            Regex re = Utilities.ConvertWildcardsToRegEx(searchPattern);

            List<string> results = new List<string>();
            DoSearch(results, path, re, searchOption == SearchOption.AllDirectories, false, true);
            return results.ToArray();
        }

        public override string[] GetFileSystemEntries(string path)
        {
            Directory parentDir = GetDirectory(path);
            return Utilities.Map<FileIdentifier, string>(parentDir.Entries, (m) => Utilities.CombinePaths(path, m.Name));
        }

        public override string[] GetFileSystemEntries(string path, string searchPattern)
        {
            throw new NotImplementedException();
        }

        public override Stream OpenFile(string path, FileMode mode, FileAccess access)
        {
            throw new NotImplementedException();
        }

        public override FileAttributes GetAttributes(string path)
        {
            throw new NotImplementedException();
        }

        public override DateTime GetCreationTimeUtc(string path)
        {
            File file = GetFile(path);
            if (file == null)
            {
                throw new FileNotFoundException("No such file or directory", path);
            }

            return file.CreationTimeUtc;
        }

        public override DateTime GetLastAccessTimeUtc(string path)
        {
            throw new NotImplementedException();
        }

        public override DateTime GetLastWriteTimeUtc(string path)
        {
            throw new NotImplementedException();
        }

        public override long GetFileLength(string path)
        {
            throw new NotImplementedException();
        }

        public static bool Detect(Stream data)
        {
            long vdpos = 0x8000; // Skip lead-in

            byte[] buffer = new byte[IsoUtilities.SectorSize];

            bool validDescriptor = true;
            bool foundUdfMarker = false;

            BaseVolumeDescriptor bvd;
            while(validDescriptor)
            {
                data.Position = vdpos;
                int numRead = Utilities.ReadFully(data, buffer, 0, IsoUtilities.SectorSize);
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

        internal File GetFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return _rootDir;
            }

            FileIdentifier dirEntry = GetDirectoryEntry(path);
            if (dirEntry == null)
            {
                throw new FileNotFoundException("No such file or directory", path);
            }

            return File.FromDescriptor(_context, dirEntry.FileLocation);
        }

        internal Directory GetDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return _rootDir;
            }

            FileIdentifier dirEntry = GetDirectoryEntry(path);
            if (dirEntry == null || (dirEntry.FileCharacteristics & FileCharacteristic.Directory) == 0)
            {
                throw new DirectoryNotFoundException("No such directory: " + path);
            }

            return (Directory)File.FromDescriptor(_context, dirEntry.FileLocation);
        }

        internal FileIdentifier GetDirectoryEntry(string path)
        {
            return GetDirectoryEntry(_rootDir, path);
        }

        private FileIdentifier GetDirectoryEntry(Directory dir, string path)
        {
            string[] pathElements = path.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            return GetDirectoryEntry(dir, pathElements, 0);
        }

        private FileIdentifier GetDirectoryEntry(Directory dir, string[] pathEntries, int pathOffset)
        {
            FileIdentifier entry;

            if (pathEntries.Length == 0)
            {
                return null;
            }
            else
            {
                entry = dir.GetEntryByName(pathEntries[pathOffset]);
                if (entry != null)
                {
                    if (pathOffset == pathEntries.Length - 1)
                    {
                        return entry;
                    }
                    else if ((entry.FileCharacteristics & FileCharacteristic.Directory) != 0)
                    {
                        return GetDirectoryEntry((Directory)File.FromDescriptor(_context, entry.FileLocation), pathEntries, pathOffset + 1);
                    }
                    else
                    {
                        throw new IOException(string.Format(CultureInfo.InvariantCulture, "{0} is a file, not a directory", pathEntries[pathOffset]));
                    }
                }
                else
                {
                    return null;
                }
            }
        }

        private void DoSearch(List<string> results, string path, Regex regex, bool subFolders, bool dirs, bool files)
        {
            Directory parentDir = GetDirectory(path);
            if (parentDir == null)
            {
                throw new DirectoryNotFoundException(string.Format(CultureInfo.InvariantCulture, "The directory '{0}' was not found", path));
            }

            foreach (FileIdentifier de in parentDir.Entries)
            {
                bool isDir = ((de.FileCharacteristics & FileCharacteristic.Directory) != 0);

                if ((isDir && dirs) || (!isDir && files))
                {
                    if (regex.IsMatch(de.SearchName))
                    {
                        results.Add(Path.Combine(path, de.Name));
                    }
                }

                if (subFolders && isDir)
                {
                    DoSearch(results, Path.Combine(path, de.Name), regex, subFolders, dirs, files);
                }
            }
        }

        private bool ProbeSectorSize(int size)
        {
            if (_data.Length < 257 * size)
            {
                return false;
            }

            _data.Position = 256 * size;

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
