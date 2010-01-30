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

using System.Collections.Generic;
using System.Management.Automation;
using DiscUtils.Ntfs;

namespace DiscUtils.PowerShell.Provider
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
            DiscFileSystem result;
            if (!_fsCache.TryGetValue(volInfo.Identity, out result))
            {
                if (volInfo.BiosType == 7)
                {
                    result = new NtfsFileSystem(volInfo.Open());
                    _fsCache.Add(volInfo.Identity, result);
                }
            }

            return result;
        }
    }

}
