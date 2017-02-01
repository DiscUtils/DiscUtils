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

namespace DiscUtils.Sdi
{
    /// <summary>
    /// Information about a blob (or section) within an SDI file.
    /// </summary>
    public class Section
    {
        private readonly SectionRecord _record;

        internal Section(SectionRecord record, int index)
        {
            _record = record;
            Index = index;
        }

        /// <summary>
        /// Gets the zero-based index of this section.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Gets the length of the section.
        /// </summary>
        public long Length
        {
            get { return _record.Size; }
        }

        /// <summary>
        /// Gets the MBR partition type of the partition, for "PART" sections.
        /// </summary>
        public byte PartitionType
        {
            get { return (byte)_record.PartitionType; }
        }

        /// <summary>
        /// Gets the type of this section.
        /// </summary>
        /// <remarks>Sample types are "PART" (disk partition), "WIM" (Windows Imaging Format).</remarks>
        public string SectionType
        {
            get { return _record.SectionType; }
        }
    }
}