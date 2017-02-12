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
using System.Collections.Generic;
using System.Text;
using DiscUtils.Compression;

namespace DiscUtils.Ewf
{
    /// <summary>
    /// Represents the generic information that preceeds a section.
    /// Used to identify the section that follows.
    /// </summary>
    public class SectionDescriptor
    {
        /// <summary>
        /// The number of bytes expectde in the section descriptor.
        /// </summary>
        public const int SECTION_DESCRIPTOR_SIZE = 76;

        /// <summary>
        /// The kind of Section that is about to follow.
        /// </summary>
        public SECTION_TYPE SectionType { get; set; }

        /// <summary>
        /// The offset, from the beginning of the EWF, of the next section.
        /// </summary>
        public int NextSectionOffset { get; set; }

        /// <summary>
        /// The number of bytes that make up the section data that follows.
        /// </summary>
        public int SectionSize { get; set; }

        //public uint Checksum { get; set; }

        /// <summary>
        /// Creates an object to hold the SectionDescriptor data.
        /// </summary>
        /// <param name="bytes">The bytes from which to make the SectionDescriptor object.</param>
        /// <param name="fileOffset">The offset within the EWF file where this section starts.</param>
        public SectionDescriptor(byte[] bytes, long fileOffset)
        {
            if (bytes.Length != SECTION_DESCRIPTOR_SIZE)
                throw new ArgumentException("number of bytes in section descriptor must be " + SECTION_DESCRIPTOR_SIZE, "byte");

            string sectionType = Encoding.ASCII.GetString(bytes, 0, 16).Trim(new char[] { (char)0x00 }); ;
            if (Enum.IsDefined(typeof(SECTION_TYPE), sectionType))
                SectionType = (SECTION_TYPE)Enum.Parse(typeof(SECTION_TYPE), sectionType, true);
            else
                throw new ArgumentException("unknown section type: " + sectionType);

            NextSectionOffset = (int)BitConverter.ToInt64(bytes, 16);
            if (NextSectionOffset < fileOffset)
                throw new ArgumentException("next section cannot be before current section");

            SectionSize = (int)BitConverter.ToInt64(bytes, 24);
            if (SectionType != SECTION_TYPE.next && SectionType != SECTION_TYPE.done && SectionSize < 1)
                throw new ArgumentException("section size cannot be less than 1");

            Adler32 checksum = new Adler32();
            checksum.Process(bytes, 0, 72);
            uint adler32 = (uint)checksum.Value;
            if (adler32 != BitConverter.ToUInt32(bytes, 72))
                throw new ArgumentException("bad Adler32 checksum");
        }
    }    
}
