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

namespace DiscUtils
{
    /// <summary>
    /// Common parameters for virtual disks.
    /// </summary>
    /// <remarks>Not all attributes make sense for all kinds of disks, so some
    /// may be null.  Modifying instances of this class does not modify the
    /// disk itself.</remarks>
    public sealed class VirtualDiskParameters
    {
        /// <summary>
        /// Gets or sets the type of disk adapter.
        /// </summary>
        public GenericDiskAdapterType AdapterType { get; set; }

        /// <summary>
        /// Gets or sets the logical (aka BIOS) geometry of the disk.
        /// </summary>
        public Geometry BiosGeometry { get; set; }

        /// <summary>
        /// Gets or sets the disk capacity.
        /// </summary>
        public long Capacity { get; set; }

        /// <summary>
        /// Gets or sets the type of disk (optical, hard disk, etc).
        /// </summary>
        public VirtualDiskClass DiskType { get; set; }

        /// <summary>
        /// Gets a dictionary of extended parameters, that varies by disk type.
        /// </summary>
        public Dictionary<string, string> ExtendedParameters { get; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets the physical (aka IDE) geometry of the disk.
        /// </summary>
        public Geometry Geometry { get; set; }
    }
}