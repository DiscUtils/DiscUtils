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
using DiscUtils.Bcd;
using DiscUtils.Common;
using DiscUtils.Ntfs;
using DiscUtils.Partitions;
using DiscUtils.Registry;

namespace OSClone
{
    class Program
    {
        // Shared to avoid continual re-allocation of a large buffer
        private static byte[] s_copyBuffer = new byte[10 * 1024 * 1024];

        private static string[] s_excludedFiles = new string[]
        {
            @"\PAGEFILE.SYS", @"\HIBERFIL.SYS", @"\SYSTEM VOLUME INFORMATION"
        };

        private static CommandLineParameter _sourceFile;
        private static CommandLineParameter _destFile;
        private static CommandLineSwitch _outFormat;
        private static CommandLineSwitch _sizeSwitch;
        private static CommandLineSwitch _labelSwitch;
        private static CommandLineSwitch _userName;
        private static CommandLineSwitch _password;
        private static CommandLineSwitch _helpSwitch;
        private static CommandLineSwitch _quietSwitch;

        static void Main(string[] args)
        {
            _sourceFile = new CommandLineParameter("in_file", "The disk image containing the Operating System image to be cloned.", false);
            _destFile = new CommandLineParameter("out_file", "The path to the output disk image.", false);
            _outFormat = new CommandLineSwitch("of", "outputFormat", "format", "The type of disk to output, one of RAW, VMDK-fixed, VMDK-dynamic, VMDK-vmfsFixed, VMDK-vmfsDynamic, VHD-fixed, VHD-dynamic, VDI-dynamic, VDI-fixed or iSCSI.");
            _sizeSwitch = new CommandLineSwitch("sz", "size", "size", "The size of the output disk.  Use B, KB, MB, GB to specify units (units default to bytes if not specified).");
            _labelSwitch = new CommandLineSwitch("l", "label", "name", "The volume label for the NTFS file system created.");
            _userName = new CommandLineSwitch("u", "user", "user_name", "If using an iSCSI source or target, optionally use this parameter to specify the user name to authenticate with.  If this parameter is specified without a password, you will be prompted to supply the password.");
            _password = new CommandLineSwitch("pw", "password", "secret", "If using an iSCSI source or target, optionally use this parameter to specify the password to authenticate with.");
            _helpSwitch = new CommandLineSwitch(new string[] { "h", "?" }, "help", null, "Show this help.");
            _quietSwitch = new CommandLineSwitch("q", "quiet", null, "Run quietly.");

            CommandLineParser parser = new CommandLineParser("OSClone");
            parser.AddParameter(_sourceFile);
            parser.AddParameter(_destFile);
            parser.AddSwitch(_outFormat);
            parser.AddSwitch(_sizeSwitch);
            parser.AddSwitch(_labelSwitch);
            parser.AddSwitch(_userName);
            parser.AddSwitch(_password);
            parser.AddSwitch(_helpSwitch);
            parser.AddSwitch(_quietSwitch);

            bool parseResult = parser.Parse(args);

            if (!_quietSwitch.IsPresent)
            {
                Utilities.ShowHeader(typeof(Program));
            }

            if (_helpSwitch.IsPresent || !parseResult)
            {
                parser.DisplayHelp();
                Environment.ExitCode = 1;
                return;
            }

            if (!_sizeSwitch.IsPresent)
            {
                parser.DisplayHelp();
                Environment.ExitCode = 1;
                return;
            }

            if (!_outFormat.IsPresent)
            {
                parser.DisplayHelp();
                Environment.ExitCode = 1;
                return;
            }

            long diskSize;
            if (!Utilities.TryParseDiskSize(_sizeSwitch.Value, out diskSize))
            {
                parser.DisplayHelp();
                Environment.ExitCode = 1;
                return;
            }

            string user = _userName.IsPresent ? _userName.Value : null;
            string password = _password.IsPresent ? _password.Value : null;
            string label = _labelSwitch.IsPresent ? _labelSwitch.Value : "New Volume";

            using (VirtualDisk sourceDisk = Utilities.OpenDisk(_sourceFile.Value, FileAccess.Read, user, password))
            using (VirtualDisk destDisk = Utilities.OpenOutputDisk(_outFormat.Value, _destFile.Value, diskSize, null, user, password))
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
                                    if (elem.Format == DiscUtils.Bcd.ElementFormat.Device)
                                    {
                                        Console.WriteLine(obj.FriendlyName + ":" + elem.Value);
                                        elem.Value = DiscUtils.Bcd.ElementValue.FromVolume(elem.Value.ParentObject, volMgr.GetPhysicalVolumes()[0]);
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
                    if(!IsExcluded(dir))
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
