//
// Copyright (c) 2014, Quamotion
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

using DiscUtils;
using DiscUtils.Common;
using DiscUtils.HfsPlus;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DmgExtract 
{
    class Program : ProgramBase
    {
        CommandLineParameter _dmg;
        CommandLineParameter _folder;
        CommandLineSwitch _recursive;

        static void Main(string[] args)
        {
            Program program = new Program();
            program.Run(args);
        }

        protected override StandardSwitches DefineCommandLine(CommandLineParser parser)
        {
            _dmg = new CommandLineParameter("dmg", "Path to the .dmg file from which to extract the files", isOptional: false);
            _folder = new CommandLineParameter("folder", "Paths to the folders from which to extract the files.", isOptional: false);
            _recursive = new CommandLineSwitch("r", "recursive", null, "Include all subfolders of the folder specified");

            parser.AddParameter(_dmg);
            parser.AddParameter(_folder);
            parser.AddSwitch(_recursive);

            return StandardSwitches.Default;
        }

        protected override void DoRun()
        {
            using(var disk = VirtualDisk.OpenDisk(_dmg.Value, FileAccess.Read))
            using(HfsPlusFileSystem hfs = new HfsPlusFileSystem(disk.Partitions[3].Open()))
            {

            }
        }
    }
}
