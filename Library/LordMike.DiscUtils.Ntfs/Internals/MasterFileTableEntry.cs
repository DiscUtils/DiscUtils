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

namespace DiscUtils.Ntfs.Internals
{
    /// <summary>
    /// An entry within the Master File Table.
    /// </summary>
    public sealed class MasterFileTableEntry
    {
        private readonly INtfsContext _context;
        private readonly FileRecord _fileRecord;

        internal MasterFileTableEntry(INtfsContext context, FileRecord fileRecord)
        {
            _context = context;
            _fileRecord = fileRecord;
        }

        /// <summary>
        /// Gets the attributes contained in this entry.
        /// </summary>
        public ICollection<GenericAttribute> Attributes
        {
            get
            {
                List<GenericAttribute> result = new List<GenericAttribute>();
                foreach (AttributeRecord attr in _fileRecord.Attributes)
                {
                    result.Add(GenericAttribute.FromAttributeRecord(_context, attr));
                }

                return result;
            }
        }

        /// <summary>
        /// Gets the identity of the base entry for files split over multiple entries.
        /// </summary>
        /// <remarks>
        /// All entries that form part of the same file have the same value for
        /// this property.
        /// </remarks>
        public MasterFileTableReference BaseRecordReference
        {
            get { return new MasterFileTableReference(_fileRecord.BaseFile); }
        }

        /// <summary>
        /// Gets the flags indicating the nature of the entry.
        /// </summary>
        public MasterFileTableEntryFlags Flags
        {
            get { return (MasterFileTableEntryFlags)_fileRecord.Flags; }
        }

        /// <summary>
        /// Gets the number of hard links referencing this file.
        /// </summary>
        public int HardLinkCount
        {
            get { return _fileRecord.HardLinkCount; }
        }

        /// <summary>
        /// Gets the index of this entry in the Master File Table.
        /// </summary>
        public long Index
        {
            get { return _fileRecord.LoadedIndex; }
        }

        /// <summary>
        /// Gets the change identifier that is updated each time the file is modified by Windows, relates to the NTFS log file.
        /// </summary>
        /// <remarks>
        /// The NTFS log file provides journalling, preventing meta-data corruption in the event of a system crash.
        /// </remarks>
        public long LogFileSequenceNumber
        {
            get { return (long)_fileRecord.LogFileSequenceNumber; }
        }

        /// <summary>
        /// Gets the next attribute identity that will be allocated.
        /// </summary>
        public int NextAttributeId
        {
            get { return _fileRecord.NextAttributeId; }
        }

        /// <summary>
        /// Gets the index of this entry in the Master File Table (as stored in the entry itself).
        /// </summary>
        /// <remarks>
        /// Note - older versions of Windows did not store this value, so it may be Zero.
        /// </remarks>
        public long SelfIndex
        {
            get { return _fileRecord.MasterFileTableIndex; }
        }

        /// <summary>
        /// Gets the revision number of the entry.
        /// </summary>
        /// <remarks>
        /// Each time an entry is allocated or de-allocated, this number is incremented by one.
        /// </remarks>
        public int SequenceNumber
        {
            get { return _fileRecord.SequenceNumber; }
        }
    }
}