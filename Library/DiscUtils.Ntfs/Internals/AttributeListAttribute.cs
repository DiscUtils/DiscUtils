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

using System.Collections.Generic;
using DiscUtils.Streams;

namespace DiscUtils.Ntfs.Internals
{
    /// <summary>
    /// List of attributes for files that are split over multiple Master File Table entries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Files with lots of attribute data (for example that have become very fragmented) contain
    /// this attribute in their 'base' Master File Table entry.  This attribute acts as an index,
    /// indicating for each attribute in the file, which Master File Table entry contains the
    /// attribute.
    /// </para>
    /// </remarks>
    public sealed class AttributeListAttribute : GenericAttribute
    {
        private readonly AttributeList _list;

        internal AttributeListAttribute(INtfsContext context, AttributeRecord record)
            : base(context, record)
        {
            byte[] content = StreamUtilities.ReadAll(Content);
            _list = new AttributeList();
            _list.ReadFrom(content, 0);
        }

        /// <summary>
        /// Gets the entries in this attribute list.
        /// </summary>
        public ICollection<AttributeListEntry> Entries
        {
            get
            {
                List<AttributeListEntry> entries = new List<AttributeListEntry>();
                foreach (AttributeListRecord record in _list)
                {
                    entries.Add(new AttributeListEntry(record));
                }

                return entries;
            }
        }
    }
}