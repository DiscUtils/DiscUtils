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
using System.IO;

namespace DiscUtils.Ewf
{
    /// <summary>
    /// Helper class which exposes the most frequently accessed meta-data from the EWF file.
    /// </summary>
    public class EWFInfo
    {
        /// <summary>
        /// The path to the EWF file used to build the EWFInfo object.
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// Exposes the Volume section.
        /// </summary>
        public Section.Volume VolumeSection { get; private set; }

        /// <summary>
        /// Exposes the Header/Header2 section.
        /// </summary>
        public Section.Header2 HeaderSection { get; private set; }

        /// <summary>
        /// Creates a new EWFInfo helper class from filePath.
        /// </summary>
        /// <param name="filePath">The path of the EWF file.</param>
        public EWFInfo(string filePath)
        {
            FilePath = filePath;

            using (FileStream fs = File.OpenRead(filePath))
            {
                fs.Seek(0, SeekOrigin.Begin);

                byte[] buff = new byte[13];
                fs.Read(buff, 0, 13);
                EWFHeader ewfHeader = new EWFHeader(buff);

                while (fs.Position < fs.Length)
                {
                    buff = new byte[76];
                    fs.Read(buff, 0, 76);
                    SectionDescriptor sd = new SectionDescriptor(buff, fs.Position - 76);

                    switch (sd.SectionType)
                    {
                        case SECTION_TYPE.header:
                        case SECTION_TYPE.header2:
                            // Save the header
                            buff = new byte[sd.NextSectionOffset - fs.Position];
                            fs.Read(buff, 0, sd.NextSectionOffset - (int)fs.Position);
                            HeaderSection = new Section.Header2(buff);
                            break;

                        case SECTION_TYPE.volume:
                        case SECTION_TYPE.disk:
                        case SECTION_TYPE.data:
                            // Save the volume
                            buff = new byte[sd.NextSectionOffset - fs.Position];
                            fs.Read(buff, 0, sd.NextSectionOffset - (int)fs.Position);
                            VolumeSection = new Section.Volume(buff);
                            break;

                        case SECTION_TYPE.next:
                        case SECTION_TYPE.done:
                            fs.Seek(76, SeekOrigin.Current);
                            break;

                        default:
                            fs.Seek(sd.NextSectionOffset - (int)fs.Position, SeekOrigin.Current);
                            break;
                    }

                    if (HeaderSection != null && VolumeSection != null)
                        break;
                }
            }

            if (HeaderSection == null || VolumeSection == null)
                throw new Exception("file missing header or volume section");
        }
    }
}
