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
using System.IO;
using System.Text;

namespace DiscUtils.Fat
{

    public class FatFileSystem : DiscFileSystem
    {
        private Stream _data;
        private byte[] _bootSector;
        private FileAllocationTable _fat;
        private ClusterReader _clusterReader;
        private FatDirectoryInfo _rootDir;


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

        public static readonly DateTime Epoch = new DateTime(1980, 1, 1);


        public FatFileSystem(Stream data)
        {
            this._data = data;
            data.Position = 0;
            _bootSector = Utilities.ReadSector(data);

            _type = DetectFATType(_bootSector);

            ReadBPB();

            LoadFAT();

            LoadClusterReader();

            LoadRootDirectory();
        }

        public FatType FATVariant
        {
            get { return _type; }
        }

        public string FriendlyName
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

        public string OEMName
        {
            get { return _bpbOEMName; }
        }

        public ushort BytesPerSector
        {
            get { return _bpbBytesPerSec; }
        }

        public byte SectorsPerCluster
        {
            get { return _bpbSecPerClus; }
        }

        public ushort ReservedSectorCount
        {
            get { return _bpbRsvdSecCnt; }
        }

        public byte NumFATs
        {
            get { return _bpbNumFATs; }
        }

        public ushort MaxRootDirectoryEntries
        {
            get { return _bpbRootEntCnt; }
        }

        public uint TotalSectors
        {
            get { return (_bpbTotSec16 != 0) ? _bpbTotSec16 : _bpbTotSec32; }
        }

        public byte Media
        {
            get { return _bpbMedia; }
        }

        public uint FATSize
        {
            get { return (_bpbFATSz16 != 0) ? _bpbFATSz16 : _bpbFATSz32; }
        }

        public ushort SectorsPerTrack
        {
            get { return _bpbSecPerTrk; }
        }

        public ushort NumHeads
        {
            get { return _bpbNumHeads; }
        }

        public uint HiddenSectors
        {
            get { return _bpbHiddSec; }
        }

        public byte BIOSDriveNumber
        {
            get { return _bsDrvNum; }
        }

        public bool ExtendedBootSignaturePresent
        {
            get { return _bsBootSig == 0x29; }
        }

        public uint VolumeId
        {
            get { return _bsVolId; }
        }

        public string VolumeLabel
        {
            get { return _bsVolLab; }
        }

        public string FileSystemType
        {
            get { return _bsFilSysType; }
        }

        public byte ActiveFAT
        {
            get { return (byte)(((_bpbExtFlags & 0x08) != 0) ? _bpbExtFlags & 0x7 : 0); }
        }

        public bool MirrorFAT
        {
            get { return ((_bpbExtFlags & 0x08) == 0); }
        }

        public ushort Version
        {
            get { return _bpbFSVer; }
        }

        public uint RootDirectoryCluster
        {
            get { return _bpbRootClus; }
        }

        public ushort FSInfoSector
        {
            get { return _bpbFSInfo; }
        }

        public ushort BackupBootSector
        {
            get { return _bpbBkBootSec; }
        }

        public override bool CanWrite()
        {
            // Read-only (for now)
            return false;
        }

        public override DiscDirectoryInfo Root
        {
            get { return _rootDir; }
        }

        public override Stream Open(string path, FileMode mode, FileAccess access)
        {
            if (mode != FileMode.Open)
            {
                throw new NotImplementedException("No read-write support yet");
            }

            if (access != FileAccess.Read)
            {
                throw new NotImplementedException("No read-write support yet");
            }

            DirectoryEntry dirEntry = FindFile(_rootDir, path.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries), 0);
            if (dirEntry == null)
            {
                throw new FileNotFoundException("Could not locate file", path);
            }

            return OpenExistingStream(mode, dirEntry.FirstCluster, (uint)dirEntry.FileSize);
        }

        private DirectoryEntry FindFile(FatDirectoryInfo dir, string[] pathEntries, int pathOffset)
        {
            DirectoryEntry entry;

            if (dir.TryGetDirectoryEntry(FatUtilities.NormalizeFileName(pathEntries[pathOffset]), out entry))
            {
                if (pathOffset == pathEntries.Length - 1)
                {
                    return entry;
                }
                else
                {
                    Stream dirStream = new ClusterFileStream(_clusterReader, _fat, entry.FirstCluster, (uint)entry.FileSize);
                    FatDirectoryInfo dirInfo = new FatDirectoryInfo(this, dir, entry, dirStream);
                    return FindFile(dirInfo, pathEntries, pathOffset + 1);
                }
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the value of a cluster's entry in the FAT (i.e. the next cluster)
        /// </summary>
        /// <param name="cluster">The cluster to lookup</param>
        /// <returns>The next cluster, or EOC or BAD marker</returns>
        private uint GetNextCluster(uint cluster)
        {
            return _fat.GetNext(cluster);
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
                fatStream = new ClusterFileStream(_clusterReader, _fat, _bpbRootClus, uint.MaxValue);
            }
            _rootDir = new FatDirectoryInfo(this, null, null, fatStream);
        }

        private void LoadFAT()
        {
            if (_type == FatType.FAT32)
            {
                _data.Position = (_bpbRsvdSecCnt + (ActiveFAT * FATSize)) * BytesPerSector;
            }
            else
            {
                _data.Position = _bpbRsvdSecCnt * BytesPerSector;
            }
            byte[] buffer = Utilities.ReadFully(_data, (int)(FATSize * BytesPerSector));
            _fat = new FileAllocationTable(_type, buffer);
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

        internal Stream OpenExistingStream(FileMode mode, uint firstCluster, uint length)
        {
            if (mode == FileMode.Create || mode == FileMode.CreateNew)
            {
                throw new ArgumentOutOfRangeException("mode", "Attempt to use a Create mode on an existing stream");
            }

            ClusterFileStream fs = new ClusterFileStream(_clusterReader, _fat, firstCluster, length);

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
    }
}
