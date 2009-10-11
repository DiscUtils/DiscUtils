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
using DiscUtils.BootConfig;
using DiscUtils.Common;
using DiscUtils.Registry;

namespace BCDDump
{
    class Program
    {
        private static CommandLineParameter _bcdFile;
        private static CommandLineSwitch _helpSwitch;
        private static CommandLineSwitch _quietSwitch;

        static void Main(string[] args)
        {
            _bcdFile = new CommandLineParameter("bcd_file", "Path to the BCD file to inspect.", false);
            _helpSwitch = new CommandLineSwitch(new string[] { "h", "?" }, "help", null, "Show this help.");
            _quietSwitch = new CommandLineSwitch("q", "quiet", null, "Run quietly.");

            CommandLineParser parser = new CommandLineParser("BCDDump");
            parser.AddParameter(_bcdFile);
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

            using (Stream fileStream = File.OpenRead(_bcdFile.Value))
            {
                RegistryHive hive = new RegistryHive(fileStream);

                Store bcdDb = new Store(hive.Root);
                foreach (var obj in bcdDb.Objects)
                {
                    Console.WriteLine(obj.FriendlyName + ":");
                    Console.WriteLine("               Id: " + obj.ToString());
                    Console.WriteLine("             Type: " + obj.ObjectType);
                    Console.WriteLine("   App Image Type: " + obj.ApplicationImageType);
                    Console.WriteLine("         App Type: " + obj.ApplicationType);
                    Console.WriteLine("  App can inherit: " + obj.IsInheritableBy(ObjectType.Application));
                    Console.WriteLine("  Dev can inherit: " + obj.IsInheritableBy(ObjectType.Device));
                    Console.WriteLine("  ELEMENTS");
                    foreach (var elem in obj.Elements)
                    {
                        Console.WriteLine("    " + elem.FriendlyName + ":");
                        Console.WriteLine("          Id: " + elem.ToString());
                        Console.WriteLine("       Class: " + elem.Class);
                        Console.WriteLine("      Format: " + elem.Format);
                        Console.WriteLine("       Value: " + elem.Value);
                    }

                    Console.WriteLine();
                }
            }
        }
    }
}
