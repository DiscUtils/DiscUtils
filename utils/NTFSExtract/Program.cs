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
using DiscUtils.Ntfs;
using DiscUtils.Vhd;

namespace NTFSExtract
{
    class Program
    {
        private static CommandLineParameter _diskFile;
        private static CommandLineParameter _inFilePath;
        private static CommandLineParameter _outFilePath;
        private static CommandLineSwitch _attributeName;
        private static CommandLineSwitch _attributeType;
        private static CommandLineSwitch _partition;
        private static CommandLineSwitch _helpSwitch;
        private static CommandLineSwitch _quietSwitch;

        static void Main(string[] args)
        {
            _diskFile = new CommandLineParameter("disk", "The name of the VHD, VMDK or VDI file to access.", false);
            _inFilePath = new CommandLineParameter("file_path", "The path of the file to extract.", false);
            _outFilePath = new CommandLineParameter("out_file", "The output file to be written.", false);
            _attributeName = new CommandLineSwitch("a", "attribute", "name", "The name of the attribute to extract (the default is 'unnamed').");
            _attributeType = new CommandLineSwitch("t", "type", "type", "The type of the attribute to extract (the default is Data).  One of: StandardInformation, AttributeList, FileName, ObjectId, SecurityDescriptor, VolumeName, VolumeInformation, Data, IndexRoot, IndexAllocation, Bitmap, ReparsePoint, ExtendedAttributesInformation, ExtendedAttributes, PropertySet, LoggedUtilityStream.");
            _partition = new CommandLineSwitch("p", "partition", "num", "The number of the partition to access, in the range 0-n.  If not specified, 0 (the first partition) is the default.");
            _helpSwitch = new CommandLineSwitch(new string[] { "h", "?" }, "help", null, "Show this help.");
            _quietSwitch = new CommandLineSwitch("q", "quiet", null, "Run quietly.");

            CommandLineParser parser = new CommandLineParser("NTFSDump");
            parser.AddParameter(_diskFile);
            parser.AddParameter(_inFilePath);
            parser.AddParameter(_outFilePath);
            parser.AddSwitch(_attributeName);
            parser.AddSwitch(_attributeType);
            parser.AddSwitch(_partition);
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
                return;
            }

            int partition = 0;
            if (_partition.IsPresent && !int.TryParse(_partition.Value, out partition))
            {
                parser.DisplayHelp();
                return;
            }

            AttributeType type = AttributeType.Data;
            if (_attributeType.IsPresent && !string.IsNullOrEmpty(_attributeType.Value))
            {
                type = (AttributeType)Enum.Parse(typeof(AttributeType), _attributeType.Value, true);
            }

            using (VirtualDisk disk = Utilities.OpenDisk(_diskFile.Value, FileAccess.Read))
            {
                using (Stream partitionStream = disk.Partitions[partition].Open())
                {
                    using (NtfsFileSystem fs = new NtfsFileSystem(partitionStream))
                    {
                        using (Stream source = fs.OpenRawStream(_inFilePath.Value, type, _attributeName.Value, FileAccess.Read))
                        {
                            using (FileStream outFile = new FileStream(_outFilePath.Value, FileMode.Create, FileAccess.ReadWrite))
                            {
                                byte[] buffer = new byte[100 * 1024];
                                int numRead = source.Read(buffer, 0, buffer.Length);
                                while (numRead != 0)
                                {
                                    outFile.Write(buffer, 0, numRead);
                                    numRead = source.Read(buffer, 0, buffer.Length);
                                }
                            }
                        }
                    }
                }
            }
        }



        private static void ShowHeader()
        {
            Console.WriteLine("NTFSExtract v{0}, available from http://codeplex.com/DiscUtils", GetVersion());
            Console.WriteLine("Copyright (c) Kenneth Bell, 2008-2009");
            Console.WriteLine("Free software issued under the MIT License, see LICENSE.TXT for details.");
            Console.WriteLine();
        }

        private static string GetVersion()
        {
            return typeof(Program).Assembly.GetName().Version.ToString(3);
        }
    }
}
