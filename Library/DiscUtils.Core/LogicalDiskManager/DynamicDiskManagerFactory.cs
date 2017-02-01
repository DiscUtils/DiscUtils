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
using DiscUtils.Internal;

namespace DiscUtils.LogicalDiskManager
{
    [LogicalVolumeFactory]
    internal class DynamicDiskManagerFactory : LogicalVolumeFactory
    {
        public override bool HandlesPhysicalVolume(PhysicalVolumeInfo volume)
        {
            return DynamicDiskManager.HandlesPhysicalVolume(volume);
        }

        public override void MapDisks(IEnumerable<VirtualDisk> disks, Dictionary<string, LogicalVolumeInfo> result)
        {
            DynamicDiskManager mgr = new DynamicDiskManager();

            foreach (VirtualDisk disk in disks)
            {
                if (DynamicDiskManager.IsDynamicDisk(disk))
                {
                    mgr.Add(disk);
                }
            }

            foreach (LogicalVolumeInfo vol in mgr.GetLogicalVolumes())
            {
                result.Add(vol.Identity, vol);
            }
        }
    }
}