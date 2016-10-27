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
using System.Globalization;
using DiscUtils.Common;
using DiscUtils.Iscsi;

namespace iSCSIBrowse
{
    class Program : ProgramBase
    {
        private CommandLineParameter _portalAddress;

        static void Main(string[] args)
        {
            Program program = new Program();
            program.Run(args);
        }

        protected override StandardSwitches DefineCommandLine(CommandLineParser parser)
        {
            _portalAddress = new CommandLineParameter("portal", "Address of the iSCSI server (aka Portal) in the form <host>[:<port>], for example 192.168.1.2:3260 or 192.168.1.2", false);
            parser.AddParameter(_portalAddress);

            return StandardSwitches.UserAndPassword | StandardSwitches.Verbose;
        }

        protected override void DoRun()
        {
            Initiator initiator = new Initiator();

            if (!string.IsNullOrEmpty(UserName))
            {
                initiator.SetCredentials(UserName, Password);
            }

            bool foundTargets = false;
            try
            {
                foreach (var target in initiator.GetTargets(_portalAddress.Value))
                {
                    foundTargets = true;
                    Console.WriteLine("Target: " + target);

                    if (Verbose)
                    {
                        Console.WriteLine("  Name: " + target.Name);
                        foreach (var addr in target.Addresses)
                        {
                            Console.WriteLine("  Address: " + addr + "  <" + addr.ToUri() + ">");
                        }
                        Console.WriteLine();
                    }

                    using (Session s = initiator.ConnectTo(target))
                    {
                        foreach (var lun in s.GetLuns())
                        {
                            Console.WriteLine(lun.DeviceType + ": ");
                            string[] uris = lun.GetUris();
                            if (uris.Length > 1)
                            {
                                for (int i = 0; i < uris.Length; ++i)
                                {
                                    Console.WriteLine("  URI[" + i + "]: " + uris[i]);
                                }
                            }
                            else if (uris.Length > 0)
                            {
                                Console.WriteLine("  URI: " + uris[0]);
                            }

                            if (Verbose)
                            {
                                Console.WriteLine("  LUN: " + lun.Lun.ToString("x16", CultureInfo.InvariantCulture));
                                Console.WriteLine("  Device Type: " + lun.DeviceType);
                                Console.WriteLine("  Removeable: " + (lun.Removable ? "Yes" : "No"));
                                Console.WriteLine("  Vendor: " + lun.VendorId);
                                Console.WriteLine("  Product: " + lun.ProductId);
                                Console.WriteLine("  Revision: " + lun.ProductRevision);
                                Console.WriteLine();
                            }
                        }
                    }
                }

                if (!foundTargets)
                {
                    Console.WriteLine("No targets found");
                }
            }
            catch (LoginException)
            {
                Console.WriteLine("ERROR: Need credentials, or the credentials specified were invalid");
            }
        }
    }
}
