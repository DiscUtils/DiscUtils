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

namespace DiscUtils.Ntfs
{
    /// <summary>
    /// Fully-qualified reference to an attribute.
    /// </summary>
    internal class AttributeReference : IComparable<AttributeReference>, IEquatable<AttributeReference>
    {
        private FileRecordReference _fileReference;
        private ushort _attributeId;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="fileReference">The file containing the attribute</param>
        /// <param name="attributeId">The identity of the attribute within the file record</param>
        public AttributeReference(FileRecordReference fileReference, ushort attributeId)
        {
            _fileReference = fileReference;
            _attributeId = attributeId;
        }

        /// <summary>
        /// The file containing the attribute.
        /// </summary>
        public FileRecordReference File
        {
            get { return _fileReference; }
        }

        /// <summary>
        /// The identity of the attribute within the file record.
        /// </summary>
        public ushort AttributeId
        {
            get { return _attributeId; }
        }

        /// <summary>
        /// The reference as a string.
        /// </summary>
        /// <returns>String representing the attribute</returns>
        public override string ToString()
        {
            return _fileReference.ToString() + ".attr[" + _attributeId + "]";
        }

        #region IComparable<AttributeReference> Members

        /// <summary>
        /// Compares this attribute reference to another.
        /// </summary>
        /// <param name="other">The attribute reference to compare against</param>
        /// <returns>Zero if references are identical.</returns>
        public int CompareTo(AttributeReference other)
        {
            int refDiff = _fileReference.CompareTo(other._fileReference);
            if (refDiff != 0)
            {
                return refDiff;
            }

            return _attributeId.CompareTo(other._attributeId);
        }

        #endregion

        #region IEquatable<AttributeReference> Members

        /// <summary>
        /// Indicates if two references are equivalent.
        /// </summary>
        /// <param name="other">The attribute reference to compare</param>
        /// <returns><c>true</c> if the references are equivalent.</returns>
        public bool Equals(AttributeReference other)
        {
            return CompareTo(other) == 0;
        }

        #endregion

        /// <summary>
        /// Indicates if this reference is equivalent to another object.
        /// </summary>
        /// <param name="obj">The object to compare</param>
        /// <returns><c>true</c> if obj is an equivalent attribute reference</returns>
        public override bool Equals(object obj)
        {
            AttributeReference objAsAttrRef = obj as AttributeReference;
            if (objAsAttrRef == null)
            {
                return false;
            }
            return Equals(objAsAttrRef);
        }

        /// <summary>
        /// Gets the hash code for this reference.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            return _fileReference.GetHashCode() ^ _attributeId.GetHashCode();
        }
    }
}
