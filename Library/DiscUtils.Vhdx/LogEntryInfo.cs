//
// Copyright (c) 2008-2013, Kenneth Bell
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

namespace DiscUtils.Vhdx
{
    /// <summary>
    /// Provides information about a entry in the VHDX log.
    /// </summary>
    public sealed class LogEntryInfo
    {
        private readonly LogEntry _entry;

        internal LogEntryInfo(LogEntry entry)
        {
            _entry = entry;
        }

        /// <summary>
        /// Gets the VHDX file size (in bytes) that is at least as large as the size of the VHDX file at the time the log entry was written.
        /// </summary>
        /// <remarks>When shrinking a VHDX file this field is used to indicate the new (smaller) size.</remarks>
        public long FlushedFileOffset
        {
            get { return (long)_entry.FlushedFileOffset; }
        }

        /// <summary>
        /// Gets a value indicating whether this log entry doesn't contain any data (or zero) descriptors.
        /// </summary>
        public bool IsEmpty
        {
            get { return _entry.IsEmpty; }
        }

        /// <summary>
        /// Gets the file size (in bytes) that all allocated file structures fit into, at the time the log entry was written.
        /// </summary>
        public long LastFileOffset
        {
            get { return (long)_entry.LastFileOffset; }
        }

        /// <summary>
        /// Gets the file extents that would be modified by replaying this log entry.
        /// </summary>
        public IEnumerable<Range<long, long>> ModifiedExtents
        {
            get
            {
                foreach (Range<ulong, ulong> range in _entry.ModifiedExtents)
                {
                    yield return new Range<long, long>((long)range.Offset, (long)range.Count);
                }
            }
        }

        /// <summary>
        /// Gets the sequence number of this log entry.
        /// </summary>
        /// <remarks>Consecutively numbered log entries form a sequence.</remarks>
        public long SequenceNumber
        {
            get { return (long)_entry.SequenceNumber; }
        }

        /// <summary>
        /// Gets the oldest logged activity that has not been persisted to disk.
        /// </summary>
        /// <remarks>The tail indicates how far back in the log replay must start in order
        /// to fully recreate the state of the VHDX file's metadata.</remarks>
        public int Tail
        {
            get { return (int)_entry.Tail; }
        }
    }
}