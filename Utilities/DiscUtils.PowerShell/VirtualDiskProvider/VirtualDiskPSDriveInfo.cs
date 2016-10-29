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

using System.Collections.Generic;
using System.Management.Automation;
using DiscUtils.FileSystems;

namespace DiscUtils.PowerShell.VirtualDiskProvider
{
    public sealed class VirtualDiskPSDriveInfo : PSDriveInfo
    {
        private VirtualDisk _disk;
        private VolumeManager _volMgr;
        private Dictionary<string, DiscFileSystem> _fsCache;

        public VirtualDiskPSDriveInfo(PSDriveInfo toCopy, string root, VirtualDisk disk)
            : base(toCopy.Name, toCopy.Provider, root, toCopy.Description, toCopy.Credential)
        {
            _disk = disk;
            _volMgr = new VolumeManager(_disk);
            _fsCache = new Dictionary<string, DiscFileSystem>();
        }

        public VirtualDisk Disk
        {
            get { return _disk; }
        }

        public VolumeManager VolumeManager
        {
            get { return _volMgr; }
        }

        internal DiscFileSystem GetFileSystem(VolumeInfo volInfo)
        {
            SetupHelper.SetupFileSystems();

            DiscFileSystem result;
            if (!_fsCache.TryGetValue(volInfo.Identity, out result))
            {
                FileSystemInfo[] fsInfo = FileSystemManager.DetectFileSystems(volInfo);
                if (fsInfo != null && fsInfo.Length > 0)
                {
                    result = fsInfo[0].Open(volInfo);
                    _fsCache.Add(volInfo.Identity, result);
                }
            }

            return result;
        }

        internal void RescanVolumes()
        {
            VolumeManager newVolMgr = new VolumeManager(_disk);
            Dictionary<string, DiscFileSystem> newFsCache = new Dictionary<string, DiscFileSystem>();
            Dictionary<string, DiscFileSystem> deadFileSystems = new Dictionary<string, DiscFileSystem>(_fsCache);

            foreach (var volInfo in newVolMgr.GetLogicalVolumes())
            {
                if (_fsCache.ContainsKey(volInfo.Identity))
                {
                    newFsCache.Add(volInfo.Identity, _fsCache[volInfo.Identity]);
                    deadFileSystems.Remove(volInfo.Identity);
                }
            }

            foreach (var deadFs in deadFileSystems.Values)
            {
                deadFs.Dispose();
            }

            _volMgr = newVolMgr;
            _fsCache = newFsCache;
        }

        internal void UncacheFileSystem(string volId)
        {
            DiscFileSystem fs;
            if (_fsCache.TryGetValue(volId, out fs))
            {
                fs.Dispose();
                _fsCache.Remove(volId);
            }
        }
    }

}
