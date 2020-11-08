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
using DiscUtils;
using DiscUtils.Common;
using System.Text;
using DiscUtils.FileSystems;
using DiscUtils.Partitions;
using DiscUtils.Complete;
using DiscUtils.Fat;
using DiscUtils.Ntfs;
using DiscUtils.Vhd;

namespace DiskFormat
{
    class Program : ProgramBase
    {
        private CommandLineParameter _diskFile;
        private CommandLineSwitch _formatSwitch;
        private CommandLineSwitch _labelSwitch;
        private CommandLineSwitch _partitionTableSwitch;
        private CommandLineSwitch _partitionTypeSwitch;
        private CommandLineSwitch _diskType;

        static void Main(string[] args)
        {
            Program program = new Program();
            program.Run(args);
        }

        protected override StandardSwitches DefineCommandLine(CommandLineParser parser)
        {
            _diskFile = new CommandLineParameter("disk.vhd", "Path to the VHD file to format.", false);
            _formatSwitch = new CommandLineSwitch("ft", "format", "format", "The format type like FAT, NTFS, EXT, hfsp");
            _labelSwitch = new CommandLineSwitch("l", "label", "label", "The label of the volume to create");
            _partitionTableSwitch = new CommandLineSwitch("ptt", "partition table", "partition table", "If the disk needs to be have partition table, use this format (guid or bios). If present, repartition the drive");
            _partitionTypeSwitch = new CommandLineSwitch("p", "partition type", "When the disk is partitioned, use this format. If present, repartition the drive");
            _diskType = new CommandLineSwitch("dt", "disktype", "type", "Force the type of disk - use a file extension (one of " + string.Join(", ", VirtualDiskManager.SupportedDiskTypes) + ")");
            
            parser.AddParameter(_diskFile);
            parser.AddSwitch(_labelSwitch);
            parser.AddSwitch(_formatSwitch);
            parser.AddSwitch(_diskType);
            parser.AddSwitch(_partitionTableSwitch);
            parser.AddSwitch(_partitionTypeSwitch);

            return StandardSwitches.UserAndPassword | StandardSwitches.FileNameEncoding;
        }

        protected override void DoRun()
        {
            if (!_diskFile.IsPresent || !_formatSwitch.IsPresent)
            {
                DisplayHelp();
                Environment.ExitCode = 1;
                return;
            }

            DiscUtils.FileSystems.SetupHelper.SetupFileSystems();
            DiscUtils.Complete.SetupHelper.SetupComplete(); // From DiscUtils.Complete
            Console.OutputEncoding = Encoding.UTF8;

            VirtualDisk disk = VirtualDisk.OpenDisk(_diskFile.Value, _diskType.IsPresent ? _diskType.Value : null, FileAccess.ReadWrite, UserName, Password);
            if (disk == null) {
                Console.WriteLine("Failed to open virtual disk");
                Environment.ExitCode = 1;
                return;
            }

            if (!disk.IsPartitioned && !_partitionTableSwitch.IsPresent) {
                Console.WriteLine("Disk needs to be partitioned. Please specify the partition table type");
                Environment.ExitCode = 1;
                return;
            }
            // check if we need to partition the disk
            if (!disk.IsPartitioned || _partitionTableSwitch.IsPresent)
            {
                // figure out the partition type
                WellKnownPartitionType partitionType = WellKnownPartitionType.WindowsFat;
                if (_partitionTypeSwitch.Value == "fat" || _formatSwitch.Value == "fat") partitionType = WellKnownPartitionType.WindowsFat;
                else if (_partitionTypeSwitch.Value == "ntfs" || _formatSwitch.Value == "ntfs") partitionType = WellKnownPartitionType.WindowsNtfs;
                else if (_partitionTypeSwitch.Value == "linux") partitionType = WellKnownPartitionType.Linux;
                else if (_partitionTypeSwitch.Value == "swap") partitionType = WellKnownPartitionType.LinuxSwap;
                else if (_partitionTypeSwitch.Value == "lvm") partitionType = WellKnownPartitionType.LinuxLvm;
                else {
                    Console.WriteLine("Unknown Partition Type");
                    Environment.ExitCode = 1;
                    return;
                }

                if (_partitionTableSwitch.Value == "guid" || _partitionTableSwitch.Value == "gpt") GuidPartitionTable.Initialize(disk, partitionType);
                else if (_partitionTableSwitch.Value == "bios") BiosPartitionTable.Initialize(disk, partitionType);
                else {
                    Console.WriteLine("Unknown Partition Table Type");
                    Environment.ExitCode = 1;
                    return;
                }
                Console.WriteLine("Disk Partitioned.");
            }
            else if (_partitionTypeSwitch.IsPresent) {
                Console.WriteLine("You specified a partition type, but we are not partitioning the disk");
                Environment.ExitCode = 1;
                return;
            }

            // now we need to format the file system
            string label = _labelSwitch.IsPresent ? _labelSwitch.Value : null;
            if (_formatSwitch.Value == "fat") {
                FatFileSystem.FormatPartition(disk, 0, label);
            }
            else if (_formatSwitch.Value == "ntfs") {
                VolumeManager volMgr = new VolumeManager(disk);
                NtfsFileSystem.Format(volMgr.GetLogicalVolumes()[0], label);
            }
            else if (_formatSwitch.Value == "hfsp") {
                Console.WriteLine("HFS+ is currently unsupported");
                Environment.ExitCode = 1;
                return;
            }   
            else if (_formatSwitch.Value == "ext") {
                Console.WriteLine("EXT is currently unsupported");
                Environment.ExitCode = 1;
                return;
            }   
            else {
                Console.WriteLine("Unknown format type");
                Environment.ExitCode = 1;
                return;
            }

            // Make sure to clean up after ourselves
            disk.Dispose();
           
        }

    }
}