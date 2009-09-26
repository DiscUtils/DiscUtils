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
using System.Collections.Generic;
using System.IO;
using DiscUtils;
using DiscUtils.Common;
using DiscUtils.LogicalDiskManager;
using DiscUtils.Partitions;

namespace DiskDump
{
    class Program
    {
        private static CommandLineMultiParameter _inFiles;
        private static CommandLineSwitch _userName;
        private static CommandLineSwitch _password;
        private static CommandLineSwitch _showContent;
        private static CommandLineSwitch _showVolContent;
        private static CommandLineSwitch _helpSwitch;
        private static CommandLineSwitch _quietSwitch;

        static void Main(string[] args)
        {
            _inFiles = new CommandLineMultiParameter("disk", "Paths to the disks to inspect.  Values can be a file path, or a path to an iSCSI LUN (iscsi://<address>), for example iscsi://192.168.1.2/iqn.2002-2004.example.com:port1?LUN=2.  Use iSCSIBrowse to discover this address.", false);
            _showContent = new CommandLineSwitch("db", "diskbytes", null, "Includes a hexdump of all disk content in the output");
            _showVolContent = new CommandLineSwitch("vb", "volbytes", null, "Includes a hexdump of all volumes content in the output");
            _userName = new CommandLineSwitch("u", "user", "user_name", "If using iSCSI, optionally use this parameter to specify the user name to authenticate with.  If this parameter is specified without a password, you will be prompted to supply the password.");
            _password = new CommandLineSwitch("pw", "password", "secret", "If using iSCSI, optionally use this parameter to specify the password to authenticate with.");
            _helpSwitch = new CommandLineSwitch(new string[] { "h", "?" }, "help", null, "Show this help.");
            _quietSwitch = new CommandLineSwitch("q", "quiet", null, "Run quietly.");

            CommandLineParser parser = new CommandLineParser("DumpDisk");
            parser.AddMultiParameter(_inFiles);
            parser.AddSwitch(_showContent);
            parser.AddSwitch(_showVolContent);
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
                return;
            }

            string user = _userName.IsPresent ? _userName.Value : null;
            string password = _password.IsPresent ? _password.Value : null;

            List<VirtualDisk> disks = new List<VirtualDisk>();
            foreach (var path in _inFiles.Values)
            {
                VirtualDisk disk = Utilities.OpenDisk(path, FileAccess.Read, user, password);
                disks.Add(disk);

                Console.WriteLine();
                Console.WriteLine("DISK: " + path);
                Console.WriteLine();
                Console.WriteLine("   Capacity: {0:X16}", disk.Capacity);
                Console.WriteLine("   Geometry: {0}", disk.Geometry);
                Console.WriteLine("  Signature: {0:X8}", disk.Signature);
                if (disk.IsPartitioned)
                {
                    Console.WriteLine("       GUID: {0}", disk.Partitions.DiskGuid);
                }
                Console.WriteLine();


                Console.WriteLine();
                Console.WriteLine("  Stored Extents");
                Console.WriteLine();
                foreach (var extent in disk.Content.Extents)
                {
                    Console.WriteLine("    {0:X16} - {1:X16}", extent.Start, extent.Start + extent.Length);
                }
                Console.WriteLine();


                if (disk.IsPartitioned)
                {
                    Console.WriteLine();
                    Console.WriteLine("  Partitions");
                    Console.WriteLine();
                    Console.WriteLine("  T   Start (bytes)     End (bytes)       Type");
                    foreach (var partition in disk.Partitions.Partitions)
                    {
                        Console.WriteLine("  {0:X2}  {1:X16}  {2:X16}  {3}", partition.BiosType, partition.FirstSector * 512, partition.LastSector * 512 + 512, partition.TypeAsString);
                    }
                    Console.WriteLine();
                }
            }


            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("VOLUMES");
            Console.WriteLine();
            VolumeManager volMgr = new VolumeManager();
            foreach (var disk in disks)
            {
                volMgr.AddDisk(disk);
            }

            try
            {
                Console.WriteLine();
                Console.WriteLine("  Physical Volumes");
                Console.WriteLine();
                foreach (var vol in volMgr.GetPhysicalVolumes())
                {
                    Console.WriteLine("  " + vol.Identity);
                    Console.WriteLine("    Type: " + vol.VolumeType);
                    Console.WriteLine("    BIOS Type: " + vol.BiosType.ToString("X2") + " [" + BiosPartitionTypes.ToString(vol.BiosType) + "]");
                    Console.WriteLine("    Size: " + vol.Length);
                    Console.WriteLine("    Disk Id: " + vol.DiskIdentity);
                    Console.WriteLine("    Disk Sig: " + vol.DiskSignature.ToString("X8"));
                    Console.WriteLine("    Partition: " + vol.PartitionIdentity);
                    Console.WriteLine("    Disk Geometry: " + vol.PhysicalGeometry);
                    Console.WriteLine("    First Sector: " + vol.PhysicalStartSector);
                    Console.WriteLine();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            try
            {
                Console.WriteLine();
                Console.WriteLine("  Logical Volumes");
                Console.WriteLine();
                foreach (var vol in volMgr.GetLogicalVolumes())
                {
                    Console.WriteLine("  " + vol.Identity);
                    Console.WriteLine("    BIOS Type: " + vol.BiosType.ToString("X2") + " [" + BiosPartitionTypes.ToString(vol.BiosType) + "]");
                    Console.WriteLine("    Status: " + vol.Status);
                    Console.WriteLine("    Size: " + vol.Length);
                    Console.WriteLine("    Disk Geometry: " + vol.PhysicalGeometry);
                    Console.WriteLine("    First Sector: " + vol.PhysicalStartSector);
                    Console.WriteLine();

                    if (_showVolContent.IsPresent)
                    {
                        Console.WriteLine("    Contents...");
                        try
                        {
                            using (Stream s = vol.Open())
                            {
                                HexDump.Generate(s, Console.Out);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.ToString());
                        }
                        Console.WriteLine();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            try
            {
                bool foundDynDisk = false;
                DynamicDiskManager dynDiskManager = new DynamicDiskManager();
                foreach (var disk in disks)
                {
                    if (DynamicDiskManager.IsDynamicDisk(disk))
                    {
                        dynDiskManager.Add(disk);
                        foundDynDisk = true;
                    }
                }
                if (foundDynDisk)
                {
                    Console.WriteLine();
                    Console.WriteLine("  Logical Disk Manager Info");
                    Console.WriteLine();
                    dynDiskManager.Dump(Console.Out, "  ");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            if (_showContent.IsPresent)
            {
                foreach (var path in _inFiles.Values)
                {
                    VirtualDisk disk = Utilities.OpenDisk(path, FileAccess.Read, user, password);

                    Console.WriteLine();
                    Console.WriteLine("DISK CONTENTS ({0})", path);
                    Console.WriteLine();
                    HexDump.Generate(disk.Content, Console.Out);
                    Console.WriteLine();
                }
            }
        }

    }
}
