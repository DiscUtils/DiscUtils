//
// Copyright (c) 2008-2010, Kenneth Bell
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
using System.IO;
using System.Text;
using DiscUtils.Vfs;

namespace DiscUtils.Iso9660
{
    internal class VfsCDReader : VfsReadOnlyFileSystem<DirectoryRecord, File, ReaderDirectory, IsoContext>
    {
        private Stream _data;
        private bool _hideVersions;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="data">The stream to read the ISO image from.</param>
        /// <param name="joliet">Whether to read Joliet extensions.</param>
        /// <param name="hideVersions">Hides version numbers (e.g. ";1") from the end of files</param>
        public VfsCDReader(Stream data, bool joliet, bool hideVersions)
            : base(new DiscFileSystemOptions())
        {
            _data = data;
            _hideVersions = hideVersions;

            long vdpos = 0x8000; // Skip lead-in

            byte[] buffer = new byte[IsoUtilities.SectorSize];

            long pvdPos = 0;
            long svdPos = 0;

            BaseVolumeDescriptor bvd;
            do
            {
                data.Position = vdpos;
                int numRead = data.Read(buffer, 0, IsoUtilities.SectorSize);
                if (numRead != IsoUtilities.SectorSize)
                {
                    break;
                }


                bvd = new BaseVolumeDescriptor(buffer, 0);
                switch (bvd.VolumeDescriptorType)
                {
                    case VolumeDescriptorType.Boot:
                        break;
                    case VolumeDescriptorType.Primary: //Primary Vol Descriptor
                        pvdPos = vdpos;
                        break;
                    case VolumeDescriptorType.Supplementary: //Supplementary Vol Descriptor
                        svdPos = vdpos;
                        break;
                    case VolumeDescriptorType.Partition: //Volume Partition Descriptor
                        break;
                    case VolumeDescriptorType.SetTerminator: //Volume Descriptor Set Terminator
                        break;
                }

                vdpos += IsoUtilities.SectorSize;
            } while (bvd.VolumeDescriptorType != VolumeDescriptorType.SetTerminator);


            CommonVolumeDescriptor volDesc;
            if (joliet && svdPos != 0)
            {
                data.Position = svdPos;
                data.Read(buffer, 0, IsoUtilities.SectorSize);
                volDesc = new SupplementaryVolumeDescriptor(buffer, 0);
            }
            else
            {
                data.Position = pvdPos;
                data.Read(buffer, 0, IsoUtilities.SectorSize);
                volDesc = new PrimaryVolumeDescriptor(buffer, 0);
            }

            Context = new IsoContext { VolumeDescriptor = volDesc, DataStream = _data };
            RootDirectory = new ReaderDirectory(Context, volDesc.RootDirectory);
        }

        /// <summary>
        /// Provides the friendly name for the CD filesystem.
        /// </summary>
        public override string FriendlyName
        {
            get { return "ISO 9660 (CD-ROM)"; }
        }

        /// <summary>
        /// Gets the Volume Identifier.
        /// </summary>
        public override string VolumeLabel
        {
            get { return Context.VolumeDescriptor.VolumeIdentifier; }
        }

        protected override File ConvertDirEntryToFile(DirectoryRecord dirEntry)
        {
            if (dirEntry.IsDirectory)
            {
                return new ReaderDirectory(Context, dirEntry);
            }
            else
            {
                return new File(Context, dirEntry);
            }
        }

        protected override string FormatFileName(string name)
        {
            if (_hideVersions)
            {
                int pos = name.LastIndexOf(';');
                if (pos > 0)
                {
                    return name.Substring(0, pos);
                }
            }

            return name;
        }

    }
}
