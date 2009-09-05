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
using DiscUtils.Common;
using DiscUtils.Ntfs;

namespace NTFSDump
{
    class Program
    {
        private static CommandLineMultiParameter _diskFiles;
        private static CommandLineSwitch _userName;
        private static CommandLineSwitch _password;
        private static CommandLineSwitch _partition;
        private static CommandLineSwitch _volumeId;
        private static CommandLineSwitch _showHidden;
        private static CommandLineSwitch _showSystem;
        private static CommandLineSwitch _showMeta;
        private static CommandLineSwitch _helpSwitch;
        private static CommandLineSwitch _quietSwitch;

        static void Main(string[] args)
        {
            _diskFiles = new CommandLineMultiParameter("disk", "Paths to the disks to inspect.  Values can be a file path, or a path to an iSCSI LUN (iscsi://<address>), for example iscsi://192.168.1.2/iqn.2002-2004.example.com:port1?LUN=2.  Use iSCSIBrowse to discover this address.", false);
            _userName = new CommandLineSwitch("u", "user", "user_name", "If using iSCSI, optionally use this parameter to specify the user name to authenticate with.  If this parameter is specified without a password, you will be prompted to supply the password.");
            _password = new CommandLineSwitch("pw", "password", "secret", "If using iSCSI, optionally use this parameter to specify the password to authenticate with.");
            _partition = new CommandLineSwitch("p", "partition", "num", "The number of the partition to inspect, in the range 0-n.  If not specified, 0 (the first partition) is the default.");
            _volumeId = new CommandLineSwitch("v", "volume", "id", "The volume id of the volume to access, use the VolInfo tool to discover this id.  If specified, the partition parameter is ignored.");
            _showHidden = new CommandLineSwitch("H", "hidden", null, "Don't hide files and directories with the hidden attribute set in the directory listing.");
            _showSystem = new CommandLineSwitch("S", "system", null, "Don't hide files and directories with the system attribute set in the directory listing.");
            _showMeta = new CommandLineSwitch("M", "meta", null, "Don't hide files and directories that are part of the file system itself in the directory listing.");
            _helpSwitch = new CommandLineSwitch(new string[] { "h", "?" }, "help", null, "Show this help.");
            _quietSwitch = new CommandLineSwitch("q", "quiet", null, "Run quietly.");

            CommandLineParser parser = new CommandLineParser("NTFSDump");
            parser.AddMultiParameter(_diskFiles);
            parser.AddSwitch(_userName);
            parser.AddSwitch(_password);
            parser.AddSwitch(_partition);
            parser.AddSwitch(_volumeId);
            parser.AddSwitch(_showHidden);
            parser.AddSwitch(_showSystem);
            parser.AddSwitch(_showMeta);
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

            int partition = -1;
            if (_partition.IsPresent && !int.TryParse(_partition.Value, out partition))
            {
                parser.DisplayHelp();
                return;
            }

            string user = _userName.IsPresent ? _userName.Value : null;
            string password = _password.IsPresent ? _password.Value : null;

            using (Stream partitionStream = Utilities.OpenVolume(_volumeId.Value, partition, user, password, FileAccess.Read, _diskFiles.Values))
            {
                NtfsFileSystem fs = new NtfsFileSystem(partitionStream);
                fs.NtfsOptions.HideHiddenFiles = !_showHidden.IsPresent;
                fs.NtfsOptions.HideSystemFiles = !_showSystem.IsPresent;
                fs.NtfsOptions.HideMetafiles = !_showMeta.IsPresent;

                fs.Dump(Console.Out, "");
            }
        }
    }
}
