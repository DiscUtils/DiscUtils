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
    public enum VolumeManagerType
    {
        Bios = 0,
        Gpt = 1
    }

    [Cmdlet("Initialize", "VirtualDisk")]
    public class InitializeVirtualDiskCommand : PSCmdlet
    {
        [Parameter(Position = 0)]
        public string LiteralPath { get; set; }

        [Parameter(ValueFromPipeline = true)]
        public PSObject InputObject { get; set; }

        [Parameter]
        public VolumeManagerType VolumeManager { get; set; }

        [Parameter]
        public int Signature { get; set; }


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

            PartitionTable pt = null;
            if (VolumeManager == VolumeManagerType.Bios)
            {
                pt = BiosPartitionTable.Initialize(disk);
            }
            else
            {
                pt = GuidPartitionTable.Initialize(disk);
            }

            if (Signature != 0)
            {
                disk.Signature = Signature;
            }
            else
            {
                disk.Signature = new Random().Next();
            }

            // Changed volume layout, force a rescan
            var drive = diskObject.Properties["PSDrive"].Value as VirtualDiskPSDriveInfo;
            if (drive != null)
            {
                drive.RescanVolumes();
            }


            WriteObject(disk);
        }
    }
}
