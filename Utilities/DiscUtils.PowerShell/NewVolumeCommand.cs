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
using System.Management.Automation;
using DiscUtils.Partitions;
using DiscUtils.PowerShell.VirtualDiskProvider;

namespace DiscUtils.PowerShell
{
    [Cmdlet(VerbsCommon.New, "Volume")]
    public class NewVolumeCommand : PSCmdlet
    {
        [Parameter(ValueFromPipeline = true)]
        public PSObject InputObject { get; set; }

        [Parameter(Position = 0)]
        public string LiteralPath { get; set; }

        [Parameter]
        public string Size { get; set; }

        [Parameter]
        public WellKnownPartitionType Type { get; set; }

        [Parameter]
        public SwitchParameter Active { get; set; }

        public NewVolumeCommand()
        {
            Type = WellKnownPartitionType.WindowsNtfs;
        }

        protected override void ProcessRecord()
        {
            PSObject diskObject = null;
            VirtualDisk disk = null;

            if (InputObject != null)
            {
                diskObject = InputObject;
                disk = diskObject.BaseObject as VirtualDisk;
            }
            if (disk == null && string.IsNullOrEmpty(LiteralPath))
            {
                WriteError(new ErrorRecord(
                    new ArgumentException("No disk specified"),
                    "NoDiskSpecified",
                    ErrorCategory.InvalidArgument,
                    null));
                return;
            }

            if (disk == null)
            {
                diskObject = SessionState.InvokeProvider.Item.Get(LiteralPath)[0];
                VirtualDisk vdisk = diskObject.BaseObject as VirtualDisk;

                if (vdisk == null)
                {
                    WriteError(new ErrorRecord(
                        new ArgumentException("Path specified is not a virtual disk"),
                        "BadDiskSpecified",
                        ErrorCategory.InvalidArgument,
                        null));
                    return;

                }

                disk = vdisk;
            }

            int newIndex;
            if (string.IsNullOrEmpty(Size))
            {
                newIndex = disk.Partitions.Create(Type, Active);
            }
            else
            {
                long size;
                if (!DiscUtils.Common.Utilities.TryParseDiskSize(Size, out size))
                {
                    WriteError(new ErrorRecord(
                        new ArgumentException("Unable to parse the volume size"),
                        "BadVolumeSize",
                        ErrorCategory.InvalidArgument,
                        null));
                    return;
                }

                newIndex = disk.Partitions.Create(size, Type, Active);
            }

            long startSector = disk.Partitions[newIndex].FirstSector;
            VolumeManager volMgr = null;

            // Changed volume layout, force a rescan
            var drive = diskObject.Properties["PSDrive"].Value as VirtualDiskPSDriveInfo;
            if (drive != null)
            {
                drive.RescanVolumes();
                volMgr = drive.VolumeManager;
            }
            else
            {
                volMgr = new VolumeManager(disk);
            }

            foreach (var vol in volMgr.GetLogicalVolumes())
            {
                if (vol.PhysicalStartSector == startSector)
                {
                    WriteObject(vol);
                }
            }
        }
    }
}
