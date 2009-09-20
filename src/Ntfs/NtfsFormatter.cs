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
using System.Security.AccessControl;

namespace DiscUtils.Ntfs
{
    internal class NtfsFormatter
    {
        public string Label { get; set; }
        public Geometry DiskGeometry { get; set;}
        public long FirstSector { get; set; }
        public long SectorCount { get; set; }

        private int _clusterSize;
        private int _mftRecordSize;
        private int _indexBufferSize;
        private int _bitmapCluster;
        private int _mftMirrorCluster;
        private int _mftCluster;

        private NtfsContext _context;

        public NtfsFileSystem Format(Stream stream)
        {
            _context = new NtfsContext();
            _context.Options = new NtfsOptions();
            _context.RawStream = stream;
            _context.AttributeDefinitions = new AttributeDefinitions();

            using (new NtfsTransaction())
            {
                _clusterSize = 4096;
                _mftRecordSize = 1024;
                _indexBufferSize = 4096;

                long totalClusters = ((SectorCount - 1) * Sizes.Sector) / _clusterSize;

                uint numBootClusters = 1;

                _mftMirrorCluster = (int)numBootClusters + 1;
                uint numMftMirrorClusters = 1;

                _bitmapCluster = (int)Sizes.OneMiB / _clusterSize;
                int numBitmapClusters = (int)Utilities.Ceil((totalClusters / 8), _clusterSize);

                int mftBitmapCluster = _bitmapCluster + numBitmapClusters;
                int numMftBitmapClusters = 1;

                _mftCluster = mftBitmapCluster + numMftBitmapClusters;
                int numMftClusters = 64;

                CreateBiosParameterBlock(stream);


                _context.Mft = new MasterFileTable();
                File mftFile = _context.Mft.InitializeNew(_context, mftBitmapCluster, (ulong)numMftBitmapClusters, (long)_mftCluster, (ulong)numMftClusters);


                File bitmapFile = CreateFixedSystemFile(MasterFileTable.BitmapIndex, _bitmapCluster, (ulong)numBitmapClusters, true);
                _context.ClusterBitmap = new ClusterBitmap(bitmapFile);
                _context.ClusterBitmap.MarkAllocated(_bitmapCluster, numBitmapClusters);
                _context.ClusterBitmap.MarkAllocated(mftBitmapCluster, numMftBitmapClusters);
                _context.ClusterBitmap.MarkAllocated(_mftCluster, numMftClusters);
                _context.ClusterBitmap.SetTotalClusters(totalClusters);
                bitmapFile.UpdateRecordInMft();


                File mftMirrorFile = CreateFixedSystemFile(MasterFileTable.MftMirrorIndex, _mftMirrorCluster, numMftMirrorClusters, true);


                File logFile = CreateSystemFile(MasterFileTable.LogFileIndex);
                using (Stream s = logFile.OpenStream(AttributeType.Data, null, FileAccess.ReadWrite))
                {
                    s.SetLength(2 * Sizes.OneMiB);
                }


                File volumeFile = CreateSystemFile(MasterFileTable.VolumeIndex);
                NtfsStream volNameStream = volumeFile.CreateStream(AttributeType.VolumeName, null);
                volNameStream.SetContent(new VolumeName(Label));
                NtfsStream volInfoStream = volumeFile.CreateStream(AttributeType.VolumeInformation, null);
                volInfoStream.SetContent(new VolumeInformation(3, 1, VolumeInformationFlags.None));
                volumeFile.UpdateRecordInMft();


                _context.GetFileByIndex = delegate(long index) { return new File(_context, _context.Mft.GetRecord(index, false)); };
                _context.AllocateFile = delegate(FileRecordFlags frf) { return new File(_context, _context.Mft.AllocateRecord(frf)); };


                File attrDefFile = CreateSystemFile(MasterFileTable.AttrDefIndex);
                _context.AttributeDefinitions.WriteTo(attrDefFile);
                attrDefFile.UpdateRecordInMft();


                File bootFile = CreateFixedSystemFile(MasterFileTable.BootIndex, 0, numBootClusters, false);


                File badClusFile = CreateSystemFile(MasterFileTable.BadClusIndex);
                badClusFile.CreateStream(AttributeType.Data, "$Bad");
                badClusFile.UpdateRecordInMft();


                File secureFile = CreateSystemFile(MasterFileTable.SecureIndex, FileRecordFlags.HasViewIndex);
                secureFile.RemoveStream(secureFile.GetStream(AttributeType.Data, null));
                _context.SecurityDescriptors = SecurityDescriptors.Initialize(secureFile);
                secureFile.UpdateRecordInMft();


                File upcaseFile = CreateSystemFile(MasterFileTable.UpCaseIndex);
                _context.UpperCase = UpperCase.Initialize(upcaseFile);
                upcaseFile.UpdateRecordInMft();


                File objIdFile = File.CreateNew(_context, FileRecordFlags.IsMetaFile | FileRecordFlags.HasViewIndex);
                objIdFile.CreateIndex("$O", (AttributeType)0, AttributeCollationRule.MultipleUnsignedLongs);
                objIdFile.UpdateRecordInMft();


                File reparseFile = File.CreateNew(_context, FileRecordFlags.IsMetaFile | FileRecordFlags.HasViewIndex);
                reparseFile.CreateIndex("$R", (AttributeType)0, AttributeCollationRule.MultipleUnsignedLongs);
                reparseFile.UpdateRecordInMft();

                File quotaFile = File.CreateNew(_context, FileRecordFlags.IsMetaFile | FileRecordFlags.HasViewIndex);
                quotaFile.CreateIndex("$O", (AttributeType)0, AttributeCollationRule.Sid);
                quotaFile.CreateIndex("$Q", (AttributeType)0, AttributeCollationRule.UnsignedLong);

                Directory extendDir = CreateSystemDirectory(MasterFileTable.ExtendIndex);
                extendDir.AddEntry(objIdFile, "$ObjId");
                extendDir.AddEntry(reparseFile, "$Reparse");
                extendDir.AddEntry(quotaFile, "$Quota");
                extendDir.UpdateRecordInMft();


                Directory rootDir = CreateSystemDirectory(MasterFileTable.RootDirIndex);
                rootDir.AddEntry(mftFile, "$MFT");
                rootDir.AddEntry(mftMirrorFile, "$MFTMirr");
                rootDir.AddEntry(logFile, "$LogFile");
                rootDir.AddEntry(volumeFile, "$Volume");
                rootDir.AddEntry(attrDefFile, "$AttrDef");
                rootDir.AddEntry(rootDir, ".");
                rootDir.AddEntry(bitmapFile, "$Bitmap");
                rootDir.AddEntry(bootFile, "$Boot");
                rootDir.AddEntry(badClusFile, "$BadClus");
                rootDir.AddEntry(secureFile, "$Secure");
                rootDir.AddEntry(upcaseFile, "$UpCase");
                rootDir.AddEntry(extendDir, "$Extend");
                rootDir.UpdateRecordInMft();

                // A number of records are effectively 'reserved'
                for (long i = MasterFileTable.ExtendIndex + 1; i <= 15; i++)
                {
                    CreateSystemFile(i);
                }
            }

            // XP-style security permissions setup
            NtfsFileSystem ntfs = new NtfsFileSystem(stream);

            ntfs.SetSecurity(@"$MFT", new RawSecurityDescriptor("O:LAG:BAD:(A;;FR;;;SY)(A;;FR;;;BA)"));
            ntfs.SetSecurity(@"$MFTMirr", new RawSecurityDescriptor("O:LAG:BAD:(A;;FR;;;SY)(A;;FR;;;BA)"));
            ntfs.SetSecurity(@"$LogFile", new RawSecurityDescriptor("O:LAG:BAD:(A;;FR;;;SY)(A;;FR;;;BA)"));
            ntfs.SetSecurity(@"$Volume", new RawSecurityDescriptor("O:LAG:BAD:(A;;0x12019f;;;SY)(A;;0x12019f;;;BA)"));
            ntfs.SetSecurity(@"$AttrDef", new RawSecurityDescriptor("O:LAG:BAD:(A;;FR;;;SY)(A;;FR;;;BA)"));
            ntfs.SetSecurity(@".", new RawSecurityDescriptor("O:LAG:BUD:(A;OICI;FA;;;BA)(A;OICI;FA;;;SY)(A;OICIIO;GA;;;CO)(A;OICI;0x1200a9;;;BU)(A;CI;LC;;;BU)(A;CIIO;DC;;;BU)(A;;0x1200a9;;;WD)"));
            ntfs.SetSecurity(@"$Bitmap", new RawSecurityDescriptor("O:LAG:BAD:(A;;FR;;;SY)(A;;FR;;;BA)"));
            ntfs.SetSecurity(@"$BadClus", new RawSecurityDescriptor("O:LAG:BAD:(A;;FR;;;SY)(A;;FR;;;BA)"));
            ntfs.SetSecurity(@"$UpCase", new RawSecurityDescriptor("O:LAG:BAD:(A;;FR;;;SY)(A;;FR;;;BA)"));
            ntfs.SetSecurity(@"$Secure", new RawSecurityDescriptor("O:LAG:BAD:(A;;0x12019f;;;SY)(A;;0x12019f;;;BA)"));
            ntfs.SetSecurity(@"$Extend", new RawSecurityDescriptor("O:LAG:BAD:(A;;0x12019f;;;SY)(A;;0x12019f;;;BA)"));
            ntfs.SetSecurity(@"$Extend\$Quota", new RawSecurityDescriptor("O:LAG:BAD:(A;;0x12019f;;;SY)(A;;0x12019f;;;BA)"));
            ntfs.SetSecurity(@"$Extend\$ObjId", new RawSecurityDescriptor("O:LAG:BAD:(A;;0x12019f;;;SY)(A;;0x12019f;;;BA)"));
            ntfs.SetSecurity(@"$Extend\$Reparse", new RawSecurityDescriptor("O:LAG:BAD:(A;;0x12019f;;;SY)(A;;0x12019f;;;BA)"));

            ntfs.CreateDirectory("System Volume Information");
            ntfs.SetAttributes("System Volume Information", FileAttributes.Hidden | FileAttributes.System | FileAttributes.Directory);
            ntfs.SetSecurity("System Volume Information", new RawSecurityDescriptor("O:BAG:SYD:(A;OICI;FA;;;SY)"));

            using (Stream s = ntfs.OpenFile(@"System Volume Information\MountPointManagerRemoteDatabase", FileMode.Create)) { }
            ntfs.SetAttributes(@"System Volume Information\MountPointManagerRemoteDatabase", FileAttributes.Hidden | FileAttributes.System | FileAttributes.Archive);
            ntfs.SetSecurity(@"System Volume Information\MountPointManagerRemoteDatabase", new RawSecurityDescriptor("O:BAG:SYD:(A;;FA;;;SY)"));
            return ntfs;
        }

        private File CreateFixedSystemFile(long mftIndex, long firstCluster, ulong numClusters, bool wipe)
        {
            DateTime now = DateTime.UtcNow;
            BiosParameterBlock bpb = _context.BiosParameterBlock;

            if (wipe)
            {
                byte[] wipeBuffer = new byte[bpb.BytesPerCluster];
                _context.RawStream.Position = firstCluster * bpb.BytesPerCluster;
                for (ulong i = 0; i < numClusters; ++i)
                {
                    _context.RawStream.Write(wipeBuffer, 0, wipeBuffer.Length);
                }
            }

            FileRecord fileRec = _context.Mft.AllocateRecord((uint)mftIndex, FileRecordFlags.None);
            fileRec.Flags = FileRecordFlags.InUse;
            fileRec.SequenceNumber = (ushort)mftIndex;

            File file = new File(_context, fileRec);

            StandardInformation.InitializeNewFile(file, FileAttributeFlags.Hidden | FileAttributeFlags.System);

            NtfsStream dataStream = file.CreateStream(AttributeType.Data, null, firstCluster, numClusters, (uint)bpb.BytesPerCluster);

            file.UpdateRecordInMft();

            if (_context.ClusterBitmap != null)
            {
                _context.ClusterBitmap.MarkAllocated(firstCluster, (long)numClusters);
            }

            return file;
        }

        private File CreateSystemFile(long mftIndex)
        {
            return CreateSystemFile(mftIndex, FileRecordFlags.None);
        }

        private File CreateSystemFile(long mftIndex, FileRecordFlags flags)
        {
            BiosParameterBlock bpb = _context.BiosParameterBlock;

            FileRecord fileRec = _context.Mft.AllocateRecord((uint)mftIndex, flags);
            fileRec.SequenceNumber = (ushort)mftIndex;

            File file = new File(_context, fileRec);

            StandardInformation.InitializeNewFile(file, FileAttributeFlags.Hidden | FileAttributeFlags.System | FileRecord.ConvertFlags(flags));

            NtfsStream dataStream = file.CreateStream(AttributeType.Data, null);

            file.UpdateRecordInMft();

            return file;
        }

        private Directory CreateSystemDirectory(long mftIndex)
        {
            DateTime now = DateTime.UtcNow;
            BiosParameterBlock bpb = _context.BiosParameterBlock;

            FileRecord fileRec = _context.Mft.AllocateRecord((uint)mftIndex, FileRecordFlags.None);
            fileRec.Flags = FileRecordFlags.InUse | FileRecordFlags.IsDirectory;
            fileRec.SequenceNumber = (ushort)mftIndex;

            Directory dir = new Directory(_context, fileRec);

            StandardInformation.InitializeNewFile(dir, FileAttributeFlags.Hidden | FileAttributeFlags.System);

            dir.CreateIndex("$I30", AttributeType.FileName, AttributeCollationRule.Filename);

            dir.UpdateRecordInMft();

            return dir;
        }

        private void CreateBiosParameterBlock(Stream stream)
        {
            byte[] bootSectors = new byte[Sizes.Sector];
            BiosParameterBlock bpb = BiosParameterBlock.Initialized(DiskGeometry, _clusterSize, (uint)FirstSector, SectorCount, _mftRecordSize, _indexBufferSize);
            bpb.MftCluster = _mftCluster;
            bpb.MftMirrorCluster = _mftMirrorCluster;
            bpb.ToBytes(bootSectors, 0);

            // Primary goes at the start of the partition
            stream.Position = 0;
            stream.Write(bootSectors, 0, bootSectors.Length);

            // Backup goes at the end of the data in the partition
            stream.Position = (SectorCount - 1) * Sizes.Sector;
            stream.Write(bootSectors, 0, bootSectors.Length);


            _context.BiosParameterBlock = bpb;
        }

    }
}
