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
using System.IO;
using DiscUtils.Common;
using DiscUtils.Iso9660;

namespace ISOCreate
{
    class Program : ProgramBase
    {
        private CommandLineParameter _isoFileParam;
        private CommandLineParameter _srcDir;
        private CommandLineParameter _bootImage;
        private CommandLineSwitch _volLabelSwitch;

        static void Main(string[] args)
        {
            Program program = new Program();
            program.Run(args);
        }

        protected override StandardSwitches DefineCommandLine(CommandLineParser parser)
        {
            _isoFileParam = new CommandLineParameter("iso_file", "The ISO file to create.", false);
            _srcDir = new CommandLineParameter("sourcedir", "The directory to be added to the ISO", false);
            _bootImage = new CommandLineParameter("bootimage", "The bootable disk image, to create a bootable ISO", true);
            _volLabelSwitch = new CommandLineSwitch("vl", "vollabel", "label", "Volume Label for the ISO file.");

            parser.AddParameter(_isoFileParam);
            parser.AddParameter(_srcDir);
            parser.AddParameter(_bootImage);
            parser.AddSwitch(_volLabelSwitch);

            return StandardSwitches.Default;
        }

        protected override void DoRun()
        {
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

            if (_bootImage.IsPresent)
            {
                builder.SetBootImage(new FileStream(_bootImage.Value, FileMode.Open, FileAccess.Read), BootDeviceEmulation.NoEmulation, 0);
            }

            PopulateFromFolder(builder, di, di.FullName);

            builder.Build(_isoFileParam.Value);
        }

        private static void PopulateFromFolder(CDBuilder builder, DirectoryInfo di, string basePath)
        {
            foreach (FileInfo file in di.GetFiles())
            {
                builder.AddFile(file.FullName.Substring(basePath.Length), file.FullName);
            }

            foreach (DirectoryInfo dir in di.GetDirectories())
            {
                PopulateFromFolder(builder, dir, basePath);
            }
        }
    }
}
