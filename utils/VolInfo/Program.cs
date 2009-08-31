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
using DiscUtils.Iscsi;

namespace VolInfo
{
    class Program
    {
        private static CommandLineMultiParameter _inFiles;
        private static CommandLineSwitch _userName;
        private static CommandLineSwitch _password;
        private static CommandLineSwitch _helpSwitch;
        private static CommandLineSwitch _quietSwitch;

        static void Main(string[] args)
        {
            _inFiles = new CommandLineMultiParameter("disk", "Paths to the disks to inspect.  Values can be a file path, or a path to an iSCSI LUN (iscsi://<address>), for example iscsi://192.168.1.2/iqn.2002-2004.example.com:port1?LUN=2.  Use iSCSIBrowse to discover this address.", false);
            _userName = new CommandLineSwitch("u", "user", "user_name", "If using iSCSI, optionally use this parameter to specify the user name to authenticate with.  If this parameter is specified without a password, you will be prompted to supply the password.");
            _password = new CommandLineSwitch("pw", "password", "secret", "If using iSCSI, optionally use this parameter to specify the password to authenticate with.");
            _helpSwitch = new CommandLineSwitch(new string[] { "h", "?" }, "help", null, "Show this help.");
            _quietSwitch = new CommandLineSwitch("q", "quiet", null, "Run quietly.");

            CommandLineParser parser = new CommandLineParser("VolInfo");
            parser.AddMultiParameter(_inFiles);
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

            string user = _userName.IsPresent ? _userName.Value : null;
            string password = _password.IsPresent ? _password.Value : null;

            VolumeManager volMgr = new VolumeManager();
            foreach (var path in _inFiles.Values)
            {
                volMgr.AddDisk(Utilities.OpenDisk(path, FileAccess.Read, user, password));
            }

            Console.WriteLine("PHYSICAL VOLUMES");
            foreach (var physVol in volMgr.GetPhysicalVolumes())
            {
                Console.WriteLine("  Identity: " + physVol.Identity);
                Console.WriteLine("      Type: " + physVol.VolumeType);
                Console.WriteLine("   Disk Id: " + physVol.DiskIdentity);
                Console.WriteLine("  Disk Sig: " + physVol.DiskSignature.ToString("X8"));
                Console.WriteLine("   Part Id: " + physVol.PartitionIdentity);
                Console.WriteLine("    Length: " + physVol.Length + " bytes");
                Console.WriteLine();
            }

            Console.WriteLine("LOGICAL VOLUMES");
            foreach (var logVol in volMgr.GetLogicalVolumes())
            {
                Console.WriteLine("  Identity: " + logVol.Identity);
                Console.WriteLine("    Length: " + logVol.Length + " bytes");
                Console.WriteLine();
            }
        }

    }
}
