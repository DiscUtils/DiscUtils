//
// Copyright (c) 2008, Kenneth Bell
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
using System.Text;

namespace DiscUtils.Fat
{
    /// <summary>
    /// Class for accessing FAT file systems.
    /// </summary>
    public class FatFileSystem : DiscFileSystem
    {
        private TimeZoneInfo _timeZone;
        private Stream _data;
        private byte[] _bootSector;
        private FileAllocationTable _fat;
        private ClusterReader _clusterReader;
        private Directory _rootDir;
        private Dictionary<uint, Directory> _dirCache;


        private FatType _type;
        private string _bpbOEMName;
        private ushort _bpbBytesPerSec;
        private byte _bpbSecPerClus;
        private ushort _bpbRsvdSecCnt;
        private byte _bpbNumFATs;
        private ushort _bpbRootEntCnt;
        private ushort _bpbTotSec16;
        private byte _bpbMedia;
        private ushort _bpbFATSz16;
        private ushort _bpbSecPerTrk;
        private ushort _bpbNumHeads;
        private uint _bpbHiddSec;
        private uint _bpbTotSec32;

        private byte _bsDrvNum;
        private byte _bsBootSig;
        private uint _bsVolId;
        private string _bsVolLab;
        private string _bsFilSysType;

        private uint _bpbFATSz32;
        private ushort _bpbExtFlags;
        private ushort _bpbFSVer;
        private uint _bpbRootClus;
        private ushort _bpbFSInfo;
        private ushort _bpbBkBootSec;

        /// <summary>
        /// The Epoch for FAT file systems (1st Jan, 1980).
        /// </summary>
        public static readonly DateTime Epoch = new DateTime(1980, 1, 1);

        /// <summary>
        /// Creates a new instance, with local time as effective timezone
        /// </summary>
        /// <param name="data">The stream containing the file system.</param>
        public FatFileSystem(Stream data)
        {
            _dirCache = new Dictionary<uint, Directory>();
            _timeZone = TimeZoneInfo.Local;
            Initialize(data);
        }

        /// <summary>
        /// Creates a new instance, with a specific timezone
        /// </summary>
        /// <param name="data">The stream containing the file system.</param>
        public FatFileSystem(Stream data, TimeZoneInfo timeZone)
        {
            _dirCache = new Dictionary<uint, Directory>();
            _timeZone = timeZone;
            Initialize(data);
        }

        /// <summary>
        /// Gets the FAT variant of the file system.
        /// </summary>
        public FatType FATVariant
        {
            get { return _type; }
        }

        /// <summary>
        /// Gets the friendly name for the file system, including FAT variant.
        /// </summary>
        public override string FriendlyName
        {
            get
            {
                switch (_type)
                {
                    case FatType.FAT12: return "Microsoft FAT12";
                    case FatType.FAT16: return "Microsoft FAT16";
                    case FatType.FAT32: return "Microsoft FAT32";
                    default: return "Unknown FAT";
                }
            }
        }

        /// <summary>
        /// Gets the OEM name from the file system.
        /// </summary>
        public string OEMName
        {
            get { return _bpbOEMName; }
        }

        /// <summary>
        /// Gets the number of bytes per sector (as stored in the file-system meta data).
        /// </summary>
        public ushort BytesPerSector
        {
            get { return _bpbBytesPerSec; }
        }

        /// <summary>
        /// Gets the number of contiguous sectors that make up one cluster.
        /// </summary>
        public byte SectorsPerCluster
        {
            get { return _bpbSecPerClus; }
        }

        /// <summary>
        /// Gets the number of reserved sectors at the start of the disk.
        /// </summary>
        public ushort ReservedSectorCount
        {
            get { return _bpbRsvdSecCnt; }
        }

        /// <summary>
        /// Gets the number of FATs present.
        /// </summary>
        public byte NumFATs
        {
            get { return _bpbNumFATs; }
        }

        /// <summary>
        /// Gets the maximum number of root directory entries (on FAT variants that have a limit).
        /// </summary>
        public ushort MaxRootDirectoryEntries
        {
            get { return _bpbRootEntCnt; }
        }

        /// <summary>
        /// Gets the total number of sectors on the disk.
        /// </summary>
        public uint TotalSectors
        {
            get { return (_bpbTotSec16 != 0) ? _bpbTotSec16 : _bpbTotSec32; }
        }

        /// <summary>
        /// Gets the Media marker byte, which indicates fixed or removable media.
        /// </summary>
        public byte Media
        {
            get { return _bpbMedia; }
        }

        /// <summary>
        /// Gets the size of a single FAT, in sectors.
        /// </summary>
        public uint FATSize
        {
            get { return (_bpbFATSz16 != 0) ? _bpbFATSz16 : _bpbFATSz32; }
        }

        /// <summary>
        /// Gets the number of sectors per logical track.
        /// </summary>
        public ushort SectorsPerTrack
        {
            get { return _bpbSecPerTrk; }
        }

        /// <summary>
        /// Gets the number of logical heads.
        /// </summary>
        public ushort NumHeads
        {
            get { return _bpbNumHeads; }
        }

        /// <summary>
        /// Gets the number of hidden sectors, hiding partition tables, etc.
        /// </summary>
        public uint HiddenSectors
        {
            get { return _bpbHiddSec; }
        }

        /// <summary>
        /// BIOS drive number for BIOS Int 13h calls.
        /// </summary>
        public byte BIOSDriveNumber
        {
            get { return _bsDrvNum; }
        }

        /// <summary>
        /// Indicates if the VolumeId, VolumeLabel and FileSystemType fields are valid.
        /// </summary>
        public bool ExtendedBootSignaturePresent
        {
            get { return _bsBootSig == 0x29; }
        }

        /// <summary>
        /// Gets the volume serial number.
        /// </summary>
        public uint VolumeId
        {
            get { return _bsVolId; }
        }

        /// <summary>
        /// Gets the volume label.
        /// </summary>
        public string VolumeLabel
        {
            get { return _bsVolLab; }
        }

        /// <summary>
        /// Gets the (informational only) file system type recorded in the meta-data.
        /// </summary>
        public string FileSystemType
        {
            get { return _bsFilSysType; }
        }

        /// <summary>
        /// Gets the active FAT (zero-based index).
        /// </summary>
        public byte ActiveFAT
        {
            get { return (byte)(((_bpbExtFlags & 0x08) != 0) ? _bpbExtFlags & 0x7 : 0); }
        }

        /// <summary>
        /// Gets whether FAT changes are mirrored to all copies of the FAT.
        /// </summary>
        public bool MirrorFAT
        {
            get { return ((_bpbExtFlags & 0x08) == 0); }
        }

        /// <summary>
        /// Gets the file-system version (usually 0)
        /// </summary>
        public ushort Version
        {
            get { return _bpbFSVer; }
        }

        /// <summary>
        /// Gets the cluster number of the first cluster of the root directory (FAT32 only).
        /// </summary>
        public uint RootDirectoryCluster
        {
            get { return _bpbRootClus; }
        }

        /// <summary>
        /// Gets the sector location of the FSINFO structure (FAT32 only).
        /// </summary>
        public ushort FSInfoSector
        {
            get { return _bpbFSInfo; }
        }

        /// <summary>
        /// Sector location of the backup boot sector (FAT32 only).
        /// </summary>
        public ushort BackupBootSector
        {
            get { return _bpbBkBootSec; }
        }

        /// <summary>
        /// Indicates if this file system is read-only or read-write.
        /// </summary>
        /// <returns></returns>
        public override bool CanWrite()
        {
            // Read-only (for now)
            return false;
        }

        /// <summary>
        /// Gets the root directory of the file system.
        /// </summary>
        public override DiscDirectoryInfo Root
        {
            get { return new FatDirectoryInfo(this, ""); }
        }

        /// <summary>
        /// Opens a file for reading and/or writing.
        /// </summary>
        /// <param name="path">The full path to the file</param>
        /// <param name="mode">The file mode</param>
        /// <param name="access">The desired access</param>
        /// <returns>The stream to the opened file</returns>
        public override Stream Open(string path, FileMode mode, FileAccess access)
        {
            if (mode != FileMode.Open)
            {
                throw new NotImplementedException("No support for creating files (yet)");
            }

            Directory parent;
            DirectoryEntry dirEntry = FindFile(_rootDir, path, out parent);
            if (dirEntry == null)
            {
                throw new FileNotFoundException("Could not locate file", path);
            }

            return parent.OpenFile(dirEntry.NormalizedName, mode, access);
        }

        /// <summary>
        /// Creates a directory.
        /// </summary>
        /// <param name="path">The directory to create.</param>
        public void CreateDirectory(string path)
        {
            string[] pathElements = path.Split(new char[]{'\\'}, StringSplitOptions.RemoveEmptyEntries);

            Directory focusDir = _rootDir;

            for (int i = 0; i < pathElements.Length; ++i)
            {
                string normalizedName = FatUtilities.NormalizeFileName(pathElements[i]);
                Directory child = focusDir.GetChildDirectory(normalizedName);
                if (child == null)
                {
                    child = focusDir.CreateChildDirectory(normalizedName);
                }
                focusDir = child;
            }
        }

        private void Initialize(Stream data)
        {
            _data = data;
            data.Position = 0;
            _bootSector = Utilities.ReadSector(data);

            _type = DetectFATType(_bootSector);

            ReadBPB();

            LoadFAT();

            LoadClusterReader();

            LoadRootDirectory();
        }

        internal FileAllocationTable FAT
        {
            get { return _fat; }
        }

        private void LoadClusterReader()
        {
            int rootDirSectors = ((_bpbRootEntCnt * 32) + (_bpbBytesPerSec - 1)) / _bpbBytesPerSec;
            int firstDataSector = (int)(_bpbRsvdSecCnt + (_bpbNumFATs * FATSize) + rootDirSectors);
            _clusterReader = new ClusterReader(_data, firstDataSector, _bpbSecPerClus, _bpbBytesPerSec);
        }

        private void LoadRootDirectory()
        {
            Stream fatStream;
            if (_type != FatType.FAT32)
            {
                fatStream = new SubStream(_data, (_bpbRsvdSecCnt + (_bpbNumFATs * _bpbFATSz16)) * _bpbBytesPerSec, _bpbRootEntCnt * 32);
            }
            else
            {
                fatStream = new ClusterStream(FileAccess.Read, _clusterReader, _fat, _bpbRootClus, uint.MaxValue);
            }
            _rootDir = new Directory(this, fatStream);
        }

        private void LoadFAT()
        {
            int fatStart;
            if (_type == FatType.FAT32)
            {
                fatStart = (int)((_bpbRsvdSecCnt + (ActiveFAT * FATSize)) * BytesPerSector);
            }
            else
            {
                fatStart = _bpbRsvdSecCnt * BytesPerSector;
            }
            _fat = new FileAllocationTable(_type, _data, fatStart, (int)(FATSize * BytesPerSector));
        }

        private void ReadBPB()
        {
            _bpbOEMName = Encoding.ASCII.GetString(_bootSector, 3, 8).TrimEnd('\0');
            _bpbBytesPerSec = BitConverter.ToUInt16(_bootSector, 11);
            _bpbSecPerClus = _bootSector[13];
            _bpbRsvdSecCnt = BitConverter.ToUInt16(_bootSector, 14);
            _bpbNumFATs = _bootSector[16];
            _bpbRootEntCnt = BitConverter.ToUInt16(_bootSector, 17);
            _bpbTotSec16 = BitConverter.ToUInt16(_bootSector, 19);
            _bpbMedia = _bootSector[21];
            _bpbFATSz16 = BitConverter.ToUInt16(_bootSector, 22);
            _bpbSecPerTrk = BitConverter.ToUInt16(_bootSector, 24);
            _bpbNumHeads = BitConverter.ToUInt16(_bootSector, 26);
            _bpbHiddSec = BitConverter.ToUInt32(_bootSector, 28);
            _bpbTotSec32 = BitConverter.ToUInt32(_bootSector, 32);

            if (_type != FatType.FAT32)
            {
                ReadBS(36);
            }
            else
            {
                _bpbFATSz32 = BitConverter.ToUInt32(_bootSector, 36);
                _bpbExtFlags = BitConverter.ToUInt16(_bootSector, 40);
                _bpbFSVer = BitConverter.ToUInt16(_bootSector, 42);
                _bpbRootClus = BitConverter.ToUInt32(_bootSector, 44);
                _bpbFSInfo = BitConverter.ToUInt16(_bootSector, 48);
                _bpbBkBootSec = BitConverter.ToUInt16(_bootSector, 50);
                ReadBS(64);
            }
        }

        private void ReadBS(int offset)
        {
            _bsDrvNum = _bootSector[offset];
            _bsBootSig = _bootSector[offset + 2];
            _bsVolId = BitConverter.ToUInt32(_bootSector, offset + 3);
            _bsVolLab = Encoding.ASCII.GetString(_bootSector, offset + 7, 11);
            _bsFilSysType = Encoding.ASCII.GetString(_bootSector, offset + 18, 8);
        }

        private static FatType DetectFATType(byte[] bpb)
        {
            uint bpbBytesPerSec = BitConverter.ToUInt16(bpb, 11);
            uint bpbRootEntCnt = BitConverter.ToUInt16(bpb, 17);
            uint bpbFATSz16 = BitConverter.ToUInt16(bpb, 22);
            uint bpbFATSz32 = BitConverter.ToUInt32(bpb, 36);
            uint bpbTotSec16 = BitConverter.ToUInt16(bpb, 19);
            uint bpbTotSec32 = BitConverter.ToUInt32(bpb, 32);
            uint bpbResvdSecCnt = BitConverter.ToUInt16(bpb, 14);
            uint bpbNumFATs = bpb[16];
            uint bpbSecPerClus = bpb[13];

            uint rootDirSectors = ((bpbRootEntCnt * 32) + bpbBytesPerSec - 1) / bpbBytesPerSec;
            uint fatSz = (bpbFATSz16 != 0) ? (uint)bpbFATSz16 : bpbFATSz32;
            uint totalSec = (bpbTotSec16 != 0) ? (uint)bpbTotSec16 : bpbTotSec32;

            uint dataSec = totalSec - (bpbResvdSecCnt + (bpbNumFATs * fatSz) + rootDirSectors);
            uint countOfClusters = dataSec / bpbSecPerClus;

            if (countOfClusters < 4085)
            {
                return FatType.FAT12;
            }
            else if (countOfClusters < 65525)
            {
                return FatType.FAT16;
            }
            else
            {
                return FatType.FAT32;
            }
        }

        internal Stream OpenExistingStream(FileMode mode, FileAccess access, uint firstCluster, uint length)
        {
            if (mode == FileMode.Create || mode == FileMode.CreateNew)
            {
                throw new ArgumentOutOfRangeException("mode", "Attempt to use a Create mode on an existing stream");
            }

            ClusterStream fs = new ClusterStream(access, _clusterReader, _fat, firstCluster, length);

            if (mode == FileMode.Append)
            {
                fs.Seek(0, SeekOrigin.End);
            }
            if (mode == FileMode.Truncate)
            {
                fs.SetLength(0);
            }

            return fs;
        }

        internal DateTime ConvertToUtc(DateTime dateTime)
        {
            return TimeZoneInfo.ConvertTimeToUtc(dateTime, _timeZone);
        }

        internal DateTime ConvertFromUtc(DateTime dateTime)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(dateTime, _timeZone);
        }

        internal Stream OpenFile(Directory dir, string name, FileAccess fileAccess)
        {
            return new FatFileStream(dir, name, fileAccess, _clusterReader, _fat);
        }

        internal Directory GetDirectory(string path)
        {
            Directory parent;
            DirectoryEntry entry;

            if (string.IsNullOrEmpty(path) || path == "\\")
            {
                return _rootDir;
            }

            entry = FindFile(_rootDir, path, out parent);
            if (entry != null)
            {
                return GetDirectory(entry, parent);
            }
            else
            {
                return null;
            }
        }

        internal Directory GetDirectory(DirectoryEntry dirEntry, Directory parent)
        {
            if (dirEntry == null)
            {
                return _rootDir;
            }

            // If we have this one cached, return it
            Directory result;
            if (_dirCache.TryGetValue(dirEntry.FirstCluster, out result))
            {
                return result;
            }

            // Not cached - create a new one.
            result = new Directory(this, parent, dirEntry);
            _dirCache.Add(dirEntry.FirstCluster, result);
            return result;
        }

        internal DirectoryEntry GetDirectoryEntry(string _path)
        {
            Directory parent;

            return FindFile(_rootDir, _path, out parent);
        }

        private DirectoryEntry FindFile(Directory dir, string path, out Directory parent)
        {
            string[] pathElements = path.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            return FindFile(dir, pathElements, 0, out parent);
        }

        private DirectoryEntry FindFile(Directory dir, string[] pathEntries, int pathOffset, out Directory parent)
        {
            DirectoryEntry entry;

            entry = dir.GetEntry(FatUtilities.NormalizeFileName(pathEntries[pathOffset]));
            if (entry != null)
            {
                if (pathOffset == pathEntries.Length - 1)
                {
                    parent = dir;
                    return entry;
                }
                else
                {
                    return FindFile(GetDirectory(entry, dir), pathEntries, pathOffset + 1, out parent);
                }
            }
            else
            {
                parent = null;
                return null;
            }
        }
    }
}
