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
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using DiscUtils.Streams;

namespace DiscUtils.Ntfs
{
    internal class NtfsFormatter
    {
        private long _bitmapCluster;
        private int _clusterSize;

        private NtfsContext _context;
        private int _indexBufferSize;
        private long _mftCluster;
        private long _mftMirrorCluster;
        private int _mftRecordSize;

        public byte[] BootCode { get; set; }

        public SecurityIdentifier ComputerAccount { get; set; }

        public Geometry DiskGeometry { get; set; }

        public long FirstSector { get; set; }

        public string Label { get; set; }

        public long SectorCount { get; set; }

        public NtfsFileSystem Format(Stream stream)
        {
            _context = new NtfsContext();
            _context.Options = new NtfsOptions();
            _context.RawStream = stream;
            _context.AttributeDefinitions = new AttributeDefinitions();

            string localAdminString = ComputerAccount == null
                ? "LA"
                : new SecurityIdentifier(WellKnownSidType.AccountAdministratorSid, ComputerAccount).ToString();

            using (new NtfsTransaction())
            {
                _clusterSize = 4096;
                _mftRecordSize = 1024;
                _indexBufferSize = 4096;

                long totalClusters = (SectorCount - 1) * Sizes.Sector / _clusterSize;

                // Allocate a minimum of 8KB for the boot loader, but allow for more
                int numBootClusters =
                    MathUtilities.Ceil(Math.Max((int)(8 * Sizes.OneKiB), BootCode == null ? 0 : BootCode.Length),
                        _clusterSize);

                // Place MFT mirror in the middle of the volume
                _mftMirrorCluster = totalClusters / 2;
                uint numMftMirrorClusters = 1;

                // The bitmap is also near the middle
                _bitmapCluster = _mftMirrorCluster + 13;
                int numBitmapClusters = (int)MathUtilities.Ceil(totalClusters / 8, _clusterSize);

                // The MFT bitmap goes 'near' the start - approx 10% in - but ensure we avoid the bootloader
                long mftBitmapCluster = Math.Max(3 + totalClusters / 10, numBootClusters);
                int numMftBitmapClusters = 1;

                // The MFT follows it's bitmap
                _mftCluster = mftBitmapCluster + numMftBitmapClusters;
                int numMftClusters = 8;

                if (_mftCluster + numMftClusters > _mftMirrorCluster
                    || _bitmapCluster + numBitmapClusters >= totalClusters)
                {
                    throw new IOException("Unable to determine initial layout of NTFS metadata - disk may be too small");
                }

                CreateBiosParameterBlock(stream, numBootClusters * _clusterSize);

                _context.Mft = new MasterFileTable(_context);
                File mftFile = _context.Mft.InitializeNew(_context, mftBitmapCluster, (ulong)numMftBitmapClusters,
                                           _mftCluster, (ulong)numMftClusters);

                File bitmapFile = CreateFixedSystemFile(MasterFileTable.BitmapIndex, _bitmapCluster,
                    (ulong)numBitmapClusters, true);
                _context.ClusterBitmap = new ClusterBitmap(bitmapFile);
                _context.ClusterBitmap.MarkAllocated(0, numBootClusters);
                _context.ClusterBitmap.MarkAllocated(_bitmapCluster, numBitmapClusters);
                _context.ClusterBitmap.MarkAllocated(mftBitmapCluster, numMftBitmapClusters);
                _context.ClusterBitmap.MarkAllocated(_mftCluster, numMftClusters);
                _context.ClusterBitmap.SetTotalClusters(totalClusters);
                bitmapFile.UpdateRecordInMft();

                File mftMirrorFile = CreateFixedSystemFile(MasterFileTable.MftMirrorIndex, _mftMirrorCluster,
                    numMftMirrorClusters, true);

                File logFile = CreateSystemFile(MasterFileTable.LogFileIndex);
                using (Stream s = logFile.OpenStream(AttributeType.Data, null, FileAccess.ReadWrite))
                {
                    s.SetLength(Math.Min(Math.Max(2 * Sizes.OneMiB, totalClusters / 500 * _clusterSize),
                        64 * Sizes.OneMiB));
                    byte[] buffer = new byte[1024 * 1024];
                    for (int i = 0; i < buffer.Length; ++i)
                    {
                        buffer[i] = 0xFF;
                    }

                    long totalWritten = 0;
                    while (totalWritten < s.Length)
                    {
                        int toWrite = (int)Math.Min(s.Length - totalWritten, buffer.Length);
                        s.Write(buffer, 0, toWrite);
                        totalWritten += toWrite;
                    }
                }

                File volumeFile = CreateSystemFile(MasterFileTable.VolumeIndex);
                NtfsStream volNameStream = volumeFile.CreateStream(AttributeType.VolumeName, null);
                volNameStream.SetContent(new VolumeName(Label ?? "New Volume"));
                NtfsStream volInfoStream = volumeFile.CreateStream(AttributeType.VolumeInformation, null);
                volInfoStream.SetContent(new VolumeInformation(3, 1, VolumeInformationFlags.None));
                SetSecurityAttribute(volumeFile, "O:" + localAdminString + "G:BAD:(A;;0x12019f;;;SY)(A;;0x12019f;;;BA)");
                volumeFile.UpdateRecordInMft();

                _context.GetFileByIndex =
                    delegate(long index) { return new File(_context, _context.Mft.GetRecord(index, false)); };
                _context.AllocateFile =
                    delegate(FileRecordFlags frf) { return new File(_context, _context.Mft.AllocateRecord(frf, false)); };

                File attrDefFile = CreateSystemFile(MasterFileTable.AttrDefIndex);
                _context.AttributeDefinitions.WriteTo(attrDefFile);
                SetSecurityAttribute(attrDefFile, "O:" + localAdminString + "G:BAD:(A;;FR;;;SY)(A;;FR;;;BA)");
                attrDefFile.UpdateRecordInMft();

                File bootFile = CreateFixedSystemFile(MasterFileTable.BootIndex, 0, (uint)numBootClusters, false);
                SetSecurityAttribute(bootFile, "O:" + localAdminString + "G:BAD:(A;;FR;;;SY)(A;;FR;;;BA)");
                bootFile.UpdateRecordInMft();

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

                File objIdFile = File.CreateNew(_context, FileRecordFlags.IsMetaFile | FileRecordFlags.HasViewIndex,
                    FileAttributeFlags.None);
                objIdFile.RemoveStream(objIdFile.GetStream(AttributeType.Data, null));
                objIdFile.CreateIndex("$O", 0, AttributeCollationRule.MultipleUnsignedLongs);
                objIdFile.UpdateRecordInMft();

                File reparseFile = File.CreateNew(_context, FileRecordFlags.IsMetaFile | FileRecordFlags.HasViewIndex,
                    FileAttributeFlags.None);
                reparseFile.CreateIndex("$R", 0, AttributeCollationRule.MultipleUnsignedLongs);
                reparseFile.UpdateRecordInMft();

                File quotaFile = File.CreateNew(_context, FileRecordFlags.IsMetaFile | FileRecordFlags.HasViewIndex,
                    FileAttributeFlags.None);
                Quotas.Initialize(quotaFile);

                Directory extendDir = CreateSystemDirectory(MasterFileTable.ExtendIndex);
                extendDir.AddEntry(objIdFile, "$ObjId", FileNameNamespace.Win32AndDos);
                extendDir.AddEntry(reparseFile, "$Reparse", FileNameNamespace.Win32AndDos);
                extendDir.AddEntry(quotaFile, "$Quota", FileNameNamespace.Win32AndDos);
                extendDir.UpdateRecordInMft();

                Directory rootDir = CreateSystemDirectory(MasterFileTable.RootDirIndex);
                rootDir.AddEntry(mftFile, "$MFT", FileNameNamespace.Win32AndDos);
                rootDir.AddEntry(mftMirrorFile, "$MFTMirr", FileNameNamespace.Win32AndDos);
                rootDir.AddEntry(logFile, "$LogFile", FileNameNamespace.Win32AndDos);
                rootDir.AddEntry(volumeFile, "$Volume", FileNameNamespace.Win32AndDos);
                rootDir.AddEntry(attrDefFile, "$AttrDef", FileNameNamespace.Win32AndDos);
                rootDir.AddEntry(rootDir, ".", FileNameNamespace.Win32AndDos);
                rootDir.AddEntry(bitmapFile, "$Bitmap", FileNameNamespace.Win32AndDos);
                rootDir.AddEntry(bootFile, "$Boot", FileNameNamespace.Win32AndDos);
                rootDir.AddEntry(badClusFile, "$BadClus", FileNameNamespace.Win32AndDos);
                rootDir.AddEntry(secureFile, "$Secure", FileNameNamespace.Win32AndDos);
                rootDir.AddEntry(upcaseFile, "$UpCase", FileNameNamespace.Win32AndDos);
                rootDir.AddEntry(extendDir, "$Extend", FileNameNamespace.Win32AndDos);
                SetSecurityAttribute(rootDir,
                    "O:" + localAdminString +
                    "G:BUD:(A;OICI;FA;;;BA)(A;OICI;FA;;;SY)(A;OICIIO;GA;;;CO)(A;OICI;0x1200a9;;;BU)(A;CI;LC;;;BU)(A;CIIO;DC;;;BU)(A;;0x1200a9;;;WD)");
                rootDir.UpdateRecordInMft();

                // A number of records are effectively 'reserved'
                for (long i = MasterFileTable.ExtendIndex + 1; i <= 15; i++)
                {
                    File f = CreateSystemFile(i);
                    SetSecurityAttribute(f,
                        "O:S-1-5-21-1708537768-746137067-1060284298-1003G:BAD:(A;;0x12019f;;;SY)(A;;0x12019f;;;BA)");
                    f.UpdateRecordInMft();
                }
            }

            // XP-style security permissions setup
            NtfsFileSystem ntfs = new NtfsFileSystem(stream);

            ntfs.SetSecurity(@"$MFT",
                new RawSecurityDescriptor("O:" + localAdminString + "G:BAD:(A;;FR;;;SY)(A;;FR;;;BA)"));
            ntfs.SetSecurity(@"$MFTMirr",
                new RawSecurityDescriptor("O:" + localAdminString + "G:BAD:(A;;FR;;;SY)(A;;FR;;;BA)"));
            ntfs.SetSecurity(@"$LogFile",
                new RawSecurityDescriptor("O:" + localAdminString + "G:BAD:(A;;FR;;;SY)(A;;FR;;;BA)"));
            ntfs.SetSecurity(@"$Bitmap",
                new RawSecurityDescriptor("O:" + localAdminString + "G:BAD:(A;;FR;;;SY)(A;;FR;;;BA)"));
            ntfs.SetSecurity(@"$BadClus",
                new RawSecurityDescriptor("O:" + localAdminString + "G:BAD:(A;;FR;;;SY)(A;;FR;;;BA)"));
            ntfs.SetSecurity(@"$UpCase",
                new RawSecurityDescriptor("O:" + localAdminString + "G:BAD:(A;;FR;;;SY)(A;;FR;;;BA)"));
            ntfs.SetSecurity(@"$Secure",
                new RawSecurityDescriptor("O:" + localAdminString + "G:BAD:(A;;0x12019f;;;SY)(A;;0x12019f;;;BA)"));
            ntfs.SetSecurity(@"$Extend",
                new RawSecurityDescriptor("O:" + localAdminString + "G:BAD:(A;;0x12019f;;;SY)(A;;0x12019f;;;BA)"));
            ntfs.SetSecurity(@"$Extend\$Quota",
                new RawSecurityDescriptor("O:" + localAdminString + "G:BAD:(A;;0x12019f;;;SY)(A;;0x12019f;;;BA)"));
            ntfs.SetSecurity(@"$Extend\$ObjId",
                new RawSecurityDescriptor("O:" + localAdminString + "G:BAD:(A;;0x12019f;;;SY)(A;;0x12019f;;;BA)"));
            ntfs.SetSecurity(@"$Extend\$Reparse",
                new RawSecurityDescriptor("O:" + localAdminString + "G:BAD:(A;;0x12019f;;;SY)(A;;0x12019f;;;BA)"));

            ntfs.CreateDirectory("System Volume Information");
            ntfs.SetAttributes("System Volume Information",
                FileAttributes.Hidden | FileAttributes.System | FileAttributes.Directory);
            ntfs.SetSecurity("System Volume Information", new RawSecurityDescriptor("O:BAG:SYD:(A;OICI;FA;;;SY)"));

            using (
                Stream s = ntfs.OpenFile(@"System Volume Information\MountPointManagerRemoteDatabase", FileMode.Create)) {}

            ntfs.SetAttributes(@"System Volume Information\MountPointManagerRemoteDatabase",
                FileAttributes.Hidden | FileAttributes.System | FileAttributes.Archive);
            ntfs.SetSecurity(@"System Volume Information\MountPointManagerRemoteDatabase",
                new RawSecurityDescriptor("O:BAG:SYD:(A;;FA;;;SY)"));
            return ntfs;
        }

        private static void SetSecurityAttribute(File file, string secDesc)
        {
            NtfsStream rootSecurityStream = file.CreateStream(AttributeType.SecurityDescriptor, null);
            SecurityDescriptor sd = new SecurityDescriptor();
            sd.Descriptor = new RawSecurityDescriptor(secDesc);
            rootSecurityStream.SetContent(sd);
        }

        private File CreateFixedSystemFile(long mftIndex, long firstCluster, ulong numClusters, bool wipe)
        {
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

            file.CreateStream(AttributeType.Data, null, firstCluster, numClusters, (uint)bpb.BytesPerCluster);

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
            FileRecord fileRec = _context.Mft.AllocateRecord((uint)mftIndex, flags);
            fileRec.SequenceNumber = (ushort)mftIndex;

            File file = new File(_context, fileRec);

            StandardInformation.InitializeNewFile(file,
                FileAttributeFlags.Hidden | FileAttributeFlags.System | FileRecord.ConvertFlags(flags));

            file.CreateStream(AttributeType.Data, null);

            file.UpdateRecordInMft();

            return file;
        }

        private Directory CreateSystemDirectory(long mftIndex)
        {
            FileRecord fileRec = _context.Mft.AllocateRecord((uint)mftIndex, FileRecordFlags.None);
            fileRec.Flags = FileRecordFlags.InUse | FileRecordFlags.IsDirectory;
            fileRec.SequenceNumber = (ushort)mftIndex;

            Directory dir = new Directory(_context, fileRec);

            StandardInformation.InitializeNewFile(dir, FileAttributeFlags.Hidden | FileAttributeFlags.System);

            dir.CreateIndex("$I30", AttributeType.FileName, AttributeCollationRule.Filename);

            dir.UpdateRecordInMft();

            return dir;
        }

        private void CreateBiosParameterBlock(Stream stream, int bootFileSize)
        {
            byte[] bootSectors = new byte[bootFileSize];

            if (BootCode != null)
            {
                Array.Copy(BootCode, 0, bootSectors, 0, BootCode.Length);
            }

            BiosParameterBlock bpb = BiosParameterBlock.Initialized(DiskGeometry, _clusterSize, (uint)FirstSector,
                SectorCount, _mftRecordSize, _indexBufferSize);
            bpb.MftCluster = _mftCluster;
            bpb.MftMirrorCluster = _mftMirrorCluster;
            bpb.ToBytes(bootSectors, 0);

            // Primary goes at the start of the partition
            stream.Position = 0;
            stream.Write(bootSectors, 0, bootSectors.Length);

            // Backup goes at the end of the data in the partition
            stream.Position = (SectorCount - 1) * Sizes.Sector;
            stream.Write(bootSectors, 0, Sizes.Sector);

            _context.BiosParameterBlock = bpb;
        }
    }
}