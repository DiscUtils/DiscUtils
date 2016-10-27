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
using System.Management.Automation;
using DiscUtils.PowerShell.VirtualDiskProvider;

namespace DiscUtils.PowerShell
{
    [Cmdlet(VerbsCommon.New, "VirtualDisk")]
    public class NewVirtualDiskCommand : PSCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        public string LiteralPath { get; set; }

        [Parameter(Mandatory = true, ParameterSetName="New")]
        [ValidateLength(1,int.MaxValue)]
        public string Type { get; set; }

        [Parameter(Mandatory = true, ParameterSetName="New")]
        public string Size { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "Diff")]
        public SwitchParameter Differencing { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "Diff")]
        public string BaseDisk { get; set; }

        protected override void ProcessRecord()
        {
            if (ParameterSetName == "New")
            {
                CreateNewDisk();
            }
            else
            {
                CreateDiffDisk();
            }
        }

        private void CreateNewDisk()
        {
            string[] typeAndVariant = Type.Split('-');
            if (typeAndVariant.Length < 1 || typeAndVariant.Length > 2)
            {
                WriteError(new ErrorRecord(
                    new ArgumentException("Invalid Type of disk"),
                    "BadDiskType",
                    ErrorCategory.InvalidArgument,
                    null));
                return;
            }

            long size;
            if (!DiscUtils.Common.Utilities.TryParseDiskSize(Size, out size))
            {
                WriteError(new ErrorRecord(
                    new ArgumentException("Unable to parse the disk size"),
                    "BadDiskSize",
                    ErrorCategory.InvalidArgument,
                    null));
                return;
            }


            string type = typeAndVariant[0];
            string variant = typeAndVariant.Length > 1 ? typeAndVariant[1] : null;

            string child;
            PSObject parentObj = ResolveNewDiskPath(out child);

            VirtualDisk disk = null;
            if (parentObj.BaseObject is DirectoryInfo)
            {
                string path = Path.Combine(((DirectoryInfo)parentObj.BaseObject).FullName, child);
                using (VirtualDisk realDisk = VirtualDisk.CreateDisk(type, variant, path, size, null, null)) { }
                disk = new OnDemandVirtualDisk(path, FileAccess.ReadWrite);
            }
            else if (parentObj.BaseObject is DiscDirectoryInfo)
            {
                DiscDirectoryInfo ddi = (DiscDirectoryInfo)parentObj.BaseObject;
                string path = Path.Combine(ddi.FullName, child);
                using (VirtualDisk realDisk = VirtualDisk.CreateDisk(ddi.FileSystem, type, variant, path, size, null, null)) { }
                disk = new OnDemandVirtualDisk(ddi.FileSystem, path, FileAccess.ReadWrite);
            }
            else
            {
                WriteError(new ErrorRecord(
                    new DirectoryNotFoundException("Cannot create a virtual disk in that location"),
                    "BadDiskLocation",
                    ErrorCategory.InvalidArgument,
                    null));
                return;
            }

            WriteObject(disk, false);
        }

        private void CreateDiffDisk()
        {
            string child;
            PSObject parentObj = ResolveNewDiskPath(out child);

            PSObject baseDiskObj = SessionState.InvokeProvider.Item.Get(new string[] { BaseDisk }, false, true)[0];

            VirtualDisk baseDisk = null;

            try
            {
                if (baseDiskObj.BaseObject is FileInfo)
                {
                    baseDisk = VirtualDisk.OpenDisk(((FileInfo)baseDiskObj.BaseObject).FullName, FileAccess.Read);
                }
                else if (baseDiskObj.BaseObject is DiscFileInfo)
                {
                    DiscFileInfo dfi = (DiscFileInfo)baseDiskObj.BaseObject;
                    baseDisk = VirtualDisk.OpenDisk(dfi.FileSystem, dfi.FullName, FileAccess.Read);
                }
                else
                {
                    WriteError(new ErrorRecord(
                        new FileNotFoundException("The file specified by the BaseDisk parameter doesn't exist"),
                        "BadBaseDiskLocation",
                        ErrorCategory.InvalidArgument,
                        null));
                    return;
                }

                VirtualDisk newDisk = null;
                if (parentObj.BaseObject is DirectoryInfo)
                {
                    string path = Path.Combine(((DirectoryInfo)parentObj.BaseObject).FullName, child);
                    using (baseDisk.CreateDifferencingDisk(path)) { }
                    newDisk = new OnDemandVirtualDisk(path, FileAccess.ReadWrite);
                }
                else if (parentObj.BaseObject is DiscDirectoryInfo)
                {
                    DiscDirectoryInfo ddi = (DiscDirectoryInfo)parentObj.BaseObject;
                    string path = Path.Combine(ddi.FullName, child);
                    using (baseDisk.CreateDifferencingDisk(ddi.FileSystem, path)) { }
                    newDisk = new OnDemandVirtualDisk(ddi.FileSystem, path, FileAccess.ReadWrite);
                }
                else
                {
                    WriteError(new ErrorRecord(
                        new DirectoryNotFoundException("Cannot create a virtual disk in that location"),
                        "BadDiskLocation",
                        ErrorCategory.InvalidArgument,
                        null));
                    return;
                }

                WriteObject(newDisk, false);
            }
            finally
            {
                if (baseDisk != null)
                {
                    baseDisk.Dispose();
                }
            }
        }

        private PSObject ResolveNewDiskPath(out string child)
        {
            PSObject parentObj;

            child = SessionState.Path.ParseChildName(LiteralPath);
            string parent = SessionState.Path.ParseParent(LiteralPath, null);
            PathInfo parentPath = this.SessionState.Path.GetResolvedPSPathFromPSPath(parent)[0];

            parentObj = SessionState.InvokeProvider.Item.Get(new string[] { parentPath.Path }, false, true)[0];

            // If we got a Volume, then we need to send a special marker to the provider indicating that we
            // actually wanted the root directory on the volume, not the volume itself.
            if (parentObj.BaseObject is VolumeInfo)
            {
                parentObj = SessionState.InvokeProvider.Item.Get(new string[] { Path.Combine(parentPath.Path, @"$Root") }, false, true)[0];
            }

            return parentObj;
        }

    }
}
