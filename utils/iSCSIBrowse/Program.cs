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
using System.Globalization;
using DiscUtils.Common;
using DiscUtils.Iscsi;

namespace iSCSIBrowse
{
    class Program
    {
        private static CommandLineParameter _portalAddress;
        private static CommandLineSwitch _userName;
        private static CommandLineSwitch _password;
        private static CommandLineSwitch _verbose;
        private static CommandLineSwitch _helpSwitch;
        private static CommandLineSwitch _quietSwitch;

        static void Main(string[] args)
        {
            _portalAddress = new CommandLineParameter("portal", "Address of the iSCSI server (aka Portal) in the form <host>[:<port>], for example 192.168.1.2:3260 or 192.168.1.2", false);
            _userName = new CommandLineSwitch("u", "user", "user_name", "The user name to authenticate with.  If this parameter is specified without a password, you will be prompted to supply the password");
            _password = new CommandLineSwitch("pw", "password", "secret", "The password to authenticate with.");
            _verbose = new CommandLineSwitch("v", "verbose", null, "Show detailed information about targets and LUNs.");
            _helpSwitch = new CommandLineSwitch(new string[] { "h", "?" }, "help", null, "Show this help.");
            _quietSwitch = new CommandLineSwitch("q", "quiet", null, "Run quietly.");

            CommandLineParser parser = new CommandLineParser("iSCSIBrowse");
            parser.AddParameter(_portalAddress);
            parser.AddSwitch(_userName);
            parser.AddSwitch(_password);
            parser.AddSwitch(_verbose);
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

            bool foundTargets = false;
            try
            {
                foreach (var target in initiator.GetTargets(_portalAddress.Value))
                {
                    foundTargets = true;
                    Console.WriteLine("Target: " + target);

                    if (_verbose.IsPresent)
                    {
                        Console.WriteLine("  Name: " + target.Name);
                        foreach (var addr in target.Addresses)
                        {
                            Console.WriteLine("  Address: " + addr);
                        }
                        Console.WriteLine();
                    }

                    using(Session s = initiator.ConnectTo(target))
                    {
                        foreach (var lun in s.GetLuns())
                        {
                            Console.WriteLine(lun.DeviceType + ": " + target + "?LUN=" + lun);

                            if (_verbose.IsPresent)
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
