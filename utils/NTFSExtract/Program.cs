//
// Copyright (c) 2008-2010, Kenneth Bell
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
using DiscUtils.Ntfs;

namespace NTFSExtract
{
    class Program : ProgramBase
    {
        private CommandLineMultiParameter _diskFiles;
        private CommandLineParameter _inFilePath;
        private CommandLineParameter _outFilePath;
        private CommandLineSwitch _attributeName;
        private CommandLineSwitch _attributeType;
        private CommandLineSwitch _hexDump;

        static void Main(string[] args)
        {
            Program program = new Program();
            program.Run(args);
        }

        protected override ProgramBase.StandardSwitches DefineCommandLine(CommandLineParser parser)
        {
            _diskFiles = FileOrUriMultiParameter("disk", "The disks to inspect.", false);
            _inFilePath = new CommandLineParameter("file_path", "The path of the file to extract.", false);
            _outFilePath = new CommandLineParameter("out_file", "The output file to be written.", false);
            _attributeName = new CommandLineSwitch("a", "attribute", "name", "The name of the attribute to extract (the default is 'unnamed').");
            _attributeType = new CommandLineSwitch("t", "type", "type", "The type of the attribute to extract (the default is Data).  One of: StandardInformation, AttributeList, FileName, ObjectId, SecurityDescriptor, VolumeName, VolumeInformation, Data, IndexRoot, IndexAllocation, Bitmap, ReparsePoint, ExtendedAttributesInformation, ExtendedAttributes, PropertySet, LoggedUtilityStream.");
            _hexDump = new CommandLineSwitch("hd", "hexdump", null, "Output a HexDump of the NTFS stream to the console, in addition to writing it to the output file.");

            parser.AddMultiParameter(_diskFiles);
            parser.AddParameter(_inFilePath);
            parser.AddParameter(_outFilePath);
            parser.AddSwitch(_attributeName);
            parser.AddSwitch(_attributeType);
            parser.AddSwitch(_hexDump);

            return StandardSwitches.UserAndPassword | StandardSwitches.PartitionOrVolume;
        }

        protected override void DoRun()
        {
            AttributeType type = AttributeType.Data;
            if (_attributeType.IsPresent && !string.IsNullOrEmpty(_attributeType.Value))
            {
                type = (AttributeType)Enum.Parse(typeof(AttributeType), _attributeType.Value, true);
            }

            VolumeManager volMgr = new VolumeManager();
            foreach (string disk in _diskFiles.Values)
            {
                volMgr.AddDisk(VirtualDisk.OpenDisk(disk, FileAccess.Read, UserName, Password));
            }


            Stream partitionStream = null;
            if (!string.IsNullOrEmpty(VolumeId))
            {
                partitionStream = volMgr.GetVolume(VolumeId).Open();
            }
            else if (Partition >= 0)
            {
                partitionStream = volMgr.GetPhysicalVolumes()[Partition].Open();
            }
            else
            {
                partitionStream = volMgr.GetLogicalVolumes()[0].Open();
            }

            using (NtfsFileSystem fs = new NtfsFileSystem(partitionStream))
            {
                using (Stream source = fs.OpenFile(_inFilePath.Value, FileMode.Open, FileAccess.Read))
                {
                    using (FileStream outFile = new FileStream(_outFilePath.Value, FileMode.Create, FileAccess.ReadWrite))
                    {
                        outFile.SetLength(source.Length);
                        byte[] buffer = new byte[1024 * 1024];
                        int numRead = source.Read(buffer, 0, buffer.Length);
                        while (numRead > 0)
                        {
                            outFile.Write(buffer, 0, numRead);
                            numRead = source.Read(buffer, 0, buffer.Length);
                        }
                    }

                    if (_hexDump.IsPresent)
                    {
                        source.Position = 0;
                        HexDump.Generate(source, Console.Out);
                    }
                }
            }
        }

    }
}
