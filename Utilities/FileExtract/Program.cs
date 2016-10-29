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
using DiscUtils.FileSystems;

namespace FileExtract
{
    class Program : ProgramBase
    {
        private CommandLineMultiParameter _diskFiles;
        private CommandLineParameter _inFilePath;
        private CommandLineParameter _outFilePath;
        private CommandLineSwitch _diskType;
        private CommandLineSwitch _hexDump;

        static void Main(string[] args)
        {
            SetupHelper.SetupFileSystems();

            Program program = new Program();
            program.Run(args);
        }

        protected override ProgramBase.StandardSwitches DefineCommandLine(CommandLineParser parser)
        {
            _diskFiles = FileOrUriMultiParameter("disk", "The disks to inspect.", false);
            _inFilePath = new CommandLineParameter("file_path", "The path of the file to extract.", false);
            _outFilePath = new CommandLineParameter("out_file", "The output file to be written.", false);
            _diskType = new CommandLineSwitch("dt", "disktype", "type", "Force the type of disk - use a file extension (one of " + string.Join(", ", VirtualDiskManager.SupportedDiskTypes) + ")");
            _hexDump = new CommandLineSwitch("hd", "hexdump", null, "Output a HexDump of the NTFS stream to the console, in addition to writing it to the output file.");

            parser.AddMultiParameter(_diskFiles);
            parser.AddParameter(_inFilePath);
            parser.AddParameter(_outFilePath);
            parser.AddSwitch(_diskType);
            parser.AddSwitch(_hexDump);

            return StandardSwitches.UserAndPassword | StandardSwitches.PartitionOrVolume;
        }

        protected override void DoRun()
        {
            VolumeManager volMgr = new VolumeManager();
            foreach (string disk in _diskFiles.Values)
            {
                volMgr.AddDisk(VirtualDisk.OpenDisk(disk, _diskType.IsPresent ? _diskType.Value : null, FileAccess.Read, UserName, Password));
            }

            VolumeInfo volInfo = null;
            if (!string.IsNullOrEmpty(VolumeId))
            {
                volInfo = volMgr.GetVolume(VolumeId);
            }
            else if (Partition >= 0)
            {
                volInfo = volMgr.GetPhysicalVolumes()[Partition];
            }
            else
            {
                volInfo = volMgr.GetLogicalVolumes()[0];
            }

            DiscUtils.FileSystemInfo fsInfo = FileSystemManager.DetectFileSystems(volInfo)[0];

            using (DiscFileSystem fs = fsInfo.Open(volInfo, FileSystemParameters))
            {
                using (Stream source = fs.OpenFile(_inFilePath.Value, FileMode.Open, FileAccess.Read))
                {
                    using (FileStream outFile = new FileStream(_outFilePath.Value, FileMode.Create, FileAccess.ReadWrite))
                    {
                        PumpStreams(source, outFile);
                    }

                    if (_hexDump.IsPresent)
                    {
                        source.Position = 0;
                        HexDump.Generate(source, Console.Out);
                    }
                }
            }
        }

        private static void PumpStreams(Stream inStream, Stream outStream)
        {
            byte[] buffer = new byte[4096];
            int bytesRead = inStream.Read(buffer, 0, 4096);
            while (bytesRead != 0)
            {
                outStream.Write(buffer, 0, bytesRead);
                bytesRead = inStream.Read(buffer, 0, 4096);
            }
        }
    }
}
