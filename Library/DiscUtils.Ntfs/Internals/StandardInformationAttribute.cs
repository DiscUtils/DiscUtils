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

using System;
using DiscUtils.Streams;

namespace DiscUtils.Ntfs.Internals
{
    /// <summary>
    /// Representation of an NTFS File Name attribute.
    /// </summary>
    /// <para>
    /// The details in this attribute may be inconsistent with similar information in
    /// the FileNameAttribute(s) for a file.  This attribute is definitive, the
    /// FileNameAttribute attribute holds a 'cache' of some of the information.
    /// </para>
    public sealed class StandardInformationAttribute : GenericAttribute
    {
        private readonly StandardInformation _si;

        internal StandardInformationAttribute(INtfsContext context, AttributeRecord record)
            : base(context, record)
        {
            byte[] content = StreamUtilities.ReadAll(Content);
            _si = new StandardInformation();
            _si.ReadFrom(content, 0);
        }

        /// <summary>
        /// Gets the Unknown.
        /// </summary>
        public long ClassId
        {
            get { return _si.ClassId; }
        }

        /// <summary>
        /// Gets the creation time of the file.
        /// </summary>
        public DateTime CreationTime
        {
            get { return _si.CreationTime; }
        }

        /// <summary>
        /// Gets the attributes of the file, as stored by NTFS.
        /// </summary>
        public NtfsFileAttributes FileAttributes
        {
            get { return (NtfsFileAttributes)_si.FileAttributes; }
        }

        /// <summary>
        /// Gets the last update sequence number of the file (relates to the user-readable journal).
        /// </summary>
        public long JournalSequenceNumber
        {
            get { return (long)_si.UpdateSequenceNumber; }
        }

        /// <summary>
        /// Gets the last access time of the file.
        /// </summary>
        public DateTime LastAccessTime
        {
            get { return _si.LastAccessTime; }
        }

        /// <summary>
        /// Gets the last time the Master File Table entry for the file was changed.
        /// </summary>
        public DateTime MasterFileTableChangedTime
        {
            get { return _si.MftChangedTime; }
        }

        /// <summary>
        /// Gets the maximum number of file versions (normally 0).
        /// </summary>
        public long MaxVersions
        {
            get { return _si.MaxVersions; }
        }

        /// <summary>
        /// Gets the modification time of the file.
        /// </summary>
        public DateTime ModificationTime
        {
            get { return _si.ModificationTime; }
        }

        /// <summary>
        /// Gets the owner identity, for the purposes of quota allocation.
        /// </summary>
        public long OwnerId
        {
            get { return _si.OwnerId; }
        }

        /// <summary>
        /// Gets the amount charged to the owners quota for this file.
        /// </summary>
        public long QuotaCharged
        {
            get { return (long)_si.QuotaCharged; }
        }

        /// <summary>
        /// Gets the identifier of the Security Descriptor for this file.
        /// </summary>
        /// <remarks>
        /// Security Descriptors are stored in the \$Secure meta-data file.
        /// </remarks>
        public long SecurityId
        {
            get { return _si.SecurityId; }
        }

        /// <summary>
        /// Gets the version number of the file (normally 0).
        /// </summary>
        public long Version
        {
            get { return _si.Version; }
        }
    }
}