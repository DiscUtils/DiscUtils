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
using DiscUtils;
using DiscUtils.BootConfig;
using DiscUtils.Common;
using DiscUtils.Ntfs;
using DiscUtils.Partitions;
using DiscUtils.Registry;

namespace OSClone
{
    class Program : ProgramBase
    {
        // Shared to avoid continual re-allocation of a large buffer
        private static byte[] s_copyBuffer = new byte[10 * 1024 * 1024];

        private static string[] s_excludedFiles = new string[]
        {
            @"\PAGEFILE.SYS", @"\HIBERFIL.SYS", @"\SYSTEM VOLUME INFORMATION"
        };

        private CommandLineParameter _sourceFile;
        private CommandLineParameter _destFile;
        private CommandLineSwitch _labelSwitch;

        static void Main(string[] args)
        {
            Program program = new Program();
            program.Run(args);
        }

        protected override StandardSwitches DefineCommandLine(CommandLineParser parser)
        {
            _sourceFile = FileOrUriParameter("in_file", "The disk image containing the Operating System image to be cloned.", false);
            _destFile = FileOrUriParameter("out_file", "The path to the output disk image.", false);
            _labelSwitch = new CommandLineSwitch("l", "label", "name", "The volume label for the NTFS file system created.");

            parser.AddParameter(_sourceFile);
            parser.AddParameter(_destFile);
            parser.AddSwitch(_labelSwitch);

            return StandardSwitches.OutputFormat | StandardSwitches.UserAndPassword | StandardSwitches.DiskSize;
        }

        protected override void DoRun()
        {
            using (VirtualDisk sourceDisk = VirtualDisk.OpenDisk(_sourceFile.Value, FileAccess.Read, UserName, Password))
            using (VirtualDisk destDisk = VirtualDisk.CreateDisk(OutputDiskType, OutputDiskVariant, _destFile.Value, DiskSize, null, UserName, Password, null))
            {
                // Copy the MBR from the source disk, and invent a new signature for this new disk
                destDisk.SetMasterBootRecord(sourceDisk.GetMasterBootRecord());
                destDisk.Signature = new Random().Next();

                NtfsFileSystem sourceNtfs = new NtfsFileSystem(sourceDisk.Partitions[0].Open());

                // Copy the OS boot code into memory, so we can apply it when formatting the new disk
                byte[] bootCode;
                using (Stream bootStream = sourceNtfs.OpenFile("$Boot", FileMode.Open, FileAccess.Read))
                {
                    bootCode = new byte[bootStream.Length];
                    int totalRead = 0;
                    while (totalRead < bootCode.Length)
                    {
                        totalRead += bootStream.Read(bootCode, totalRead, bootCode.Length - totalRead);
                    }
                }

                // Partition the new disk with a single NTFS partition
                BiosPartitionTable pt = BiosPartitionTable.Initialize(destDisk, WellKnownPartitionType.WindowsNtfs);
                VolumeManager volMgr = new VolumeManager(destDisk);

                string label = _labelSwitch.IsPresent ? _labelSwitch.Value : sourceNtfs.VolumeLabel;
                using (NtfsFileSystem destNtfs = NtfsFileSystem.Format(volMgr.GetLogicalVolumes()[0], label, bootCode))
                {
                    destNtfs.SetSecurity(@"\", sourceNtfs.GetSecurity(@"\"));

                    sourceNtfs.NtfsOptions.HideHiddenFiles = false;
                    sourceNtfs.NtfsOptions.HideSystemFiles = false;
                    CopyFiles(sourceNtfs, destNtfs, @"\", true);

                    if (destNtfs.FileExists(@"\boot\BCD"))
                    {
                        // Force all boot entries in the BCD to point to the newly created NTFS partition - does _not_ cope with
                        // complex multi-volume / multi-boot scenarios at all.
                        using (Stream bcdStream = destNtfs.OpenFile(@"\boot\BCD", FileMode.Open, FileAccess.ReadWrite))
                        {
                            RegistryHive hive = new RegistryHive(bcdStream);
                            Store store = new Store(hive.Root);
                            foreach (var obj in store.Objects)
                            {
                                foreach (var elem in obj.Elements)
                                {
                                    if (elem.Format == DiscUtils.BootConfig.ElementFormat.Device)
                                    {
                                        elem.Value = DiscUtils.BootConfig.ElementValue.ForDevice(elem.Value.ParentObject, volMgr.GetPhysicalVolumes()[0]);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void CopyFiles(NtfsFileSystem sourceNtfs, NtfsFileSystem destNtfs, string path, bool subs)
        {
            if (subs)
            {
                foreach (var dir in sourceNtfs.GetDirectories(path))
                {
                    if (!IsExcluded(dir))
                    {
                        destNtfs.CreateDirectory(dir);

                        FileAttributes fileAttrs = sourceNtfs.GetAttributes(dir);
                        if ((fileAttrs & FileAttributes.ReparsePoint) != 0)
                        {
                            destNtfs.SetReparsePoint(dir, sourceNtfs.GetReparsePoint(dir));
                        }

                        destNtfs.SetAttributes(dir, fileAttrs);

                        destNtfs.SetSecurity(dir, sourceNtfs.GetSecurity(dir));

                        CopyFiles(sourceNtfs, destNtfs, dir, subs);
                    }
                }
            }

            foreach (var file in sourceNtfs.GetFiles(path))
            {
                Console.WriteLine(file);
                CopyFile(sourceNtfs, destNtfs, file);
            }
        }


        private static void CopyFile(NtfsFileSystem sourceNtfs, NtfsFileSystem destNtfs, string path)
        {
            if (IsExcluded(path))
            {
                return;
            }

            using (Stream s = sourceNtfs.OpenFile(path, FileMode.Open, FileAccess.Read))
            using (Stream d = destNtfs.OpenFile(path, FileMode.Create, FileAccess.ReadWrite))
            {
                d.SetLength(s.Length);
                int numRead = s.Read(s_copyBuffer, 0, s_copyBuffer.Length);
                while (numRead > 0)
                {
                    d.Write(s_copyBuffer, 0, numRead);
                    numRead = s.Read(s_copyBuffer, 0, s_copyBuffer.Length);
                }
            }

            destNtfs.SetAttributes(path, sourceNtfs.GetAttributes(path));
            destNtfs.SetSecurity(path, sourceNtfs.GetSecurity(path));
        }


        private static bool IsExcluded(string path)
        {
            string pathUpper = path.ToUpperInvariant();

            for (int i = 0; i < s_excludedFiles.Length; ++i)
            {
                if (pathUpper == s_excludedFiles[i])
                {
                    return true;
                }
            }

            return false;
        }
    }
}
