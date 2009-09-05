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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DiscUtils.Iscsi;

namespace DiscUtils.Common
{
    public class Utilities
    {
        public static string[] WordWrap(string text, int width)
        {
            List<string> lines = new List<string>();
            int pos = 0;

            while (pos < text.Length - width)
            {
                int start = Math.Min(pos + width, text.Length - 1);
                int count = start - pos;

                int breakPos = text.LastIndexOf(' ', start, count);

                lines.Add(text.Substring(pos, breakPos - pos).TrimEnd(' '));

                while (breakPos < text.Length && text[breakPos] == ' ')
                {
                    breakPos++;
                }
                pos = breakPos;
            }

            lines.Add(text.Substring(pos));

            return lines.ToArray();
        }

        public static VirtualDisk OpenDisk(string path, FileAccess access, string username, string password)
        {
            if (path.StartsWith("iscsi://", StringComparison.OrdinalIgnoreCase))
            {
                return OpenIScsiDisk(path, access, username, password);
            }
            else
            {
                VirtualDisk result = VirtualDisk.OpenDisk(path, access);
                if (result == null)
                {
                    throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, "{0} is not a recognised virtual disk type", path));
                }

                return result;
            }
        }

        public static VirtualDisk OpenIScsiDisk(string path, FileAccess access, string username, string password)
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

            if (!string.IsNullOrEmpty(username))
            {
                if (string.IsNullOrEmpty(password))
                {
                    password = Utilities.PromptForPassword();
                }
                initiator.SetCredentials(username, password);
            }

            Session session = initiator.ConnectTo(targetName, targetAddress);
            foreach (var lunInfo in session.GetLuns())
            {
                if (lunInfo.ToString() == lun)
                {
                    return session.OpenDisk(lunInfo.Lun, access);
                }
            }

            throw new FileNotFoundException("The iSCSI LUN could not be found", path);
        }

        public static string PromptForPassword()
        {
            Console.WriteLine();
            Console.Write("Password: ");

            ConsoleColor restoreColor = Console.ForegroundColor;
            Console.ForegroundColor = Console.BackgroundColor;
            try
            {
                return Console.ReadLine();
            }
            finally
            {
                Console.ForegroundColor = restoreColor;
            }
        }

        public static SparseStream OpenVolume(string volumeId, int partition, string user, string password, FileAccess access, params string[] disks)
        {
            VolumeManager volMgr = new VolumeManager();
            foreach (string disk in disks)
            {
                volMgr.AddDisk(OpenDisk(disk, access, user, password));
            }

            if (string.IsNullOrEmpty(volumeId))
            {
                if (partition >= 0)
                {
                    PhysicalVolumeInfo[] physicalVolumes = volMgr.GetPhysicalVolumes();

                    if (partition > physicalVolumes.Length)
                    {
                        throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Partition {0} not found", partition), "partition");
                    }

                    return physicalVolumes[partition].Open();
                }
                else
                {
                    return volMgr.GetLogicalVolumes()[0].Open();
                }
            }
            else
            {
                VolumeInfo volInfo = volMgr.GetVolume(volumeId);
                if (volInfo == null)
                {
                    throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Volume {0} not found", volumeId), "volumeId");
                }
                return volInfo.Open();
            }
        }

        public static void ShowHeader(Type program)
        {
            Console.WriteLine("{0} v{1}, available from http://discutils.codeplex.com", GetExeName(program), GetVersion(program));
            Console.WriteLine("Copyright (c) Kenneth Bell, 2008-2009");
            Console.WriteLine("Free software issued under the MIT License, see LICENSE.TXT for details.");
            Console.WriteLine();
        }

        public static string GetExeName(Type program)
        {
            return program.Assembly.GetName().Name;
        }

        public static string GetVersion(Type program)
        {
            return program.Assembly.GetName().Version.ToString(3);
        }
    }
}
