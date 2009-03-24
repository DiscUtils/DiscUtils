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
using DiscUtils.Vhd;

namespace NTFSDump
{
    class Program
    {
        private static CommandLineParameter _vhdFile;
        private static CommandLineSwitch _partition;
        private static CommandLineSwitch _showHidden;
        private static CommandLineSwitch _showSystem;
        private static CommandLineSwitch _showMeta;
        private static CommandLineSwitch _helpSwitch;
        private static CommandLineSwitch _quietSwitch;

        static void Main(string[] args)
        {
            _vhdFile = new CommandLineParameter("vhd_file", "The name of the VHD file to inspect.", false);
            _partition = new CommandLineSwitch("p", "partition", "num", "The number of the partition to inspect, in the range 0-n.  If not specified, 0 (the first partition) is the default.");
            _showHidden = new CommandLineSwitch("H", "hidden", null, "Don't hide files & directories with the hidden attribute set in the directory listing.");
            _showSystem = new CommandLineSwitch("S", "system", null, "Don't hide files & directories with the system attribute set in the directory listing.");
            _showMeta = new CommandLineSwitch("M", "meta", null, "Don't hide metafiles - files & directories that are part of the file system in the directory listing.");
            _helpSwitch = new CommandLineSwitch(new string[] { "h", "?" }, "help", null, "Show this help.");
            _quietSwitch = new CommandLineSwitch("q", "quiet", null, "Run quietly.");

            CommandLineParser parser = new CommandLineParser("NTFSDump");
            parser.AddParameter(_vhdFile);
            parser.AddSwitch(_partition);
            parser.AddSwitch(_showHidden);
            parser.AddSwitch(_showSystem);
            parser.AddSwitch(_showMeta);
            parser.AddSwitch(_helpSwitch);
            parser.AddSwitch(_quietSwitch);

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

            int partition = 0;
            if (_partition.IsPresent && !int.TryParse(_partition.Value, out partition))
            {
                parser.DisplayHelp();
                return;
            }

            using (Stream fileStream = File.OpenRead(_vhdFile.Value))
            {
                using (Disk vhdDisk = new Disk(fileStream, Ownership.None))
                {
                    using (Stream partitionStream = vhdDisk.Partitions[partition].Open())
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

        private static void ShowHeader()
        {
            Console.WriteLine("NTFSDump v{0}, available from http://codeplex.com/DiscUtils", GetVersion());
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
