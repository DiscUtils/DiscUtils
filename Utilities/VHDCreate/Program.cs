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
using DiscUtils.Streams;
using DiscUtils.Vhd;

namespace VHDCreate
{
    class Program : ProgramBase
    {
        private CommandLineParameter _sourceFile;
        private CommandLineParameter _destFile;
        private CommandLineSwitch _typeSwitch;
        private CommandLineSwitch _blockSizeSwitch;

        static void Main(string[] args)
        {
            Program program = new Program();
            program.Run(args);
        }

        protected override StandardSwitches DefineCommandLine(CommandLineParser parser)
        {
            _destFile = new CommandLineParameter("new.vhd", "Path to the VHD file to create.", false);
            _sourceFile = new CommandLineParameter("base.vhd", "For differencing disks, the path to the base disk.", true);
            _typeSwitch = new CommandLineSwitch("t", "type", "type", "The type of disk to create, one of: fixed, dynamic, diff.  The default is dynamic.");
            _blockSizeSwitch = new CommandLineSwitch("bs", "blocksize", "size", "For dynamic disks, the allocation uint size for new disk regions in bytes.  The default is 2MB.    Use B, KB, MB, GB to specify units (units default to bytes if not specified).");

            parser.AddParameter(_destFile);
            parser.AddParameter(_sourceFile);
            parser.AddSwitch(_typeSwitch);
            parser.AddSwitch(_blockSizeSwitch);

            return StandardSwitches.DiskSize;
        }

        protected override void DoRun()
        {
            if (!_destFile.IsPresent)
            {
                DisplayHelp();
                Environment.ExitCode = 1;
                return;
            }

            if ((_typeSwitch.IsPresent && _typeSwitch.Value == "dynamic") || !_typeSwitch.IsPresent)
            {
                long blockSize = 2 * 1024 * 1024;
                if (_blockSizeSwitch.IsPresent)
                {
                    if (!Utilities.TryParseDiskSize(_blockSizeSwitch.Value, out blockSize))
                    {
                        DisplayHelp();
                        Environment.ExitCode = 1;
                        return;
                    }
                }

                if (blockSize == 0 || ((blockSize & 0x1FF) != 0) || !IsPowerOfTwo((ulong)(blockSize / 512)))
                {
                    Console.WriteLine("ERROR: blocksize must be power of 2 sectors - e.g. 512B, 1KB, 2KB, 4KB, ...");
                    Environment.ExitCode = 2;
                    return;
                }

                if (DiskSize <= 0)
                {
                    Console.WriteLine("ERROR: disk size must be greater than zero.");
                    Environment.ExitCode = 3;
                    return;
                }

                using (FileStream fs = new FileStream(_destFile.Value, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    Disk.InitializeDynamic(fs, Ownership.None, DiskSize, blockSize);
                }
            }
            else if (_typeSwitch.Value == "diff")
            {
                // Create Diff
                if (!_sourceFile.IsPresent)
                {
                    DisplayHelp();
                    Environment.ExitCode = 1;
                    return;
                }

                Disk.InitializeDifferencing(_destFile.Value, _sourceFile.Value);
            }
            else if (_typeSwitch.Value == "fixed")
            {
                if (DiskSize <= 0)
                {
                    Console.WriteLine("ERROR: disk size must be greater than zero.");
                    Environment.ExitCode = 3;
                    return;
                }

                // Create Fixed disk
                using (FileStream fs = new FileStream(_destFile.Value, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    Disk.InitializeFixed(fs, Ownership.None, DiskSize);
                }
            }
            else
            {
                DisplayHelp();
                Environment.ExitCode = 1;
                return;
            }
        }

        private static bool IsPowerOfTwo(ulong val)
        {
            while (val > 0)
            {
                if ((val & 0x1) != 0)
                {
                    // If the low bit is set, it should be the only bit set
                    if (val != 0x1)
                    {
                        return false;
                    }
                }

                val >>= 1;
            }

            return true;
        }

    }
}
