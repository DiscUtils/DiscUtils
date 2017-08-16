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
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using DiscUtils;
using DiscUtils.Common;
using DiscUtils.Ntfs;
using DiscUtils.Partitions;
using DiscUtils.Streams;

namespace DiskClone
{
    class CloneVolume
    {
        public NativeMethods.DiskExtent SourceExtent;
        public string Path;
        public Guid SnapshotId;
        public VssSnapshotProperties SnapshotProperties;
    }

    class Program : ProgramBase
    {
        private CommandLineEnumSwitch<GeometryTranslation> _translation;
        private CommandLineMultiParameter _volumes;
        private CommandLineParameter _destDisk;

        static void Main(string[] args)
        {
            Program program = new Program();
            program.Run(args);
        }

        protected override StandardSwitches DefineCommandLine(CommandLineParser parser)
        {
            _translation = new CommandLineEnumSwitch<GeometryTranslation>("t", "translation", "mode", GeometryTranslation.Auto,"Indicates the geometry adjustment to apply.  Set this parameter to match the translation configured in the BIOS of the machine that will boot from the disk - auto should work in most cases for modern BIOS.");
            _volumes = new CommandLineMultiParameter("volume", "Volumes to clone.  The volumes should all be on the same disk.", false);
            _destDisk = new CommandLineParameter("out_file", "Path to the output disk image.", false);

            parser.AddSwitch(_translation);
            parser.AddMultiParameter(_volumes);
            parser.AddParameter(_destDisk);

            return StandardSwitches.OutputFormatAndAdapterType;
        }

        protected override string[] HelpRemarks
        {
            get
            {
                return new string[]
                {
                    "DiskClone clones a live disk into a virtual disk file.  The volumes cloned must be formatted with NTFS, and partitioned using a conventional partition table.",
                    "Only Windows 7 is supported.",
                    "The tool must be run with administrator privilege."
                };
            }
        }

        protected override void DoRun()
        {
            if (!IsAdministrator())
            {
                Console.WriteLine("\nThis utility must be run as an administrator!\n");
                Environment.Exit(1);
            }

            DiskImageBuilder builder = DiskImageBuilder.GetBuilder(OutputDiskType, OutputDiskVariant);
            builder.GenericAdapterType = AdapterType;

            string[] sourceVolume = _volumes.Values;

            uint diskNumber;

            List<CloneVolume> cloneVolumes = GatherVolumes(sourceVolume, out diskNumber);


            if (!Quiet)
            {
                Console.WriteLine("Inspecting Disk...");
            }

            // Construct a stream representing the contents of the cloned disk.
            BiosPartitionedDiskBuilder contentBuilder;
            Geometry biosGeometry;
            Geometry ideGeometry;
            long capacity;
            using (Disk disk = new Disk(diskNumber))
            {
                contentBuilder = new BiosPartitionedDiskBuilder(disk);
                biosGeometry = disk.BiosGeometry;
                ideGeometry = disk.Geometry;
                capacity = disk.Capacity;
            }

            // Preserve the IDE (aka Physical) geometry
            builder.Geometry = ideGeometry;

            // Translate the BIOS (aka Logical) geometry
            GeometryTranslation translation = _translation.EnumValue;
            if (builder.PreservesBiosGeometry && translation == GeometryTranslation.Auto)
            {
                // If the new format preserves BIOS geometry, then take no action if asked for 'auto'
                builder.BiosGeometry = biosGeometry;
                translation = GeometryTranslation.None;
            }
            else
            {
                builder.BiosGeometry = ideGeometry.TranslateToBios(0, translation);
            }

            if (translation != GeometryTranslation.None)
            {
                contentBuilder.UpdateBiosGeometry(builder.BiosGeometry);
            }


            IVssBackupComponents backupCmpnts;
            int status;
            if (Marshal.SizeOf(typeof(IntPtr)) == 4)
            {
                status = NativeMethods.CreateVssBackupComponents(out backupCmpnts);
            }
            else
            {
                status = NativeMethods.CreateVssBackupComponents64(out backupCmpnts);
            }


            Guid snapshotSetId = CreateSnapshotSet(cloneVolumes, backupCmpnts);

            if (!Quiet)
            {
                Console.Write("Copying Disk...");
            }


            foreach (var sv in cloneVolumes)
            {
                Volume sourceVol = new Volume(sv.SnapshotProperties.SnapshotDeviceObject, sv.SourceExtent.ExtentLength);

                SnapshotStream rawVolStream = new SnapshotStream(sourceVol.Content, Ownership.None);
                rawVolStream.Snapshot();

                byte[] volBitmap;
                int clusterSize;
                using (NtfsFileSystem ntfs = new NtfsFileSystem(rawVolStream))
                {
                    ntfs.NtfsOptions.HideSystemFiles = false;
                    ntfs.NtfsOptions.HideHiddenFiles = false;
                    ntfs.NtfsOptions.HideMetafiles = false;

                    // Remove VSS snapshot files (can be very large)
                    foreach (string filePath in ntfs.GetFiles(@"\System Volume Information", "*{3808876B-C176-4e48-B7AE-04046E6CC752}"))
                    {
                        ntfs.DeleteFile(filePath);
                    }

                    // Remove the page file
                    if (ntfs.FileExists(@"\Pagefile.sys"))
                    {
                        ntfs.DeleteFile(@"\Pagefile.sys");
                    }

                    // Remove the hibernation file
                    if (ntfs.FileExists(@"\hiberfil.sys"))
                    {
                        ntfs.DeleteFile(@"\hiberfil.sys");
                    }

                    using (Stream bitmapStream = ntfs.OpenFile(@"$Bitmap", FileMode.Open))
                    {
                        volBitmap = new byte[bitmapStream.Length];

                        int totalRead = 0;
                        int numRead = bitmapStream.Read(volBitmap, 0, volBitmap.Length - totalRead);
                        while (numRead > 0)
                        {
                            totalRead += numRead;
                            numRead = bitmapStream.Read(volBitmap, totalRead, volBitmap.Length - totalRead);
                        }
                    }

                    clusterSize = (int)ntfs.ClusterSize;

                    if (translation != GeometryTranslation.None)
                    {
                        ntfs.UpdateBiosGeometry(builder.BiosGeometry);
                    }
                }

                List<StreamExtent> extents = new List<StreamExtent>(BitmapToRanges(volBitmap, clusterSize));
                SparseStream partSourceStream = SparseStream.FromStream(rawVolStream, Ownership.None, extents);

                for (int i = 0; i < contentBuilder.PartitionTable.Partitions.Count; ++i)
                {
                    var part = contentBuilder.PartitionTable.Partitions[i];
                    if (part.FirstSector * 512 == sv.SourceExtent.StartingOffset)
                    {
                        contentBuilder.SetPartitionContent(i, partSourceStream);
                    }
                }
            }
            SparseStream contentStream = contentBuilder.Build();


            // Write out the disk images
            string dir = Path.GetDirectoryName(_destDisk.Value);
            string file = Path.GetFileNameWithoutExtension(_destDisk.Value);
            
            builder.Content = contentStream;
            DiskImageFileSpecification[] fileSpecs = builder.Build(file);

            for (int i = 0; i < fileSpecs.Length; ++i)
            {
                // Construct the destination file path from the directory of the primary file.
                string outputPath = Path.Combine(dir, fileSpecs[i].Name);

                // Force the primary file to the be one from the command-line.
                if (i == 0)
                {
                    outputPath = _destDisk.Value;
                }

                using (SparseStream vhdStream = fileSpecs[i].OpenStream())
                using (FileStream fs = new FileStream(outputPath, FileMode.Create, FileAccess.ReadWrite))
                {
                    StreamPump pump = new StreamPump()
                    {
                        InputStream = vhdStream,
                        OutputStream = fs,
                    };

                    long totalBytes = 0;
                    foreach (var se in vhdStream.Extents)
                    {
                        totalBytes += se.Length;
                    }

                    if (!Quiet)
                    {
                        Console.WriteLine();
                        DateTime now = DateTime.Now;
                        pump.ProgressEvent += (o, e) => { ShowProgress(fileSpecs[i].Name, totalBytes, now, o, e); };
                    }

                    pump.Run();

                    if (!Quiet)
                    {
                        Console.WriteLine();
                    }
                }
            }


            // Complete - tidy up
            CallAsyncMethod(backupCmpnts.BackupComplete);

            long numDeleteFailed;
            Guid deleteFailed;
            backupCmpnts.DeleteSnapshots(snapshotSetId, 2 /*VSS_OBJECT_SNAPSHOT_SET*/, true, out numDeleteFailed, out deleteFailed);

            Marshal.ReleaseComObject(backupCmpnts);
        }

        private static bool IsAdministrator()
        {
            WindowsPrincipal principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static IEnumerable<StreamExtent> BitmapToRanges(byte[] bitmap, int bytesPerCluster)
        {
            long numClusters = bitmap.Length * 8;
            long cluster = 0;
            while (cluster < numClusters && !IsSet(bitmap, cluster))
            {
                ++cluster;
            }

            while (cluster < numClusters)
            {
                long startCluster = cluster;
                while (cluster < numClusters && IsSet(bitmap, cluster))
                {
                    ++cluster;
                }

                yield return new StreamExtent((long)(startCluster * (long)bytesPerCluster), (long)((cluster - startCluster) * (long)bytesPerCluster));

                while (cluster < numClusters && !IsSet(bitmap, cluster))
                {
                    ++cluster;
                }
            }
        }

        private static bool IsSet(byte[] buffer, long bit)
        {
            int byteIdx = (int)(bit >> 3);
            if (byteIdx >= buffer.Length)
            {
                return false;
            }

            byte val = buffer[byteIdx];
            byte mask = (byte)(1 << (int)(bit & 0x7));

            return (val & mask) != 0;
        }

        private List<CloneVolume> GatherVolumes(string[] sourceVolume, out uint diskNumber)
        {
            diskNumber = uint.MaxValue;

            List<CloneVolume> cloneVolumes = new List<CloneVolume>(sourceVolume.Length);

            if (!Quiet)
            {
                Console.WriteLine("Inspecting Volumes...");
            }

            for (int i = 0; i < sourceVolume.Length; ++i)
            {
                using (Volume vol = new Volume(sourceVolume[i], 0))
                {
                    NativeMethods.DiskExtent[] sourceExtents = vol.GetDiskExtents();
                    if (sourceExtents.Length > 1)
                    {
                        Console.Error.WriteLine("Volume '{0}' is made up of multiple extents, which is not supported", sourceVolume[i]);
                        Environment.Exit(1);
                    }

                    if (diskNumber == uint.MaxValue)
                    {
                        diskNumber = sourceExtents[0].DiskNumber;
                    }
                    else if (diskNumber != sourceExtents[0].DiskNumber)
                    {
                        Console.Error.WriteLine("Specified volumes span multiple disks, which is not supported");
                        Environment.Exit(1);
                    }

                    string volPath = sourceVolume[i];
                    if (volPath[volPath.Length - 1] != '\\')
                    {
                        volPath += @"\";
                    }

                    cloneVolumes.Add(new CloneVolume { Path = volPath, SourceExtent = sourceExtents[0] });
                }
            }

            return cloneVolumes;
        }

        private Guid CreateSnapshotSet(List<CloneVolume> cloneVolumes, IVssBackupComponents backupCmpnts)
        {
            if (!Quiet)
            {
                Console.WriteLine("Snapshotting Volumes...");
            }

            backupCmpnts.InitializeForBackup(null);
            backupCmpnts.SetContext(0 /* VSS_CTX_BACKUP */);

            backupCmpnts.SetBackupState(false, true, 5 /* VSS_BT_COPY */, false);

            CallAsyncMethod(backupCmpnts.GatherWriterMetadata);

            Guid snapshotSetId;
            try
            {
                backupCmpnts.StartSnapshotSet(out snapshotSetId);
                foreach (var vol in cloneVolumes)
                {
                    backupCmpnts.AddToSnapshotSet(vol.Path, Guid.Empty, out vol.SnapshotId);
                }

                CallAsyncMethod(backupCmpnts.PrepareForBackup);

                CallAsyncMethod(backupCmpnts.DoSnapshotSet);
            }
            catch
            {
                backupCmpnts.AbortBackup();
                throw;
            }

            foreach (var vol in cloneVolumes)
            {
                vol.SnapshotProperties = GetSnapshotProperties(backupCmpnts, vol.SnapshotId);
            }

            return snapshotSetId;
        }

        private static VssSnapshotProperties GetSnapshotProperties(IVssBackupComponents backupComponents, Guid snapshotId)
        {
            VssSnapshotProperties props = new VssSnapshotProperties();

            IntPtr buffer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(VssSnapshotProperties)));

            backupComponents.GetSnapshotProperties(snapshotId, buffer);

            Marshal.PtrToStructure(buffer, props);

            NativeMethods.VssFreeSnapshotProperties(buffer);
            return props;
        }

        private delegate void VssAsyncMethod(out IVssAsync result);

        private static void CallAsyncMethod(VssAsyncMethod method)
        {
            IVssAsync async;
            int reserved = 0;
            uint hResult;

            method(out async);

            async.Wait(60 * 1000);

            async.QueryStatus(out hResult, ref reserved);

            if (hResult != 0 && hResult != 0x0004230a /* VSS_S_ASYNC_FINISHED */)
            {
                Marshal.ThrowExceptionForHR((int)hResult);
            }

        }
    }
}
