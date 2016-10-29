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
using System.IO;
using DiscUtils.Setup;

namespace DmgExtract
{
    class Program : ProgramBase
    {
        CommandLineParameter _dmg;
        CommandLineParameter _folder;
        CommandLineSwitch _recursive;

        static void Main(string[] args)
        {
            SetupHelper.RegisterAssembly(typeof(HfsPlusFileSystem).Assembly);

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
            using (var disk = VirtualDisk.OpenDisk(_dmg.Value, FileAccess.Read))
            {
                // Find the first (and supposedly, only, HFS partition)

                foreach (var volume in VolumeManager.GetPhysicalVolumes(disk))
                {
                    foreach (var fileSystem in FileSystemManager.DetectFileSystems(volume))
                    {
                        if (fileSystem.Name == "HFS+")
                        {
                            using (HfsPlusFileSystem hfs = (HfsPlusFileSystem)fileSystem.Open(volume))
                            {
                                var source = hfs.GetDirectoryInfo(_folder.Value);
                                var target = new DirectoryInfo(Path.Combine(Environment.CurrentDirectory, source.Name));

                                if (target.Exists)
                                {
                                    target.Delete(true);
                                }

                                target.Create();

                                CopyDirectory(source, target, _recursive.IsPresent);
                            }
                            break;
                        }
                    }
                }
            }
        }

        private void CopyDirectory(DiscDirectoryInfo source, DirectoryInfo target, bool recurse)
        {
            if (recurse)
            {
                foreach (var childDiscDirectory in source.GetDirectories())
                {
                    DirectoryInfo childDirectory = target.CreateSubdirectory(childDiscDirectory.Name);
                    CopyDirectory(childDiscDirectory, childDirectory, recurse);
                }
            }

            Console.WriteLine("{0}", source.Name);

            foreach (var childFile in source.GetFiles())
            {
                using (Stream sourceStream = childFile.OpenRead())
                using (Stream targetStream = File.OpenWrite(Path.Combine(target.FullName, childFile.Name)))
                {
                    sourceStream.CopyTo(targetStream);
                }
            }
        }
    }
}
