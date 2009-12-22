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
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Provider;
using System.Net;
using System.Text;
using DiscUtils.Ntfs;

namespace DiscUtils.PowerShell
{
    [CmdletProvider("DiscUtils", ProviderCapabilities.Credentials)]
    public sealed class Provider : NavigationCmdletProvider
    {
        #region Drive manipulation
        protected override PSDriveInfo NewDrive(PSDriveInfo drive)
        {
            NewDriveParameters dynParams = DynamicParameters as NewDriveParameters;

            if (drive == null)
            {
                WriteError(new ErrorRecord(
                    new ArgumentNullException("drive"),
                    "NullDrive",
                    ErrorCategory.InvalidArgument,
                    null));
                return null;
            }

            if (string.IsNullOrEmpty(drive.Root))
            {
                WriteError(new ErrorRecord(
                    new ArgumentException("drive"),
                    "NoRoot",
                    ErrorCategory.InvalidArgument,
                    drive));
                return null;
            }

            string[] mountPaths = NormalizePath(drive.Root).Split('!');
            if (mountPaths.Length < 1 || mountPaths.Length > 2)
            {
                WriteError(new ErrorRecord(
                    new ArgumentException("drive"),
                    "InvalidRoot",
                    ErrorCategory.InvalidArgument,
                    drive));
                //return null;
            }
            string diskPath = mountPaths[0];
            string relPath = mountPaths.Length > 1 ? mountPaths[1] : "";

            string user = null;
            string password = null;
            if (drive.Credential != null && drive.Credential.UserName != null)
            {
                NetworkCredential netCred = drive.Credential.GetNetworkCredential();
                user = netCred.UserName;
                password = netCred.Password;
            }

            try
            {
                FileAccess access = dynParams.ReadWrite.IsPresent ? FileAccess.ReadWrite : FileAccess.Read;
                VirtualDisk disk = VirtualDisk.OpenDisk(DenormalizePath(diskPath), access, user, password);
                return new VirtualDiskPSDriveInfo(drive, MakePath(diskPath + "!", relPath), disk);
            }
            catch (IOException ioe)
            {
                WriteError(new ErrorRecord(
                    ioe,
                    "DiskAccess",
                    ErrorCategory.ResourceUnavailable,
                    drive.Root));
                return null;
            }
        }

        protected override object NewDriveDynamicParameters()
        {
            return new NewDriveParameters();
        }

        protected override PSDriveInfo RemoveDrive(PSDriveInfo drive)
        {
            if (drive == null)
            {
                WriteError(new ErrorRecord(
                    new ArgumentNullException("drive"),
                    "NullDrive",
                    ErrorCategory.InvalidArgument,
                    null));
                return null;
            }

            VirtualDiskPSDriveInfo vdDrive = drive as VirtualDiskPSDriveInfo;
            if (vdDrive == null)
            {
                return null;
            }

            vdDrive.Disk.Dispose();

            return vdDrive;
        }
        #endregion

        #region Item methods
        protected override void GetItem(string path)
        {
            Object obj = FindItemByPath(NormalizePath(path));
            if (obj != null)
            {
                WriteItemObject(obj, path.Trim('\\'), true);
            }
        }

        protected override void SetItem(string path, object value)
        {
            throw new NotImplementedException();
        }

        protected override bool ItemExists(string path)
        {
            bool result = FindItemByPath(NormalizePath(path)) != null;
            return result;
        }

        protected override bool IsValidPath(string path)
        {
            return !string.IsNullOrEmpty(path);
        }
        #endregion

        #region Container methods
        protected override void GetChildItems(string path, bool recurse)
        {
            GetChildren(NormalizePath(path), recurse, false);
        }

        protected override void GetChildNames(string path, ReturnContainers returnContainers)
        {
            // TODO: returnContainers
            GetChildren(NormalizePath(path), false, true);
        }

        protected override bool HasChildItems(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return true;
            }

            return true;
        }
        #endregion

        #region Navigation methods
        protected override bool IsItemContainer(string path)
        {
            object obj = FindItemByPath(NormalizePath(path));

            bool result = false;
            if (obj is VirtualDisk)
            {
                result = true;
            }
            else if (obj is LogicalVolumeInfo)
            {
                result = true;
            }
            else if (obj is DiscDirectoryInfo)
            {
                result = true;
            }

            return result;
        }

        protected override string MakePath(string parent, string child)
        {
            return NormalizePath(base.MakePath(DenormalizePath(parent), DenormalizePath(child)));
        }

        #endregion

        #region Type Extensions
        public static string Mode(PSObject instance)
        {
            if (instance == null)
            {
                return "";
            }

            DiscFileSystemInfo fsi = instance.BaseObject as DiscFileSystemInfo;
            if (fsi == null)
            {
                return "";
            }

            StringBuilder result = new StringBuilder(5);
            result.Append(((fsi.Attributes & FileAttributes.Directory) != 0) ? "d" : "-");
            result.Append(((fsi.Attributes & FileAttributes.Archive) != 0) ? "a" : "-");
            result.Append(((fsi.Attributes & FileAttributes.ReadOnly) != 0) ? "r" : "-");
            result.Append(((fsi.Attributes & FileAttributes.Hidden) != 0) ? "h" : "-");
            result.Append(((fsi.Attributes & FileAttributes.System) != 0) ? "s" : "-");
            return result.ToString();
        }
        #endregion

        private VirtualDiskPSDriveInfo DriveInfo
        {
            get { return PSDriveInfo as VirtualDiskPSDriveInfo; }
        }

        private VirtualDisk Disk
        {
            get
            {
                VirtualDiskPSDriveInfo driveInfo = DriveInfo;
                return (driveInfo != null) ? driveInfo.Disk : null;
            }
        }

        private object FindItemByPath(string path)
        {
            string diskPath;
            string relPath;

            int mountSepIdx = path.IndexOf('!');
            if (mountSepIdx < 0)
            {
                diskPath = path;
                relPath = "";
            }
            else
            {
                diskPath = path.Substring(0, mountSepIdx);
                relPath = path.Substring(mountSepIdx + 1);
            }

            VirtualDisk disk = Disk;
            if( disk == null )
            {
                OnDemandVirtualDisk odvd = new OnDemandVirtualDisk(DenormalizePath(diskPath), FileAccess.Read);
                if (odvd.IsValid)
                {
                    disk = odvd;
                    ShowSlowDiskWarning();
                }
                else
                {
                    return null;
                }
            }

            List<string> pathElems = new List<string>(relPath.Split(new string[] { @"\" }, StringSplitOptions.RemoveEmptyEntries));

            if (pathElems.Count == 0)
            {
                return disk;
            }


            VolumeInfo volInfo = null;
            VolumeManager volMgr = DriveInfo != null ? DriveInfo.VolumeManager : new VolumeManager(disk);
            LogicalVolumeInfo[] volumes = volMgr.GetLogicalVolumes();
            string volNumStr = pathElems[0].StartsWith("Volume", StringComparison.OrdinalIgnoreCase) ? pathElems[0].Substring(6) : null;
            int volNum;
            if (int.TryParse(volNumStr, out volNum) || volNum < 0 || volNum >= volumes.Length)
            {
                volInfo = volumes[volNum];
            }
            else
            {
                volInfo = volMgr.GetVolume(DenormalizePath(pathElems[0]));
            }
            pathElems.RemoveAt(0);
            if (volInfo == null || pathElems.Count == 0)
            {
                return volInfo;
            }


            bool disposeFs;
            DiscFileSystem fs = GetFileSystem(volInfo, out disposeFs);
            try
            {
                if (fs == null)
                {
                    return null;
                }

                // Special marker in the path - disambiguates the root folder from the volume
                // containing it.  By this point it's done it's job (we didn't return volInfo),
                // so we just remove it.
                if (pathElems[0] == "$Root")
                {
                    pathElems.RemoveAt(0);
                }

                string fsPath = string.Join(@"\", pathElems.ToArray());
                if (fs.DirectoryExists(fsPath))
                {
                    return fs.GetDirectoryInfo(fsPath);
                }
                else if (fs.FileExists(fsPath))
                {
                    return fs.GetFileInfo(fsPath);
                }
            }
            finally
            {
                if (disposeFs && fs != null)
                {
                    fs.Dispose();
                }
            }

            return null;
        }

        private void ShowSlowDiskWarning()
        {
            const string varName = "DiscUtils_HideSlowDiskWarning";
            PSVariable psVar = this.SessionState.PSVariable.Get(varName);
            if (psVar != null && psVar.Value != null)
            {
                bool warningHidden;

                string valStr = psVar.Value.ToString();
                if (bool.TryParse(valStr, out warningHidden) && warningHidden)
                {
                    return;
                }
            }

            WriteWarning("Slow disk access.  Mount the disk using New-PSDrive to improve performance.  This message will not show again.");
            this.SessionState.PSVariable.Set(varName, true.ToString());
        }

        private DiscFileSystem GetFileSystem(VolumeInfo volInfo, out bool dispose)
        {
            if (DriveInfo != null)
            {
                dispose = false;
                return DriveInfo.GetFileSystem(volInfo);
            }
            else
            {
                // TODO: proper file system detection
                if (volInfo.BiosType == 7)
                {
                    dispose = true;
                    return new NtfsFileSystem(volInfo.Open());
                }
            }

            dispose = false;
            return null;
        }

        /// <summary>
        /// Replace all ':' characters with '#'.
        /// </summary>
        /// <param name="path">The path to normalize</param>
        /// <returns>The normalized path</returns>
        /// <remarks>
        /// PowerShell has a bug that prevents tab-completion if the paths contain ':'
        /// characters, so in the external path for this provider we encode ':' as '#'.
        /// </remarks>
        private static string NormalizePath(string path)
        {
            return path.Replace(':', '#');
        }

        /// <summary>
        /// Replace all '#' characters with ':'.
        /// </summary>
        /// <param name="path">The path to normalize</param>
        /// <returns>The normalized path</returns>
        /// <remarks>
        /// PowerShell has a bug that prevents tab-completion if the paths contain ':'
        /// characters, so in the external path for this provider we encode ':' as '#'.
        /// </remarks>
        private static string DenormalizePath(string path)
        {
            return path.Replace('#', ':');
        }

        private void GetChildren(string path, bool recurse, bool namesOnly)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            object obj = FindItemByPath(path);

            if (obj is VirtualDisk)
            {
                VirtualDisk vd = (VirtualDisk)obj;
                EnumerateDisk(vd, path, recurse, namesOnly);
            }
            else if (obj is LogicalVolumeInfo)
            {
                LogicalVolumeInfo lvi = (LogicalVolumeInfo)obj;

                bool dispose;
                DiscFileSystem fs = GetFileSystem(lvi, out dispose);
                try
                {
                    if (fs != null)
                    {
                        EnumerateDirectory(fs.Root, path, recurse, namesOnly);
                    }
                }
                finally
                {
                    if (dispose && fs != null)
                    {
                        fs.Dispose();
                    }
                }
            }
            else if (obj is DiscDirectoryInfo)
            {
                DiscDirectoryInfo ddi = (DiscDirectoryInfo)obj;
                EnumerateDirectory(ddi, path, recurse, namesOnly);
            }
            else
            {
                WriteError(new ErrorRecord(
                    new InvalidOperationException("Unrecognized object type: " + (obj != null ? obj.GetType() : null)),
                    "UnknownObjectType",
                    ErrorCategory.ParserError,
                    obj));
            }
        }

        private void EnumerateDisk(VirtualDisk vd, string path, bool recurse, bool namesOnly)
        {
            if (!path.TrimEnd('\\').EndsWith("!"))
            {
                path += "!";
            }

            VolumeManager volMgr = DriveInfo != null ? DriveInfo.VolumeManager : new VolumeManager(vd);
            LogicalVolumeInfo[] volumes = volMgr.GetLogicalVolumes();
            for (int i = 0; i < volumes.Length; ++i)
            {
                string name = "Volume" + i;
                string volPath = MakePath(path, name);// new PathInfo(PathInfo.Parse(path, true).MountParts, "" + i).ToString();
                WriteItemObject(namesOnly ? name : (object)volumes[i], volPath, true);
                if (recurse)
                {
                    GetChildren(volPath, recurse, namesOnly);
                }
            }
        }

        private void EnumerateDirectory(DiscDirectoryInfo parent, string basePath, bool recurse, bool namesOnly)
        {
            foreach (var dir in parent.GetDirectories())
            {
                WriteItemObject(namesOnly ? dir.Name : (object)dir, MakePath(basePath, dir.Name), true);
                if (recurse)
                {
                    EnumerateDirectory(dir, MakePath(basePath, dir.Name), recurse, namesOnly);
                }
            }
            foreach (var file in parent.GetFiles())
            {
                WriteItemObject(namesOnly ? file.Name : (object)file, MakePath(basePath, file.Name), false);
            }
        }
    }



    public class OnDemandVirtualDisk : VirtualDisk
    {
        private DiscFileSystem _fileSystem;
        private string _path;
        private FileAccess _access;

        public OnDemandVirtualDisk(string path, FileAccess access)
        {
            _path = path;
            _access = access;
        }

        public OnDemandVirtualDisk(DiscFileSystem fileSystem, string path, FileAccess access)
        {
            _fileSystem = fileSystem;
            _path = path;
            _access = access;
        }

        public bool IsValid
        {
            get
            {
                try
                {
                    using (VirtualDisk disk = OpenDisk())
                    {
                        return disk != null;
                    }
                }
                catch (IOException)
                {
                    return false;
                }
            }
        }

        public override Geometry Geometry
        {
            get
            {
                using (VirtualDisk disk = OpenDisk())
                {
                    return disk.Geometry;
                }
            }
        }

        public override long Capacity
        {
            get
            {
                using (VirtualDisk disk = OpenDisk())
                {
                    return disk.Capacity;
                }
            }
        }

        public override SparseStream Content
        {
            get { return new StreamWrapper(_fileSystem, _path, _access); }
        }

        public override IEnumerable<VirtualDiskLayer> Layers
        {
            get { throw new NotSupportedException("Access to virtual disk layers is not implemented for on-demand disks"); }
        }

        private VirtualDisk OpenDisk()
        {
            return VirtualDisk.OpenDisk(_fileSystem, _path, FileAccess.Read);
        }


        private class StreamWrapper : SparseStream
        {
            private DiscFileSystem _fileSystem;
            private string _path;
            private FileAccess _access;
            private long _position;

            public StreamWrapper(DiscFileSystem fileSystem, string path, FileAccess access)
            {
                _fileSystem = fileSystem;
                _path = path;
                _access = access;
            }

            public override IEnumerable<StreamExtent> Extents
            {
                get
                {
                    using (VirtualDisk disk = OpenDisk())
                    {
                        return new List<StreamExtent>(disk.Content.Extents);
                    }
                }
            }

            public override bool CanRead
            {
                get
                {
                    using (VirtualDisk disk = OpenDisk())
                    {
                        return disk.Content.CanRead;
                    }
                }
            }

            public override bool CanSeek
            {
                get
                {
                    using (VirtualDisk disk = OpenDisk())
                    {
                        return disk.Content.CanSeek;
                    }
                }
            }

            public override bool CanWrite
            {
                get
                {
                    using (VirtualDisk disk = OpenDisk())
                    {
                        return disk.Content.CanWrite;
                    }
                }
            }

            public override void Flush()
            {
            }

            public override long Length
            {
                get
                {
                    using (VirtualDisk disk = OpenDisk())
                    {
                        return disk.Content.Length;
                    }
                }
            }

            public override long Position
            {
                get
                {
                    return _position;
                }
                set
                {
                    _position = value;
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                using (VirtualDisk disk = OpenDisk())
                {
                    disk.Content.Position = _position;
                    return disk.Content.Read(buffer, offset, count);
                }
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                long effectiveOffset = offset;
                if (origin == SeekOrigin.Current)
                {
                    effectiveOffset += _position;
                }
                else if (origin == SeekOrigin.End)
                {
                    effectiveOffset += Length;
                }

                if (effectiveOffset < 0)
                {
                    throw new IOException("Attempt to move before beginning of disk");
                }
                else
                {
                    _position = effectiveOffset;
                    return _position;
                }
            }

            public override void SetLength(long value)
            {
                using (VirtualDisk disk = OpenDisk())
                {
                    disk.Content.SetLength(value);
                }
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                using (VirtualDisk disk = OpenDisk())
                {
                    disk.Content.Position = _position;
                    disk.Content.Write(buffer, offset, count);
                }
            }

            private VirtualDisk OpenDisk()
            {
                return VirtualDisk.OpenDisk(_fileSystem, _path, FileAccess.Read);
            }
        }
    }
}
