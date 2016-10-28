//
// Copyright (c) 2008-2013, Kenneth Bell
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
using DiscUtils.Common;
using DiscUtils.Vhdx;

namespace VHDXDump
{
    class Program : ProgramBase
    {
        private CommandLineParameter _vhdxFile;

        static void Main(string[] args)
        {
            Program program = new Program();
            program.Run(args);
        }

        protected override ProgramBase.StandardSwitches DefineCommandLine(CommandLineParser parser)
        {
            _vhdxFile = new CommandLineParameter("vhdx_file", "Path to the VHDX file to inspect.", false);

            parser.AddParameter(_vhdxFile);

            return StandardSwitches.Default;
        }

        protected override void DoRun()
        {
            using (DiskImageFile vhdxFile = new DiskImageFile(_vhdxFile.Value, FileAccess.Read))
            {
                DiskImageFileInfo info = vhdxFile.Information;

                FileInfo fileInfo = new FileInfo(_vhdxFile.Value);

                Console.WriteLine("File Info");
                Console.WriteLine("---------");
                Console.WriteLine("           File Name: {0}", fileInfo.FullName);
                Console.WriteLine("           File Size: {0} ({1} bytes)", Utilities.ApproximateDiskSize(fileInfo.Length), fileInfo.Length);
                Console.WriteLine("  File Creation Time: {0} (UTC)", fileInfo.CreationTimeUtc);
                Console.WriteLine("     File Write Time: {0} (UTC)", fileInfo.LastWriteTimeUtc);
                Console.WriteLine();

                Console.WriteLine("VHDX File Info");
                Console.WriteLine("--------------");
                Console.WriteLine("           Signature: {0:x8}", info.Signature);
                Console.WriteLine("             Creator: {0:x8}", info.Creator);
                Console.WriteLine("          Block Size: {0} (0x{0:X8})", info.BlockSize);
                Console.WriteLine("Leave Blocks Alloced: {0}", info.LeaveBlocksAllocated);
                Console.WriteLine("          Has Parent: {0}", info.HasParent);
                Console.WriteLine("           Disk Size: {0} ({1} (0x{1:X8}))", Utilities.ApproximateDiskSize(info.DiskSize), info.DiskSize);
                Console.WriteLine(" Logical Sector Size: {0} (0x{0:X8})", info.LogicalSectorSize);
                Console.WriteLine("Physical Sector Size: {0} (0x{0:X8})", info.PhysicalSectorSize);
                Console.WriteLine(" Parent Locator Type: {0}", info.ParentLocatorType);
                WriteParentLocations(info);
                Console.WriteLine();

                WriteHeaderInfo(info.FirstHeader);
                WriteHeaderInfo(info.SecondHeader);

                if (info.ActiveHeader.LogGuid != Guid.Empty)
                {
                    Console.WriteLine("Log Info (Active Sequence)");
                    Console.WriteLine("--------------------------");

                    foreach (var entry in info.ActiveLogSequence)
                    {
                        Console.WriteLine("   Log Entry");
                        Console.WriteLine("   ---------");
                        Console.WriteLine("         Sequence Number: {0}", entry.SequenceNumber);
                        Console.WriteLine("                    Tail: {0}", entry.Tail);
                        Console.WriteLine("     Flushed File Offset: {0} (0x{0:X8})", entry.FlushedFileOffset);
                        Console.WriteLine("        Last File Offset: {0} (0x{0:X8})", entry.LastFileOffset);
                        Console.WriteLine("            File Extents: {0}", entry.IsEmpty ? "<none>" : "");
                        foreach (var extent in entry.ModifiedExtents)
                        {
                            Console.WriteLine("                          {0} +{1}  (0x{0:X8} +0x{1:X8})", extent.Offset, extent.Count);
                        }
                        Console.WriteLine();
                    }
                }

                RegionTableInfo regionTable = info.RegionTable;
                Console.WriteLine("Region Table Info");
                Console.WriteLine("-----------------");
                Console.WriteLine("           Signature: {0}", regionTable.Signature);
                Console.WriteLine("            Checksum: {0:x8}", regionTable.Checksum);
                Console.WriteLine("         Entry Count: {0}", regionTable.Count);
                Console.WriteLine();

                foreach (var entry in regionTable)
                {
                    Console.WriteLine("Region Table Entry Info");
                    Console.WriteLine("-----------------------");
                    Console.WriteLine("                Guid: {0}", entry.Guid);
                    Console.WriteLine("     Well-Known Name: {0}", entry.WellKnownName);
                    Console.WriteLine("         File Offset: {0} (0x{0:X8})", entry.FileOffset);
                    Console.WriteLine("              Length: {0} (0x{0:X8})", entry.Length);
                    Console.WriteLine("         Is Required: {0}", entry.IsRequired);
                    Console.WriteLine();
                }

                MetadataTableInfo metadataTable = info.MetadataTable;
                Console.WriteLine("Metadata Table Info");
                Console.WriteLine("-------------------");
                Console.WriteLine("           Signature: {0}", metadataTable.Signature);
                Console.WriteLine("         Entry Count: {0}", metadataTable.Count);
                Console.WriteLine();

                foreach (var entry in metadataTable)
                {
                    Console.WriteLine("Metadata Table Entry Info");
                    Console.WriteLine("-------------------------");
                    Console.WriteLine("             Item Id: {0}", entry.ItemId);
                    Console.WriteLine("     Well-Known Name: {0}", entry.WellKnownName);
                    Console.WriteLine("              Offset: {0} (0x{0:X8})", entry.Offset);
                    Console.WriteLine("              Length: {0} (0x{0:X8})", entry.Length);
                    Console.WriteLine("             Is User: {0}", entry.IsUser);
                    Console.WriteLine("         Is Required: {0}", entry.IsRequired);
                    Console.WriteLine("     Is Virtual Disk: {0}", entry.IsVirtualDisk);
                    Console.WriteLine();
                }
            }
        }

        private static void WriteParentLocations(DiskImageFileInfo info)
        {
            Console.Write("    Parent Locations: ");

            bool first = true;

            foreach (var entry in info.ParentLocatorEntries)
            {
                if (!first)
                {
                    Console.WriteLine("                      ");
                }
                first = false;

                Console.WriteLine("{0} -> {1}", entry.Key, entry.Value);
            }

            if (first)
            {
                Console.WriteLine();
            }
        }

        private void WriteHeaderInfo(HeaderInfo info)
        {
            Console.WriteLine("Header Info");
            Console.WriteLine("-----------");
            Console.WriteLine("    Header Signature: {0}", info.Signature);
            Console.WriteLine("     Sequence Number: {0}", info.SequenceNumber);
            Console.WriteLine("            Checksum: {0:x8}", info.Checksum);
            Console.WriteLine("     File Write Guid: {0}", info.FileWriteGuid);
            Console.WriteLine("     Data Write Guid: {0}", info.DataWriteGuid);
            Console.WriteLine("            Log Guid: {0}", info.LogGuid);
            Console.WriteLine("         Log Version: {0}", info.LogVersion);
            Console.WriteLine("             Version: {0}", info.Version);
            Console.WriteLine("          Log Length: {0} (0x{0:X8})", info.LogLength);
            Console.WriteLine("     Log File Offset: {0} (0x{0:X8})", info.LogOffset);
            Console.WriteLine();
        }
    }
}
