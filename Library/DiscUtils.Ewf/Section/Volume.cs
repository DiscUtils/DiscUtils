//
// Copyright (c) 2013, Adam Bridge
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
using DiscUtils.Compression;

namespace DiscUtils.Ewf.Section
{
    /// <summary>
    /// The Volume section of the EWF file.
    /// Contains a variety of meta-data about the represented volume.
    /// </summary>
    public class Volume
    {
        /// <summary>
        /// Value indicating what kind of media is represented by the EWF.
        /// </summary>
        public MEDIA_TYPE MediaType { get; set; }

        /// <summary>
        /// Value indicating the total number of chunks stored across the EWF files.
        /// </summary>
        public int ChunkCount { get; set; }

        /// <summary>
        /// Value indicating how many sectors from the source media are stored in each chunk.
        /// </summary>
        public int SectorsPerChunk { get; set; }
        
        /// <summary>
        /// Value indicating the number of bytes per sector on the acquired media.
        /// </summary>
        public int BytesPerSector { get; set; }

        /// <summary>
        /// Value indicating how many sectors were contained within the source media (LBA).
        /// </summary>
        public int SectorCount { get; set; }
        
        /// <summary>
        /// The C of CHS. Often not used or unhelpful.
        /// </summary>
        public int Cylinders { get; set; }

        /// <summary>
        /// The H of CHS. Often not used or unhelpful.
        /// </summary>
        public int Heads { get; set; }

        /// <summary>
        /// The S of CHS. Often not used or unhelpful.
        /// Not to be confused with SectorCount.
        /// </summary>
        public int Sectors { get; set; }

        /// <summary>
        /// Value indicating various information about the source media.
        /// </summary>
        public MEDIA_FLAG MediaFlag { get; set; }

        /// <summary>
        /// ???
        /// </summary>
        public int PALMVolumeStart { get; set; }

        /// <summary>
        /// ???
        /// </summary>
        public int SMARTLogsStartSector { get; set; }

        /// <summary>
        /// Value indicating the level of compression used: EWF-wide.
        /// </summary>
        public COMPRESSION Compression { get; set; }

        /// <summary>
        /// Value indicating how many bytes were ignored when an error occurred reading the source media.
        /// </summary>
        public int ErrorBlockSize { get; set; }

        /// <summary>
        /// GUID generated, probably for the set of segment files. Often unused.
        /// </summary>
        public Guid SetGUID { get; set; }

        /// <summary>
        /// An object to hold the Volume section data.
        /// </summary>
        /// <param name="bytes">The bytes from which to make the object.</param>
        public Volume(byte[] bytes)
        {
            MediaType = (MEDIA_TYPE)bytes[0];

            ChunkCount = BitConverter.ToInt32(bytes, 4);
            SectorsPerChunk = BitConverter.ToInt32(bytes, 8);
            BytesPerSector = BitConverter.ToInt32(bytes, 12);
            SectorCount = BitConverter.ToInt32(bytes, 16);

            Cylinders = BitConverter.ToInt32(bytes, 24);
            Heads = BitConverter.ToInt32(bytes, 28);
            Sectors = BitConverter.ToInt32(bytes, 32);

            MediaFlag = (MEDIA_FLAG)bytes[36];

            PALMVolumeStart = BitConverter.ToInt32(bytes, 40);
            SMARTLogsStartSector = BitConverter.ToInt32(bytes, 48);

            Compression = (COMPRESSION)bytes[52];

            ErrorBlockSize = BitConverter.ToInt32(bytes, 56);

            byte[] guidBytes = new byte[16];
            Array.Copy(bytes, 64, guidBytes, 0, 16);
            SetGUID = new Guid(guidBytes);


            Adler32 checksum = new Adler32();
            checksum.Process(bytes, 0, 1048);
            uint adler32 = (uint)checksum.Value;
            if (adler32 != BitConverter.ToUInt32(bytes, 1048))
                throw new ArgumentException("bad Adler32 checksum");
        }
    }
}
