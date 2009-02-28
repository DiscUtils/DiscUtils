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
using DiscUtils.Common;
using DiscUtils.Vhd;

namespace VHDCreate
{
    class Program
    {
        private static CommandLineParameter _sourceFile;
        private static CommandLineParameter _destFile;
        private static CommandLineSwitch _typeSwitch;
        private static CommandLineSwitch _sizeSwitch;
        private static CommandLineSwitch _blockSizeSwitch;
        private static CommandLineSwitch _helpSwitch;
        private static CommandLineSwitch _quietSwitch;

        static void Main(string[] args)
        {
            _destFile = new CommandLineParameter("new.vhd", "Path to the VHD file to create.", false);
            _sourceFile = new CommandLineParameter("base.vhd", "For differencing disks, the path to the base disk.", true);
            _typeSwitch = new CommandLineSwitch("t", "type", "type", "The type of disk to create, one of: fixed, dynamic, diff.  The default is dynamic.");
            _sizeSwitch = new CommandLineSwitch("sz", "size", "size", "REQUIRED for fixed and dynamic disks, the size of the disk.  Use B, KB, MB, GB to specify units (units default to bytes if not specified).");
            _blockSizeSwitch = new CommandLineSwitch("bs", "blocksize", "size", "For dynamic disks, the allocation uint size for new disk regions in bytes.  The default is 2MB.    Use B, KB, MB, GB to specify units (units default to bytes if not specified).");
            _helpSwitch = new CommandLineSwitch(new string[] { "h", "?" }, "help", null, "Show this help.");
            _quietSwitch = new CommandLineSwitch("q", "quiet", null, "Run quietly.");

            CommandLineParser parser = new CommandLineParser("VHDCreate");
            parser.AddParameter(_destFile);
            parser.AddParameter(_sourceFile);
            parser.AddSwitch(_typeSwitch);
            parser.AddSwitch(_sizeSwitch);
            parser.AddSwitch(_blockSizeSwitch);
            parser.AddSwitch(_helpSwitch);
            parser.AddSwitch(_quietSwitch);

            bool parseResult = parser.Parse(args);

            if (!_quietSwitch.IsPresent)
            {
                ShowHeader();
            }

            if (_helpSwitch.IsPresent || !parseResult)
            {
                parser.DisplayHelp();
                Environment.ExitCode = 1;
                return;
            }

            if(!_destFile.IsPresent)
            {
                parser.DisplayHelp();
                Environment.ExitCode = 1;
                return;
            }

            if ((_typeSwitch.IsPresent && _typeSwitch.Value == "dynamic") || !_typeSwitch.IsPresent)
            {
                long size;
                if(!_sizeSwitch.IsPresent || !TryParseDiskSize(_sizeSwitch.Value, out size))
                {
                    parser.DisplayHelp();
                    Environment.ExitCode = 1;
                    return;
                }

                long blockSize = 2 * 1024 * 1024;
                if (_blockSizeSwitch.IsPresent)
                {
                    if (!TryParseDiskSize(_blockSizeSwitch.Value, out blockSize))
                    {
                        parser.DisplayHelp();
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

                using(FileStream fs = new FileStream(_destFile.Value, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    Disk.InitializeDynamic(fs, Ownership.None, size, blockSize);
                }
            }
            else if (_typeSwitch.Value == "diff")
            {
                // Create Diff
                if (!_sourceFile.IsPresent)
                {
                    parser.DisplayHelp();
                    Environment.ExitCode = 1;
                    return;
                }

                Disk.InitializeDifferencing(_destFile.Value, _sourceFile.Value);
            }
            else if (_typeSwitch.Value == "fixed")
            {
                // Create Fixed disk
                long size;
                if (!_sizeSwitch.IsPresent || !TryParseDiskSize(_sizeSwitch.Value, out size))
                {
                    parser.DisplayHelp();
                    Environment.ExitCode = 1;
                    return;
                }

                using (FileStream fs = new FileStream(_destFile.Value, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    Disk.InitializeFixed(fs, Ownership.None, size);
                }
            }
            else
            {
                parser.DisplayHelp();
                Environment.ExitCode = 1;
                return;
            }
        }

        private static void ShowHeader()
        {
            Console.WriteLine("VHDCreate v{0}, available from http://codeplex.com/DiscUtils", GetVersion());
            Console.WriteLine("Copyright (c) Kenneth Bell, 2008-2009");
            Console.WriteLine("Free software issued under the MIT License, see LICENSE.TXT for details.");
            Console.WriteLine();
        }

        private static string GetVersion()
        {
            return typeof(Program).Assembly.GetName().Version.ToString(3);
        }

        private static bool TryParseDiskSize(string size, out long value)
        {
            char lastChar = size[size.Length - 1];
            if (Char.IsDigit(lastChar))
            {
                return long.TryParse(size, out value);
            }
            else if (lastChar == 'B' && size.Length >= 2)
            {
                char unitChar = size[size.Length - 2];

                // suffix is 'B', indicating bytes
                if(Char.IsDigit(unitChar))
                {
                    return long.TryParse(size.Substring(0, size.Length - 1), out value);
                }

                // suffix is KB, MB or GB
                long quantity;
                if(!long.TryParse(size.Substring(0, size.Length - 2), out quantity))
                {
                    value = 0;
                    return false;
                }

                switch(unitChar)
                {
                    case 'K':
                        value = quantity * 1024;
                        return true;
                    case 'M':
                        value = quantity * 1024 * 1024;
                        return true;
                    case 'G':
                        value = quantity * 1024 * 1024 * 1024;
                        return true;
                    default:
                        value = 0;
                        return false;
                }
            }
            else
            {
                value = 0;
                return false;
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
