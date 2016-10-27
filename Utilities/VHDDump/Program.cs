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
using DiscUtils.Vhd;

namespace VHDDump
{
    class Program : ProgramBase
    {
        private CommandLineParameter _vhdFile;
        private CommandLineSwitch _dontCheck;

        static void Main(string[] args)
        {
            Program program = new Program();
            program.Run(args);
        }

        protected override ProgramBase.StandardSwitches DefineCommandLine(CommandLineParser parser)
        {
            _vhdFile = new CommandLineParameter("vhd_file", "Path to the VHD file to inspect.", false);
            _dontCheck = new CommandLineSwitch("nc", "noCheck", null, "Don't check the VHD file format for corruption");

            parser.AddParameter(_vhdFile);
            parser.AddSwitch(_dontCheck);

            return StandardSwitches.Default;
        }

        protected override void DoRun()
        {
            if (!_dontCheck.IsPresent)
            {
                using (Stream s = new FileStream(_vhdFile.Value, FileMode.Open, FileAccess.Read))
                {
                    FileChecker vhdChecker = new FileChecker(s);
                    if (!vhdChecker.Check(Console.Out, ReportLevels.All))
                    {
                        Console.WriteLine("Aborting: Invalid VHD file");
                        Environment.Exit(1);
                    }
                }
            }

            using (DiskImageFile vhdFile = new DiskImageFile(_vhdFile.Value, FileAccess.Read))
            {
                DiskImageFileInfo info = vhdFile.Information;

                FileInfo fileInfo = new FileInfo(_vhdFile.Value);

                Console.WriteLine("File Info");
                Console.WriteLine("---------");
                Console.WriteLine("           File Name: {0}", fileInfo.FullName);
                Console.WriteLine("           File Size: {0} bytes", fileInfo.Length);
                Console.WriteLine("  File Creation Time: {0} (UTC)", fileInfo.CreationTimeUtc);
                Console.WriteLine("     File Write Time: {0} (UTC)", fileInfo.LastWriteTimeUtc);
                Console.WriteLine();

                Console.WriteLine("Common Disk Info");
                Console.WriteLine("-----------------");
                Console.WriteLine("              Cookie: {0:x8}", info.Cookie);
                Console.WriteLine("            Features: {0:x8}", info.Features);
                Console.WriteLine(" File Format Version: {0}.{1}", ((info.FileFormatVersion >> 16) & 0xFFFF), (info.FileFormatVersion & 0xFFFF));
                Console.WriteLine("       Creation Time: {0} (UTC)", info.CreationTimestamp);
                Console.WriteLine("         Creator App: {0:x8}", info.CreatorApp);
                Console.WriteLine("     Creator Version: {0}.{1}", ((info.CreatorVersion >> 16) & 0xFFFF), (info.CreatorVersion & 0xFFFF));
                Console.WriteLine("     Creator Host OS: {0}", info.CreatorHostOS);
                Console.WriteLine("       Original Size: {0} bytes", info.OriginalSize);
                Console.WriteLine("        Current Size: {0} bytes", info.CurrentSize);
                Console.WriteLine("    Geometry (C/H/S): {0}", info.Geometry);
                Console.WriteLine("           Disk Type: {0}", info.DiskType);
                Console.WriteLine("            Checksum: {0:x8}", info.FooterChecksum);
                Console.WriteLine("           Unique Id: {0}", info.UniqueId);
                Console.WriteLine("         Saved State: {0}", info.SavedState);
                Console.WriteLine();

                if (info.DiskType == FileType.Differencing || info.DiskType == FileType.Dynamic)
                {
                    Console.WriteLine();
                    Console.WriteLine("Dynamic Disk Info");
                    Console.WriteLine("-----------------");
                    Console.WriteLine("              Cookie: {0}", info.DynamicCookie);
                    Console.WriteLine("      Header Version: {0}.{1}", ((info.DynamicHeaderVersion >> 16) & 0xFFFF), (info.DynamicHeaderVersion & 0xFFFF));
                    Console.WriteLine("         Block Count: {0}", info.DynamicBlockCount);
                    Console.WriteLine("          Block Size: {0} bytes", info.DynamicBlockSize);
                    Console.WriteLine("            Checksum: {0:x8}", info.DynamicChecksum);
                    Console.WriteLine("    Parent Unique Id: {0}", info.DynamicParentUniqueId);
                    Console.WriteLine("   Parent Write Time: {0} (UTC)", info.DynamicParentTimestamp);
                    Console.WriteLine("         Parent Name: {0}", info.DynamicParentUnicodeName);
                    Console.Write("    Parent Locations: ");
                    foreach (string parentLocation in info.DynamicParentLocators)
                    {
                        Console.Write("{0}\n                      ", parentLocation);
                    }
                    Console.WriteLine();
                }
            }
        }
    }
}
