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
using DiscUtils.Iso9660;

namespace ISOCreate
{
    class Program
    {
        private static CommandLineParameter _isoFileParam;
        private static CommandLineParameter _srcDir;
        private static CommandLineSwitch _helpSwitch;
        private static CommandLineSwitch _quietSwitch;
        private static CommandLineSwitch _volLabelSwitch;

        static void Main(string[] args)
        {
            _isoFileParam = new CommandLineParameter("iso_file", "The ISO file to create.", false);
            _srcDir = new CommandLineParameter("sourcedir", "The directory to be added to the ISO", false);
            _helpSwitch = new CommandLineSwitch(new string[] { "h", "?" }, "help", null, "Show this help.");
            _quietSwitch = new CommandLineSwitch("q", "quiet", null, "Run quietly.");
            _volLabelSwitch = new CommandLineSwitch("vl", "vollabel", "label", "Volume Label for the ISO file.");

            CommandLineParser parser = new CommandLineParser("ISOCreate");
            parser.AddParameter(_isoFileParam);
            parser.AddParameter(_srcDir);
            parser.AddSwitch(_helpSwitch);
            parser.AddSwitch(_quietSwitch);
            parser.AddSwitch(_volLabelSwitch);

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


            DirectoryInfo di = new DirectoryInfo(_srcDir.Value);
            if (!di.Exists)
            {
                Console.WriteLine("The source directory doesn't exist!");
                Environment.Exit(1);
            }

            CDBuilder builder = new CDBuilder();

            if (_volLabelSwitch.IsPresent)
            {
                builder.VolumeIdentifier = _volLabelSwitch.Value;
            }


            PopulateFromFolder(builder, di, di.FullName);

            builder.Build(_isoFileParam.Value);
        }

        private static void ShowHeader()
        {
            Console.WriteLine("ISOCreate v{0}, available from http://codeplex.com/DiscUtils", GetVersion());
            Console.WriteLine("Copyright (c) Kenneth Bell, 2008-2009");
            Console.WriteLine("Free software issued under the MIT License, see LICENSE.TXT for details.");
            Console.WriteLine();
        }

        private static string GetVersion()
        {
            return typeof(Program).Assembly.GetName().Version.ToString(3);
        }

        private static void PopulateFromFolder(CDBuilder builder, DirectoryInfo di, string basePath)
        {
            foreach (FileInfo file in di.GetFiles())
            {
                builder.AddFile(file.FullName.Substring(basePath.Length), file.FullName);
            }

            foreach(DirectoryInfo dir in di.GetDirectories())
            {
                PopulateFromFolder(builder, dir, basePath);
            }
        }
    }
}
