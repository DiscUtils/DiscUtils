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
using System.Collections.Generic;
using System.Globalization;
using DiscUtils.CoreCompat;
using DiscUtils.Internal;
using DiscUtils.Streams;

namespace DiscUtils
{
    /// <summary>
    /// Base class for all disk image builders.
    /// </summary>
    public abstract class DiskImageBuilder
    {
        private static Dictionary<string, VirtualDiskFactory> _typeMap;

        /// <summary>
        /// Gets or sets the geometry of this disk, as reported by the BIOS, will be implied from the content stream if not set.
        /// </summary>
        public Geometry BiosGeometry { get; set; }

        /// <summary>
        /// Gets or sets the content for this disk, implying the size of the disk.
        /// </summary>
        public SparseStream Content { get; set; }

        /// <summary>
        /// Gets or sets the adapter type for created virtual disk, for file formats that encode this information.
        /// </summary>
        public virtual GenericDiskAdapterType GenericAdapterType { get; set; }

        /// <summary>
        /// Gets or sets the geometry of this disk, will be implied from the content stream if not set.
        /// </summary>
        public Geometry Geometry { get; set; }

        /// <summary>
        /// Gets a value indicating whether this file format preserves BIOS geometry information.
        /// </summary>
        public virtual bool PreservesBiosGeometry
        {
            get { return false; }
        }

        private static Dictionary<string, VirtualDiskFactory> TypeMap
        {
            get
            {
                if (_typeMap == null)
                {
                    InitializeMaps();
                }

                return _typeMap;
            }
        }

        /// <summary>
        /// Gets an instance that constructs the specified type (and variant) of virtual disk image.
        /// </summary>
        /// <param name="type">The type of image to build (VHD, VMDK, etc).</param>
        /// <param name="variant">The variant type (differencing/dynamic, fixed/static, etc).</param>
        /// <returns>The builder instance.</returns>
        public static DiskImageBuilder GetBuilder(string type, string variant)
        {
            VirtualDiskFactory factory;
            if (!TypeMap.TryGetValue(type, out factory))
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Unknown disk type '{0}'", type), nameof(type));
            }

            return factory.GetImageBuilder(variant);
        }

        /// <summary>
        /// Initiates the construction of the disk image.
        /// </summary>
        /// <param name="baseName">The base name for the disk images.</param>
        /// <returns>A set of one or more logical files that constitute the
        /// disk image.  The first file is the 'primary' file that is normally attached to VMs.</returns>
        /// <remarks>The supplied <c>baseName</c> is the start of the file name, with no file
        /// extension.  The set of file specifications will indicate the actual name corresponding
        /// to each logical file that comprises the disk image.  For example, given a base name
        /// 'foo', the files 'foo.vmdk' and 'foo-flat.vmdk' could be returned.</remarks>
        public abstract DiskImageFileSpecification[] Build(string baseName);

        private static void InitializeMaps()
        {
            Dictionary<string, VirtualDiskFactory> typeMap = new Dictionary<string, VirtualDiskFactory>();

            foreach (Type type in ReflectionHelper.GetAssembly(typeof(VirtualDisk)).GetTypes())
            {
                VirtualDiskFactoryAttribute attr = (VirtualDiskFactoryAttribute)ReflectionHelper.GetCustomAttribute(type, typeof(VirtualDiskFactoryAttribute), false);
                if (attr != null)
                {
                    VirtualDiskFactory factory = (VirtualDiskFactory)Activator.CreateInstance(type);
                    typeMap.Add(attr.Type, factory);
                }
            }

            _typeMap = typeMap;
        }
    }
}