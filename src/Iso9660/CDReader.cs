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

using System.IO;
using DiscUtils.Vfs;

namespace DiscUtils.Iso9660
{
    /// <summary>
    /// Class for reading existing ISO images.
    /// </summary>
    public class CDReader : VfsFileSystemFacade
    {

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="data">The stream to read the ISO image from.</param>
        /// <param name="joliet">Whether to read Joliet extensions.</param>
        public CDReader(Stream data, bool joliet)
            : base(new VfsCDReader(data, joliet, false))
        {
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="data">The stream to read the ISO image from.</param>
        /// <param name="joliet">Whether to read Joliet extensions.</param>
        /// <param name="hideVersions">Hides version numbers (e.g. ";1") from the end of files</param>
        public CDReader(Stream data, bool joliet, bool hideVersions)
            : base(new VfsCDReader(data, joliet, hideVersions))
        {
        }

        /// <summary>
        /// Detects if a stream contains a valid ISO file system.
        /// </summary>
        /// <param name="data">The stream to inspect</param>
        /// <returns><c>true</c> if the stream contains an ISO file system, else false.</returns>
        public static bool Detect(Stream data)
        {
            byte[] buffer = new byte[IsoUtilities.SectorSize];

            if (data.Length < 0x8000 + IsoUtilities.SectorSize)
            {
                return false;
            }

            data.Position = 0x8000;
            int numRead = Utilities.ReadFully(data, buffer, 0, IsoUtilities.SectorSize);
            if (numRead != IsoUtilities.SectorSize)
            {
                return false;
            }

            BaseVolumeDescriptor bvd = new BaseVolumeDescriptor(buffer, 0);
            return bvd.StandardIdentifier == "CD001";
        }
    }
}
