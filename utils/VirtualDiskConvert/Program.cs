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

namespace VirtualDiskConvert
{
    class Program
    {
        private static CommandLineParameter _inFile;
        private static CommandLineSwitch _outFormat;
        private static CommandLineParameter _outFile;
        private static CommandLineSwitch _userName;
        private static CommandLineSwitch _password;
        private static CommandLineSwitch _wipe;
        private static CommandLineSwitch _helpSwitch;
        private static CommandLineSwitch _quietSwitch;

        static void Main(string[] args)
        {
            _inFile = new CommandLineParameter("in_file", "Path to the disk to convert.  This can be a file path, or a path to an iSCSI LUN (iscsi://<address>), for example iscsi://192.168.1.2/iqn.2002-2004.example.com:port1?LUN=2.  Use iSCSIBrowse to discover this address.", false);
            _outFormat = new CommandLineSwitch("of", "outputFormat", "format", "The type of disk to output, one of VMDK-fixed, VMDK-dynamic, VMDK-vmfsFixed, VMDK-vmfsDynamic, VHD-fixed, VHD-dynamic, VDI-dynamic, VDI-fixed or iSCSI.");
            _outFile = new CommandLineParameter("out_file", "Path to the output file.  This can be a file path, or a path to an iSCSI LUN (iscsi://<address>), for example iscsi://192.168.1.2/iqn.2002-2004.example.com:port1?LUN=2.  Use iSCSIBrowse to discover this address.", false);
            _userName = new CommandLineSwitch("u", "user", "user_name", "If using an iSCSI source or target, optionally use this parameter to specify the user name to authenticate with.  If this parameter is specified without a password, you will be prompted to supply the password.");
            _password = new CommandLineSwitch("p", "password", "secret", "If using an iSCSI source or target, optionally use this parameter to specify the password to authenticate with.");
            _wipe = new CommandLineSwitch("w", "wipe", null, "Write zero's to all unused parts of the disk.  This option only makes sense when converting to an iSCSI LUN which may be dirty.");
            _helpSwitch = new CommandLineSwitch(new string[] { "h", "?" }, "help", null, "Show this help.");
            _quietSwitch = new CommandLineSwitch("q", "quiet", null, "Run quietly.");

            CommandLineParser parser = new CommandLineParser("VirtualDiskConvert");
            parser.AddParameter(_inFile);
            parser.AddSwitch(_outFormat);
            parser.AddParameter(_outFile);
            parser.AddSwitch(_userName);
            parser.AddSwitch(_password);
            parser.AddSwitch(_wipe);
            parser.AddSwitch(_helpSwitch);
            parser.AddSwitch(_quietSwitch);

            bool parseResult = parser.Parse(args);

            if (!_quietSwitch.IsPresent)
            {
                ShowHeader();
            }

            if (_helpSwitch.IsPresent || !parseResult || !_outFormat.IsPresent)
            {
                string remark = "This utility flattens disk hierarchies (VMDK linked-clones, VHD differencing disks) into a single disk image, but does preserve sparseness where the output disk format supports it.";
                parser.DisplayHelp(remark);
                return;
            }


            using (VirtualDisk inDisk = OpenInputDisk())
            using (VirtualDisk outDisk = OpenOutputDisk(inDisk))
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

        private static VirtualDisk OpenOutputDisk(VirtualDisk source)
        {
            switch (_outFormat.Value.ToUpperInvariant())
            {
                case "VMDK-FIXED":
                    return DiscUtils.Vmdk.Disk.Initialize(_outFile.Value, source.Capacity, source.Geometry, DiscUtils.Vmdk.DiskCreateType.MonolithicFlat);
                case "VMDK-DYNAMIC":
                    return DiscUtils.Vmdk.Disk.Initialize(_outFile.Value, source.Capacity, source.Geometry, DiscUtils.Vmdk.DiskCreateType.MonolithicSparse);
                case "VMDK-VMFSFIXED":
                    return DiscUtils.Vmdk.Disk.Initialize(_outFile.Value, source.Capacity, source.Geometry, DiscUtils.Vmdk.DiskCreateType.Vmfs);
                case "VMDK-VMFSDYNAMIC":
                    return DiscUtils.Vmdk.Disk.Initialize(_outFile.Value, source.Capacity, source.Geometry, DiscUtils.Vmdk.DiskCreateType.VmfsSparse);
                case "VHD-FIXED":
                    return DiscUtils.Vhd.Disk.InitializeFixed(new FileStream(_outFile.Value, FileMode.Create, FileAccess.ReadWrite), Ownership.Dispose, source.Capacity, source.Geometry);
                case "VHD-DYNAMIC":
                    return DiscUtils.Vhd.Disk.InitializeDynamic(new FileStream(_outFile.Value, FileMode.Create, FileAccess.ReadWrite), Ownership.Dispose, source.Capacity, source.Geometry);
                case "VDI-FIXED":
                    return DiscUtils.Vdi.Disk.InitializeFixed(new FileStream(_outFile.Value, FileMode.Create, FileAccess.ReadWrite), Ownership.Dispose, source.Capacity);
                case "VDI-DYNAMIC":
                    return DiscUtils.Vdi.Disk.InitializeDynamic(new FileStream(_outFile.Value, FileMode.Create, FileAccess.ReadWrite), Ownership.Dispose, source.Capacity);
                case "ISCSI":
                    return OpenIScsiDisk(_outFile.Value);
                default:
                    throw new NotSupportedException(_outFormat.Value + " is not a recognized disk type");
            }
        }

        private static VirtualDisk OpenInputDisk()
        {
            if (_inFile.Value.StartsWith("iscsi://", StringComparison.OrdinalIgnoreCase))
            {
                return OpenIScsiDisk(_inFile.Value);
            }
            else if (_inFile.Value.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase))
            {
                return new DiscUtils.Vhd.Disk(_inFile.Value);
            }
            else if (_inFile.Value.EndsWith(".vmdk", StringComparison.OrdinalIgnoreCase))
            {
                return new DiscUtils.Vmdk.Disk(_inFile.Value, FileAccess.Read);
            }
            else if (_inFile.Value.EndsWith(".vdi", StringComparison.OrdinalIgnoreCase))
            {
                return new DiscUtils.Vdi.Disk(new FileStream(_inFile.Value, FileMode.Open, FileAccess.Read), Ownership.Dispose);
            }
            else
            {
                throw new NotSupportedException(_inFile.Value + " is not a recognised disk image type");
            }
        }

        private static VirtualDisk OpenIScsiDisk(string path)
        {
            string targetAddress;
            string targetName;
            string lun = null;

            if (!path.StartsWith("iscsi://", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The iSCSI address is invalid");
            }

            int targetAddressEnd = path.IndexOf('/', 8);
            if (targetAddressEnd < 8)
            {
                throw new ArgumentException("The iSCSI address is invalid");
            }
            targetAddress = path.Substring(8, targetAddressEnd - 8);


            int targetNameEnd = path.IndexOf('?', targetAddressEnd + 1);
            if (targetNameEnd < targetAddressEnd)
            {
                targetName = path.Substring(targetAddressEnd + 1);
            }
            else
            {
                targetName = path.Substring(targetAddressEnd + 1, targetNameEnd - (targetAddressEnd + 1));

                string[] parms = path.Substring(targetNameEnd + 1).Split('&');

                foreach (string param in parms)
                {
                    if (param.StartsWith("LUN=", StringComparison.OrdinalIgnoreCase))
                    {
                        lun = param.Substring(4);
                    }
                }
            }

            if (lun == null)
            {
                throw new ArgumentException("No LUN specified in address", "path");
            }

            Initiator initiator = new Initiator();

            if (_userName.IsPresent)
            {
                string password;
                if (_password.IsPresent)
                {
                    password = _password.Value;
                }
                else
                {
                    password = Utilities.PromptForPassword();
                }

                initiator.SetCredentials(_userName.Value, password);
            }

            Session session = initiator.ConnectTo(targetName, targetAddress);
            foreach (var lunInfo in session.GetLuns())
            {
                if (lunInfo.ToString() == lun)
                {
                    return session.OpenDisk(lunInfo.Lun);
                }
            }

            throw new FileNotFoundException("The iSCSI lun could not be found", path);
        }

        private static void ShowHeader()
        {
            Console.WriteLine("VirtualDiskConvert v{0}, available from http://codeplex.com/DiscUtils", GetVersion());
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
