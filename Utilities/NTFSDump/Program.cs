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
using DiscUtils;
using DiscUtils.Common;
using DiscUtils.Ntfs;
using DiscUtils.Streams;

namespace NTFSDump
{
    class Program : ProgramBase
    {
        private CommandLineMultiParameter _diskFiles;
        private CommandLineSwitch _showHidden;
        private CommandLineSwitch _showSystem;
        private CommandLineSwitch _showMeta;

        static void Main(string[] args)
        {
            DiscUtils.Containers.SetupHelper.SetupContainers();
            DiscUtils.Transports.SetupHelper.SetupTransports();

            Program program = new Program();
            program.Run(args);
        }

        protected override StandardSwitches DefineCommandLine(CommandLineParser parser)
        {
            _diskFiles = FileOrUriMultiParameter("disk", "Paths to the disks to inspect.", false);
            _showHidden = new CommandLineSwitch("H", "hidden", null, "Don't hide files and directories with the hidden attribute set in the directory listing.");
            _showSystem = new CommandLineSwitch("S", "system", null, "Don't hide files and directories with the system attribute set in the directory listing.");
            _showMeta = new CommandLineSwitch("M", "meta", null, "Don't hide files and directories that are part of the file system itself in the directory listing.");

            parser.AddMultiParameter(_diskFiles);
            parser.AddSwitch(_showHidden);
            parser.AddSwitch(_showSystem);
            parser.AddSwitch(_showMeta);

            return StandardSwitches.UserAndPassword | StandardSwitches.PartitionOrVolume;
        }

        protected override void DoRun()
        {
            VolumeManager volMgr = new VolumeManager();
            foreach (string disk in _diskFiles.Values)
            {
                volMgr.AddDisk(VirtualDisk.OpenDisk(disk, FileAccess.Read, UserName, Password));
            }
            
            Stream partitionStream = null;
            if (!string.IsNullOrEmpty(VolumeId))
            {
                partitionStream = volMgr.GetVolume(VolumeId).Open();
            }
            else if (Partition >= 0)
            {
                partitionStream = volMgr.GetPhysicalVolumes()[Partition].Open();
            }
            else
            {
                partitionStream = volMgr.GetLogicalVolumes()[0].Open();
            }

            SparseStream cacheStream = SparseStream.FromStream(partitionStream, Ownership.None);
            cacheStream = new BlockCacheStream(cacheStream, Ownership.None);

            NtfsFileSystem fs = new NtfsFileSystem(cacheStream);
            fs.NtfsOptions.HideHiddenFiles = !_showHidden.IsPresent;
            fs.NtfsOptions.HideSystemFiles = !_showSystem.IsPresent;
            fs.NtfsOptions.HideMetafiles = !_showMeta.IsPresent;


            fs.Dump(Console.Out, "");
        }
    }
}
