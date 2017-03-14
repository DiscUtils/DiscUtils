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

namespace DiscUtils.Vmdk
{
    /// <summary>
    /// Enumeration of VMDK disk types.
    /// </summary>
    public enum DiskCreateType
    {
        /// <summary>
        /// None - do not use.
        /// </summary>
        None = 0,

        /// <summary>
        /// VMware Workstation single-file dynamic disk.
        /// </summary>
        MonolithicSparse = 1,

        /// <summary>
        /// ESX Dynamic Disk.
        /// </summary>
        VmfsSparse = 2,

        /// <summary>
        /// VMware Workstation single-extent pre-allocated disk.
        /// </summary>
        MonolithicFlat = 3,

        /// <summary>
        /// ESX pre-allocated disk.
        /// </summary>
        Vmfs = 4,

        /// <summary>
        /// VMware Workstation multi-extent dynamic disk.
        /// </summary>
        TwoGbMaxExtentSparse = 5,

        /// <summary>
        /// VMware Workstation multi-extent pre-allocated disk.
        /// </summary>
        TwoGbMaxExtentFlat = 6,

        /// <summary>
        /// Full device disk.
        /// </summary>
        FullDevice = 7,

        /// <summary>
        /// ESX raw disk.
        /// </summary>
        VmfsRaw = 8,

        /// <summary>
        /// Partition disk.
        /// </summary>
        PartitionedDevice = 9,

        /// <summary>
        /// ESX RDM disk.
        /// </summary>
        VmfsRawDeviceMap = 10,

        /// <summary>
        /// ESX Passthrough RDM disk.
        /// </summary>
        VmfsPassthroughRawDeviceMap = 11,

        /// <summary>
        /// A streaming-optimized disk.
        /// </summary>
        StreamOptimized = 12
    }
}