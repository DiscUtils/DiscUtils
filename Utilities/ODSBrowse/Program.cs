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

using DiscUtils.Common;
using DiscUtils.OpticalDiscSharing;
using System;

namespace ODSBrowse
{
    class Program : ProgramBase
    {
        private CommandLineParameter _host;

        static void Main(string[] args)
        {
            Program program = new Program();
            program.Run(args);
        }

        protected override StandardSwitches DefineCommandLine(CommandLineParser parser)
        {
            _host = new CommandLineParameter("host", "The name of a Mac / PC sharing its optical disk(s).  For example \"My Computer\".", true);
            parser.AddParameter(_host);

            return StandardSwitches.Default;
        }

        protected override void DoRun()
        {
            OpticalDiscServiceClient odsClient = new OpticalDiscServiceClient();

            if (_host.IsPresent)
            {
                bool found = false;

                foreach (var service in odsClient.LookupServices())
                {
                    if (_host.Value == service.DisplayName || _host.Value == Uri.EscapeDataString(service.DisplayName))
                    {
                        found = true;

                        Console.WriteLine("Connecting to " + service.DisplayName + " - the owner may need to accept...");
                        service.Connect(Environment.UserName, Environment.MachineName, 30);

                        ShowService(service);

                        break;
                    }
                }

                if (!found)
                {
                    Console.WriteLine("Host not found");
                }
            }
            else
            {
                foreach (var service in odsClient.LookupServices())
                {
                    ShowService(service);
                    Console.WriteLine();
                }
            }
        }

        private static void ShowService(OpticalDiscService service)
        {
            Console.WriteLine();
            Console.WriteLine("Service: " + service.DisplayName);
            Console.WriteLine("  Safe Name: " + Uri.EscapeDataString(service.DisplayName) + "  (for URLs, copy+paste)");
            Console.WriteLine();

            bool foundDisk = false;
            foreach (var disk in service.AdvertisedDiscs)
            {
                foundDisk = true;
                Console.WriteLine("  Disk: " + disk.VolumeLabel);
                Console.WriteLine("    Name: " + disk.Name);
                Console.WriteLine("    Type: " + disk.VolumeType);
                Console.WriteLine("     Url: " + Uri.EscapeUriString("ods://local/" + service.DisplayName + "/" + disk.VolumeLabel));
            }

            if (!foundDisk)
            {
                Console.WriteLine("  [No disks found - try specifying host to connect for full list]");
            }
        }
    }
}
