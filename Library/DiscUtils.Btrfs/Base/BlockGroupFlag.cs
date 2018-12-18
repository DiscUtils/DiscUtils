//
// Copyright (c) 2017, Bianco Veigel
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

namespace DiscUtils.Btrfs.Base
{
    [Flags]
    internal enum BlockGroupFlag : ulong
    {
        /// <summary>
        /// The type of storage this block group offers. SYSTEM chunks cannot be mixed, but DATA and METADATA chunks can be mixed. 
        /// </summary>
        Data = 0x01,
        /// <summary>
        /// The type of storage this block group offers. SYSTEM chunks cannot be mixed, but DATA and METADATA chunks can be mixed. 
        /// </summary>
        System = 0x2,
        /// <summary>
        /// The type of storage this block group offers. SYSTEM chunks cannot be mixed, but DATA and METADATA chunks can be mixed. 
        /// </summary>
        Metadata = 0x4,
        /// <summary>
        /// Striping 
        /// </summary>
        Raid0 = 0x8,
        /// <summary>
        /// Mirror on a separate device 
        /// </summary>
        Raid1 = 0x10,
        /// <summary>
        /// Mirror on a single device
        /// </summary>
        Dup = 0x20,
        /// <summary>
        /// Striping and mirroring 
        /// </summary>
        Raid10 = 0x40,
        /// <summary>
        /// Parity striping with single-disk fault tolerance 
        /// </summary>
        Raid5 = 0x80,
        /// <summary>
        /// Parity striping with double-disk fault tolerance 
        /// </summary>
        Raid6 = 0x100
    }
}
