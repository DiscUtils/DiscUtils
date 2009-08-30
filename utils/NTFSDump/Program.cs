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

namespace NTFSDump
{
    class Program
    {
        private static CommandLineParameter _diskFile;
        private static CommandLineSwitch _partition;
        private static CommandLineSwitch _showHidden;
        private static CommandLineSwitch _showSystem;
        private static CommandLineSwitch _showMeta;
        private static CommandLineSwitch _helpSwitch;
        private static CommandLineSwitch _quietSwitch;

        static void Main(string[] args)
        {
            _diskFile = new CommandLineParameter("disk_file", "The name of the disk image file to inspect.", false);
            _partition = new CommandLineSwitch("p", "partition", "num", "The number of the partition to inspect, in the range 0-n.  If not specified, 0 (the first partition) is the default.");
            _showHidden = new CommandLineSwitch("H", "hidden", null, "Don't hide files & directories with the hidden attribute set in the directory listing.");
            _showSystem = new CommandLineSwitch("S", "system", null, "Don't hide files & directories with the system attribute set in the directory listing.");
            _showMeta = new CommandLineSwitch("M", "meta", null, "Don't hide metafiles - files & directories that are part of the file system in the directory listing.");
            _helpSwitch = new CommandLineSwitch(new string[] { "h", "?" }, "help", null, "Show this help.");
            _quietSwitch = new CommandLineSwitch("q", "quiet", null, "Run quietly.");

            CommandLineParser parser = new CommandLineParser("NTFSDump");
            parser.AddParameter(_diskFile);
            parser.AddSwitch(_partition);
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

            int partition = 0;
            if (_partition.IsPresent && !int.TryParse(_partition.Value, out partition))
            {
                parser.DisplayHelp();
                return;
            }

            using (VirtualDisk disk = Utilities.OpenDisk(_diskFile.Value, FileAccess.Read))
            {
                using (Stream partitionStream = Utilities.OpenVolume(disk, partition))
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
}
