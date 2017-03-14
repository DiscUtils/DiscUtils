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

namespace DiscUtils.Ntfs.Internals
{
    /// <summary>
    /// Represents an entry in an AttributeList attribute.
    /// </summary>
    /// <remarks>Each instance of this class points to the actual Master File Table
    /// entry that contains the attribute.  It is used for files split over multiple
    /// Master File Table entries.</remarks>
    public sealed class AttributeListEntry
    {
        private readonly AttributeListRecord _record;

        internal AttributeListEntry(AttributeListRecord record)
        {
            _record = record;
        }

        /// <summary>
        /// Gets the identifier of the attribute.
        /// </summary>
        public int AttributeIdentifier
        {
            get { return _record.AttributeId; }
        }

        /// <summary>
        /// Gets the name of the attribute (if any).
        /// </summary>
        public string AttributeName
        {
            get { return _record.Name; }
        }

        /// <summary>
        /// Gets the type of the attribute.
        /// </summary>
        public AttributeType AttributeType
        {
            get { return _record.Type; }
        }

        /// <summary>
        /// Gets the first cluster represented in this attribute (normally 0).
        /// </summary>
        /// <remarks>
        /// <para>
        /// For very fragmented files, it can be necessary to split a single attribute
        /// over multiple Master File Table entries.  This is achieved with multiple attributes
        /// with the same name and type (one per Master File Table entry), with this field
        /// determining the logical order of the attributes.
        /// </para>
        /// <para>
        /// The number is the first 'virtual' cluster present (i.e. divide the file's content
        /// into 'cluster' sized chunks, this is the first of those clusters logically
        /// represented in the attribute).
        /// </para>
        /// </remarks>
        public long FirstFileCluster
        {
            get { return (long)_record.StartVcn; }
        }

        /// <summary>
        /// Gets the Master File Table entry that contains the attribute.
        /// </summary>
        public MasterFileTableReference MasterFileTableEntry
        {
            get { return new MasterFileTableReference(_record.BaseFileReference); }
        }
    }
}