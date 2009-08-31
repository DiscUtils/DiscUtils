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

namespace NTFSExtract
{
    class Program
    {
        private static CommandLineMultiParameter _diskFiles;
        private static CommandLineParameter _inFilePath;
        private static CommandLineParameter _outFilePath;
        private static CommandLineSwitch _attributeName;
        private static CommandLineSwitch _attributeType;
        private static CommandLineSwitch _partition;
        private static CommandLineSwitch _volumeId;
        private static CommandLineSwitch _userName;
        private static CommandLineSwitch _password;
        private static CommandLineSwitch _helpSwitch;
        private static CommandLineSwitch _quietSwitch;

        static void Main(string[] args)
        {
            _diskFiles = new CommandLineMultiParameter("disk", "Paths to the disks to inspect.  Values can be a file path, or a path to an iSCSI LUN (iscsi://<address>), for example iscsi://192.168.1.2/iqn.2002-2004.example.com:port1?LUN=2.  Use iSCSIBrowse to discover this address.", false);
            _inFilePath = new CommandLineParameter("file_path", "The path of the file to extract.", false);
            _outFilePath = new CommandLineParameter("out_file", "The output file to be written.", false);
            _attributeName = new CommandLineSwitch("a", "attribute", "name", "The name of the attribute to extract (the default is 'unnamed').");
            _attributeType = new CommandLineSwitch("t", "type", "type", "The type of the attribute to extract (the default is Data).  One of: StandardInformation, AttributeList, FileName, ObjectId, SecurityDescriptor, VolumeName, VolumeInformation, Data, IndexRoot, IndexAllocation, Bitmap, ReparsePoint, ExtendedAttributesInformation, ExtendedAttributes, PropertySet, LoggedUtilityStream.");
            _partition = new CommandLineSwitch("p", "partition", "num", "The number of the partition to access, in the range 0-n.  If not specified, 0 (the first partition) is the default.");
            _volumeId = new CommandLineSwitch("v", "volume", "id", "The volume id of the volume to access, use the VolInfo tool to discover this id.  If specified, the partition parameter is ignored.");
            _userName = new CommandLineSwitch("u", "user", "user_name", "If using iSCSI, optionally use this parameter to specify the user name to authenticate with.  If this parameter is specified without a password, you will be prompted to supply the password.");
            _password = new CommandLineSwitch("pw", "password", "secret", "If using iSCSI, optionally use this parameter to specify the password to authenticate with.");
            _helpSwitch = new CommandLineSwitch(new string[] { "h", "?" }, "help", null, "Show this help.");
            _quietSwitch = new CommandLineSwitch("q", "quiet", null, "Run quietly.");

            CommandLineParser parser = new CommandLineParser("NTFSDump");
            parser.AddMultiParameter(_diskFiles);
            parser.AddParameter(_inFilePath);
            parser.AddParameter(_outFilePath);
            parser.AddSwitch(_attributeName);
            parser.AddSwitch(_attributeType);
            parser.AddSwitch(_partition);
            parser.AddSwitch(_volumeId);
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

            string user = _userName.IsPresent ? _userName.Value : null;
            string password = _password.IsPresent ? _password.Value : null;

            using (Stream volumeStream = Utilities.OpenVolume(_volumeId.Value, partition, user, password, FileAccess.Read, _diskFiles.Values))
            {
                using (NtfsFileSystem fs = new NtfsFileSystem(volumeStream))
                {
                    using (SparseStream source = fs.OpenRawStream(_inFilePath.Value, type, _attributeName.Value, FileAccess.Read))
                    {
                        using (FileStream outFile = new FileStream(_outFilePath.Value, FileMode.Create, FileAccess.ReadWrite))
                        {
                            outFile.SetLength(source.Length);
                            SparseStream.Pump(source, outFile);
                        }
                    }
                }
            }
        }

    }
}
