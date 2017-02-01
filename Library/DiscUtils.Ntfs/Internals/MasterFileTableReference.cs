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
    /// A reference to a Master File Table entry.
    /// </summary>
    public struct MasterFileTableReference
    {
        private FileRecordReference _ref;

        internal MasterFileTableReference(FileRecordReference recordRef)
        {
            _ref = recordRef;
        }

        /// <summary>
        /// Gets the index of the referred entry in the Master File Table.
        /// </summary>
        public long RecordIndex
        {
            get { return _ref.MftIndex; }
        }

        /// <summary>
        /// Gets the revision number of the entry.
        /// </summary>
        /// <remarks>
        /// This value prevents accidental reference to an entry - it will get out
        /// of sync with the actual entry if the entry is re-allocated or de-allocated.
        /// </remarks>
        public int RecordSequenceNumber
        {
            get { return _ref.SequenceNumber; }
        }

        /// <summary>
        /// Compares to instances for equality.
        /// </summary>
        /// <param name="a">The first instance to compare.</param>
        /// <param name="b">The second instance to compare.</param>
        /// <returns><code>true</code> if the instances are equivalent, else <code>false</code>.</returns>
        public static bool operator ==(MasterFileTableReference a, MasterFileTableReference b)
        {
            return a._ref == b._ref;
        }

        /// <summary>
        /// Compares to instances for equality.
        /// </summary>
        /// <param name="a">The first instance to compare.</param>
        /// <param name="b">The second instance to compare.</param>
        /// <returns><code>true</code> if the instances are not equivalent, else <code>false</code>.</returns>
        public static bool operator !=(MasterFileTableReference a, MasterFileTableReference b)
        {
            return a._ref != b._ref;
        }

        /// <summary>
        /// Compares another object for equality.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns><code>true</code> if the other object is equivalent, else <code>false</code>.</returns>
        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is MasterFileTableReference))
            {
                return false;
            }

            return _ref == ((MasterFileTableReference)obj)._ref;
        }

        /// <summary>
        /// Gets a hash code for this instance.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            return _ref.GetHashCode();
        }
    }
}