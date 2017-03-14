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

namespace DiscUtils.Vmdk
{
    /// <summary>
    /// The parameters used to create a new VMDK file.
    /// </summary>
    public sealed class DiskParameters
    {
        /// <summary>
        /// Initializes a new instance of the DiskParameters class with default values.
        /// </summary>
        public DiskParameters() {}

        /// <summary>
        /// Initializes a new instance of the DiskParameters class with generic parameters.
        /// </summary>
        /// <param name="genericParameters">The generic parameters to copy.</param>
        public DiskParameters(VirtualDiskParameters genericParameters)
        {
            Capacity = genericParameters.Capacity;
            Geometry = genericParameters.Geometry;
            BiosGeometry = genericParameters.BiosGeometry;

            string stringCreateType;
            if (genericParameters.ExtendedParameters.TryGetValue(Disk.ExtendedParameterKeyCreateType,
                                     out stringCreateType))
            {
                CreateType = (DiskCreateType)Enum.Parse(typeof(DiskCreateType), stringCreateType);
            }
            else
            {
                CreateType = DiskCreateType.MonolithicSparse;
            }

            if (genericParameters.AdapterType == GenericDiskAdapterType.Ide)
            {
                AdapterType = DiskAdapterType.Ide;
            }
            else
            {
                string stringAdapterType;
                if (genericParameters.ExtendedParameters.TryGetValue(Disk.ExtendedParameterKeyAdapterType,
                                         out stringAdapterType))
                {
                    AdapterType = (DiskAdapterType)Enum.Parse(typeof(DiskAdapterType), stringAdapterType);

                    // Don't refining sub-type of SCSI actually select IDE
                    if (AdapterType == DiskAdapterType.Ide)
                    {
                        AdapterType = DiskAdapterType.LsiLogicScsi;
                    }
                }
                else
                {
                    AdapterType = DiskAdapterType.LsiLogicScsi;
                }
            }
        }

        /// <summary>
        /// Gets or sets the type of emulated disk adapter.
        /// </summary>
        public DiskAdapterType AdapterType { get; set; }

        /// <summary>
        /// Gets or sets the BIOS Geometry of the virtual disk.
        /// </summary>
        public Geometry BiosGeometry { get; set; }

        /// <summary>
        /// Gets or sets the capacity of the virtual disk.
        /// </summary>
        public long Capacity { get; set; }

        /// <summary>
        /// Gets or sets the type of VMDK file to create.
        /// </summary>
        public DiskCreateType CreateType { get; set; }

        /// <summary>
        /// Gets or sets the Physical Geometry of the virtual disk.
        /// </summary>
        public Geometry Geometry { get; set; }
    }
}