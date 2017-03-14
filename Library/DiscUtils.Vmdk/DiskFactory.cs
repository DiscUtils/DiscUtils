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
using System.Globalization;
using System.IO;
using DiscUtils.Internal;

namespace DiscUtils.Vmdk
{
    [VirtualDiskFactory("VMDK", ".vmdk")]
    internal sealed class DiskFactory : VirtualDiskFactory
    {
        public override string[] Variants
        {
            get { return new[] { "fixed", "dynamic", "vmfsfixed", "vmfsdynamic" }; }
        }

        public override VirtualDiskTypeInfo GetDiskTypeInformation(string variant)
        {
            return MakeDiskTypeInfo(VariantToCreateType(variant));
        }

        public override DiskImageBuilder GetImageBuilder(string variant)
        {
            DiskBuilder builder = new DiskBuilder();
            builder.DiskType = VariantToCreateType(variant);
            return builder;
        }

        public override VirtualDisk CreateDisk(FileLocator locator, string variant, string path,
                                               VirtualDiskParameters diskParameters)
        {
            DiskParameters vmdkParams = new DiskParameters(diskParameters);
            vmdkParams.CreateType = VariantToCreateType(variant);
            return Disk.Initialize(locator, path, vmdkParams);
        }

        public override VirtualDisk OpenDisk(string path, FileAccess access)
        {
            return new Disk(path, access);
        }

        public override VirtualDisk OpenDisk(FileLocator locator, string path, FileAccess access)
        {
            return new Disk(locator, path, access);
        }

        public override VirtualDiskLayer OpenDiskLayer(FileLocator locator, string path, FileAccess access)
        {
            return new DiskImageFile(locator, path, access);
        }

        internal static VirtualDiskTypeInfo MakeDiskTypeInfo(DiskCreateType createType)
        {
            return new VirtualDiskTypeInfo
            {
                Name = "VMDK",
                Variant = CreateTypeToVariant(createType),
                CanBeHardDisk = true,
                DeterministicGeometry = false,
                PreservesBiosGeometry = false,
                CalcGeometry = c => DiskImageFile.DefaultGeometry(c)
            };
        }

        private static DiskCreateType VariantToCreateType(string variant)
        {
            switch (variant)
            {
                case "fixed":
                    return DiskCreateType.MonolithicFlat;
                case "dynamic":
                    return DiskCreateType.MonolithicSparse;
                case "vmfsfixed":
                    return DiskCreateType.Vmfs;
                case "vmfsdynamic":
                    return DiskCreateType.VmfsSparse;
                default:
                    throw new ArgumentException(
                        string.Format(CultureInfo.InvariantCulture, "Unknown VMDK disk variant '{0}'", variant),
                        nameof(variant));
            }
        }

        private static string CreateTypeToVariant(DiskCreateType createType)
        {
            switch (createType)
            {
                case DiskCreateType.MonolithicFlat:
                case DiskCreateType.TwoGbMaxExtentFlat:
                    return "fixed";

                case DiskCreateType.MonolithicSparse:
                case DiskCreateType.TwoGbMaxExtentSparse:
                    return "dynamic";

                case DiskCreateType.Vmfs:
                    return "vmfsfixed";

                case DiskCreateType.VmfsSparse:
                    return "vmfsdynamic";

                default:
                    return "fixed";
            }
        }
    }
}