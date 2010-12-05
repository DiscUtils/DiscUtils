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
        /// The index of the referred entry in the Master File Table.
        /// </summary>
        public long RecordIndex
        {
            get { return _ref.MftIndex; }
        }

        /// <summary>
        /// The revision number of the entry.
        /// </summary>
        /// <remarks>
        /// This value prevents accidental reference to an entry - it will get out
        /// of sync with the actual entry if the entry is re-allocated or de-allocated.
        /// </remarks>
        public int RecordSequenceNumber
        {
            get { return _ref.SequenceNumber; }
        }
    }
}
