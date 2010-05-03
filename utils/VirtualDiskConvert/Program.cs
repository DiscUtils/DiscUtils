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

namespace VirtualDiskConvert
{
    class Program : ProgramBase
    {
        private CommandLineParameter _inFile;
        private CommandLineParameter _outFile;
        private CommandLineSwitch _wipe;

        static void Main(string[] args)
        {
            Program program = new Program();
            program.Run(args);
        }

        protected override StandardSwitches DefineCommandLine(CommandLineParser parser)
        {
            _inFile = FileOrUriParameter("in_file", "Path to the source disk.", false);
            _outFile = FileOrUriParameter("out_file", "Path to the output disk.", false);
            _wipe = new CommandLineSwitch("w", "wipe", null, "Write zero's to all unused parts of the disk.  This option only makes sense when converting to an iSCSI LUN which may be dirty.");

            parser.AddParameter(_inFile);
            parser.AddParameter(_outFile);
            parser.AddSwitch(_wipe);

            return StandardSwitches.OutputFormat | StandardSwitches.UserAndPassword;
        }

        protected override void DoRun()
        {
            using (VirtualDisk inDisk = VirtualDisk.OpenDisk(_inFile.Value, FileAccess.Read, UserName, Password))
            using (VirtualDisk outDisk = VirtualDisk.CreateDisk(OutputDiskType, OutputDiskVariant, _outFile.Value, inDisk.Capacity, inDisk.Geometry, UserName, Password, null))
            {
                if (outDisk.Capacity < inDisk.Capacity)
                {
                    Console.WriteLine("ERROR: The output disk is smaller than the input disk, conversion aborted");
                }

                if (_wipe.IsPresent)
                {
                    Pump(inDisk.Content, outDisk.Content);
                }
                else
                {
                    SparseStream.Pump(inDisk.Content, outDisk.Content);
                }
            }
        }

        protected override string[] HelpRemarks
        {
            get
            {
                return new string[] {
                    "This utility flattens disk hierarchies (VMDK linked-clones, VHD differencing disks) " +
                    "into a single disk image, but does preserve sparseness where the output disk format " +
                    "supports it."
                };
            }
        }

        public static void Pump(Stream source, Stream dest)
        {
            byte[] buffer = new byte[512 * 1024];

            int numRead = source.Read(buffer, 0, buffer.Length);
            while (numRead != 0)
            {
                dest.Write(buffer, 0, numRead);
                numRead = source.Read(buffer, 0, buffer.Length);
            }
        }

    }
}
