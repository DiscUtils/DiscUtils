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

namespace DiscUtils
{
    /// <summary>
    /// Information about a type of virtual disk.
    /// </summary>
    public sealed class VirtualDiskTypeInfo
    {
        /// <summary>
        /// Gets or sets the algorithm for determining the geometry for a given disk capacity.
        /// </summary>
        public GeometryCalculation CalcGeometry { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this disk type can represent hard disks.
        /// </summary>
        public bool CanBeHardDisk { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this disk type requires a specific geometry for any given disk capacity.
        /// </summary>
        public bool DeterministicGeometry { get; set; }

        /// <summary>
        /// Gets or sets the name of the virtual disk type.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this disk type persists the BIOS geometry.
        /// </summary>
        public bool PreservesBiosGeometry { get; set; }

        /// <summary>
        /// Gets or sets the variant of the virtual disk type.
        /// </summary>
        public string Variant { get; set; }
    }
}