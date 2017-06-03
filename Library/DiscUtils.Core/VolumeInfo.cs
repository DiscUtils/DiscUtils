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

#if !NETCORE
using System;
#endif
using DiscUtils.Streams;

namespace DiscUtils
{
    /// <summary>
    /// Base class that holds information about a disk volume.
    /// </summary>
    public abstract class VolumeInfo
#if !NETCORE
        : MarshalByRefObject
#endif
    {
        internal VolumeInfo() {}

        /// <summary>
        /// Gets the one-byte BIOS type for this volume, which indicates the content.
        /// </summary>
        public abstract byte BiosType { get; }

        /// <summary>
        /// Gets the size of the volume, in bytes.
        /// </summary>
        public abstract long Length { get; }

        /// <summary>
        /// Gets the stable volume identity.
        /// </summary>
        /// <remarks>The stability of the identity depends the disk structure.
        /// In some cases the identity may include a simple index, when no other information
        /// is available.  Best practice is to add disks to the Volume Manager in a stable 
        /// order, if the stability of this identity is paramount.</remarks>
        public abstract string Identity { get; }

        /// <summary>
        /// Gets the disk geometry of the underlying storage medium, if any (may be null).
        /// </summary>
        public abstract Geometry PhysicalGeometry { get; }

        /// <summary>
        /// Gets the disk geometry of the underlying storage medium (as used in BIOS calls), may be null.
        /// </summary>
        public abstract Geometry BiosGeometry { get; }

        /// <summary>
        /// Gets the offset of this volume in the underlying storage medium, if any (may be Zero).
        /// </summary>
        public abstract long PhysicalStartSector { get; }

        /// <summary>
        /// Opens the volume, providing access to it's contents.
        /// </summary>
        /// <returns>Stream that can access the volume's contents.</returns>
        public abstract SparseStream Open();
    }
}