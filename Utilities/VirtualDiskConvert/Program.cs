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
using DiscUtils.Partitions;
using DiscUtils.Setup;
using DiscUtils.Streams;

namespace VirtualDiskConvert
{
    class Program : ProgramBase
    {
        private CommandLineParameter _inFile;
        private CommandLineParameter _outFile;
        private CommandLineEnumSwitch<GeometryTranslation> _translation;
        private CommandLineSwitch _wipe;

        static void Main(string[] args)
        {
            SetupHelper.RegisterAssembly(typeof(NtfsFileSystem).Assembly);

            Program program = new Program();
            program.Run(args);
        }

        protected override StandardSwitches DefineCommandLine(CommandLineParser parser)
        {
            _inFile = FileOrUriParameter("in_file", "Path to the source disk.", false);
            _outFile = FileOrUriParameter("out_file", "Path to the output disk.", false);
            _translation = new CommandLineEnumSwitch<GeometryTranslation>("t", "translation", "mode", GeometryTranslation.None, "Indicates the geometry adjustment to apply for bootable disks.  Set this parameter to match the translation configured in the BIOS of the machine that will boot from the disk - auto should work in most cases for modern BIOS.");
            _wipe = new CommandLineSwitch("w", "wipe", null, "Write zero's to all unused parts of the disk.  This option only makes sense when converting to an iSCSI LUN which may be dirty.");

            parser.AddParameter(_inFile);
            parser.AddParameter(_outFile);
            parser.AddSwitch(_translation);
            parser.AddSwitch(_wipe);

            return StandardSwitches.OutputFormatAndAdapterType | StandardSwitches.UserAndPassword;
        }

        protected override void DoRun()
        {
            using (VirtualDisk inDisk = VirtualDisk.OpenDisk(_inFile.Value, FileAccess.Read, UserName, Password))
            {
                VirtualDiskParameters diskParams = inDisk.Parameters;
                diskParams.AdapterType = AdapterType;

                VirtualDiskTypeInfo diskTypeInfo = VirtualDisk.GetDiskType(OutputDiskType, OutputDiskVariant);
                if (diskTypeInfo.DeterministicGeometry)
                {
                    diskParams.Geometry = diskTypeInfo.CalcGeometry(diskParams.Capacity);
                }

                if (_translation.IsPresent && _translation.EnumValue != GeometryTranslation.None)
                {
                    diskParams.BiosGeometry = diskParams.Geometry.TranslateToBios(diskParams.Capacity, _translation.EnumValue);
                }
                else if (!inDisk.DiskTypeInfo.PreservesBiosGeometry)
                {
                    // In case the BIOS geometry was just a default, it's better to override based on the physical geometry
                    // of the new disk.
                    diskParams.BiosGeometry = Geometry.MakeBiosSafe(diskParams.Geometry, diskParams.Capacity);
                }

                using (VirtualDisk outDisk = VirtualDisk.CreateDisk(OutputDiskType, OutputDiskVariant, _outFile.Value, diskParams, UserName, Password))
                {
                    if (outDisk.Capacity < inDisk.Capacity)
                    {
                        Console.WriteLine("ERROR: The output disk is smaller than the input disk, conversion aborted");
                    }

                    SparseStream contentStream = inDisk.Content;

                    if (_translation.IsPresent && _translation.EnumValue != GeometryTranslation.None)
                    {
                        SnapshotStream ssStream = new SnapshotStream(contentStream, Ownership.None);
                        ssStream.Snapshot();

                        UpdateBiosGeometry(ssStream, inDisk.BiosGeometry, diskParams.BiosGeometry);

                        contentStream = ssStream;
                    }

                    StreamPump pump = new StreamPump()
                    {
                        InputStream = contentStream,
                        OutputStream = outDisk.Content,
                        SparseCopy = !_wipe.IsPresent
                    };

                    if (!Quiet)
                    {
                        long totalBytes = contentStream.Length;
                        if (!_wipe.IsPresent)
                        {
                            totalBytes = 0;
                            foreach (var se in contentStream.Extents)
                            {
                                totalBytes += se.Length;
                            }
                        }

                        DateTime now = DateTime.Now;
                        pump.ProgressEvent += (o, e) => { ShowProgress("Progress", totalBytes, now, o, e); };
                    }

                    pump.Run();
                }
            }
        }

        protected override string[] HelpRemarks
        {
            get
            {
                return new string[] {
                    "This utility flattens disk hierarchies (VMDK linked-clones, VHD differencing disks) " +
                    "into a single disk image, but does preserve sparseness where the output disk format " +
                    "supports it."
                };
            }
        }

        private static void UpdateBiosGeometry(SparseStream contentStream, Geometry oldGeometry, Geometry newGeometry)
        {
            BiosPartitionTable partTable = new BiosPartitionTable(contentStream, oldGeometry);
            partTable.UpdateBiosGeometry(newGeometry);

            VolumeManager volMgr = new VolumeManager(contentStream);
            foreach (var volume in volMgr.GetLogicalVolumes())
            {
                foreach (var fsInfo in FileSystemManager.DetectFileSystems(volume.Open()))
                {
                    if (fsInfo.Name == "NTFS")
                    {
                        using (NtfsFileSystem fs = new NtfsFileSystem(volume.Open()))
                        {
                            fs.UpdateBiosGeometry(newGeometry);
                        }
                    }
                }
            }
        }

    }
}
