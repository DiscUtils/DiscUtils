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
    /// <remarks>
    /// <para>
    /// Each Master File Table entry (MFT Entry) has one of these attributes for each
    /// hard link.  Files with a long name and a short name will have at least two of
    /// these attributes.</para>
    /// <para>
    /// The details in this attribute may be inconsistent with similar information in
    /// the StandardInformationAttribute for a file.  The StandardInformation is
    /// definitive, this attribute holds a 'cache' of the information.
    /// </para>
    /// </remarks>
    public sealed class FileNameAttribute : GenericAttribute
    {
        private readonly FileNameRecord _fnr;

        internal FileNameAttribute(INtfsContext context, AttributeRecord record)
            : base(context, record)
        {
            byte[] content = StreamUtilities.ReadAll(Content);
            _fnr = new FileNameRecord();
            _fnr.ReadFrom(content, 0);
        }

        /// <summary>
        /// Gets the amount of disk space allocated for the file.
        /// </summary>
        public long AllocatedSize
        {
            get { return (long)_fnr.AllocatedSize; }
        }

        /// <summary>
        /// Gets the creation time of the file.
        /// </summary>
        public DateTime CreationTime
        {
            get { return _fnr.CreationTime; }
        }

        /// <summary>
        /// Gets the extended attributes size, or a reparse tag, depending on the nature of the file.
        /// </summary>
        public long ExtendedAttributesSizeOrReparsePointTag
        {
            get { return _fnr.EASizeOrReparsePointTag; }
        }

        /// <summary>
        /// Gets the attributes of the file, as stored by NTFS.
        /// </summary>
        public NtfsFileAttributes FileAttributes
        {
            get { return (NtfsFileAttributes)_fnr.Flags; }
        }

        /// <summary>
        /// Gets the name of the file within the parent directory.
        /// </summary>
        public string FileName
        {
            get { return _fnr.FileName; }
        }

        /// <summary>
        /// Gets the namespace of the FileName property.
        /// </summary>
        public NtfsNamespace FileNameNamespace
        {
            get { return (NtfsNamespace)_fnr.FileNameNamespace; }
        }

        /// <summary>
        /// Gets the last access time of the file.
        /// </summary>
        public DateTime LastAccessTime
        {
            get { return _fnr.LastAccessTime; }
        }

        /// <summary>
        /// Gets the last time the Master File Table entry for the file was changed.
        /// </summary>
        public DateTime MasterFileTableChangedTime
        {
            get { return _fnr.MftChangedTime; }
        }

        /// <summary>
        /// Gets the modification time of the file.
        /// </summary>
        public DateTime ModificationTime
        {
            get { return _fnr.ModificationTime; }
        }

        /// <summary>
        /// Gets the reference to the parent directory.
        /// </summary>
        /// <remarks>
        /// This attribute stores the name of a file within a directory, this field
        /// provides the link back to the directory.
        /// </remarks>
        public MasterFileTableReference ParentDirectory
        {
            get { return new MasterFileTableReference(_fnr.ParentDirectory); }
        }

        /// <summary>
        /// Gets the amount of data stored in the file.
        /// </summary>
        public long RealSize
        {
            get { return (long)_fnr.RealSize; }
        }
    }
}