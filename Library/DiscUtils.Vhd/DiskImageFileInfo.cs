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
using System.Collections.Generic;
using System.IO;
using System.Text;
using DiscUtils.Streams;

namespace DiscUtils.Vhd
{
    /// <summary>
    /// Provides read access to detailed information about a VHD file.
    /// </summary>
    public class DiskImageFileInfo
    {
        private readonly Footer _footer;
        private readonly DynamicHeader _header;
        private readonly Stream _vhdStream;

        internal DiskImageFileInfo(Footer footer, DynamicHeader header, Stream vhdStream)
        {
            _footer = footer;
            _header = header;
            _vhdStream = vhdStream;
        }

        /// <summary>
        /// Gets the cookie indicating this is a VHD file (should be "conectix").
        /// </summary>
        public string Cookie
        {
            get { return _footer.Cookie; }
        }

        /// <summary>
        /// Gets the time the file was created (note: this is not the modification time).
        /// </summary>
        public DateTime CreationTimestamp
        {
            get { return _footer.Timestamp; }
        }

        /// <summary>
        /// Gets the application used to create the file.
        /// </summary>
        public string CreatorApp
        {
            get { return _footer.CreatorApp; }
        }

        /// <summary>
        /// Gets the host operating system of the application used to create the file.
        /// </summary>
        public string CreatorHostOS
        {
            get { return _footer.CreatorHostOS; }
        }

        /// <summary>
        /// Gets the version of the application used to create the file, packed as an integer.
        /// </summary>
        public int CreatorVersion
        {
            get { return (int)_footer.CreatorVersion; }
        }

        /// <summary>
        /// Gets the current size of the disk (in bytes).
        /// </summary>
        public long CurrentSize
        {
            get { return _footer.CurrentSize; }
        }

        /// <summary>
        /// Gets the type of the disk.
        /// </summary>
        public FileType DiskType
        {
            get { return _footer.DiskType; }
        }

        /// <summary>
        /// Gets the number of sparse blocks the file is divided into.
        /// </summary>
        public long DynamicBlockCount
        {
            get { return _header.MaxTableEntries; }
        }

        /// <summary>
        /// Gets the size of a sparse allocation block, in bytes.
        /// </summary>
        public long DynamicBlockSize
        {
            get { return _header.BlockSize; }
        }

        /// <summary>
        /// Gets the checksum value of the dynamic header structure.
        /// </summary>
        public int DynamicChecksum
        {
            get { return (int)_header.Checksum; }
        }

        /// <summary>
        /// Gets the cookie indicating a dynamic disk header (should be "cxsparse").
        /// </summary>
        public string DynamicCookie
        {
            get { return _header.Cookie; }
        }

        /// <summary>
        /// Gets the version of the dynamic header structure, packed as an integer.
        /// </summary>
        public int DynamicHeaderVersion
        {
            get { return (int)_header.HeaderVersion; }
        }

        /// <summary>
        /// Gets the stored paths to the parent file (for differencing disks).
        /// </summary>
        public IEnumerable<string> DynamicParentLocators
        {
            get
            {
                List<string> vals = new List<string>(8);
                foreach (ParentLocator pl in _header.ParentLocators)
                {
                    if (pl.PlatformCode == ParentLocator.PlatformCodeWindowsAbsoluteUnicode
                        || pl.PlatformCode == ParentLocator.PlatformCodeWindowsRelativeUnicode)
                    {
                        _vhdStream.Position = pl.PlatformDataOffset;
                        byte[] buffer = StreamUtilities.ReadExact(_vhdStream, pl.PlatformDataLength);
                        vals.Add(Encoding.Unicode.GetString(buffer));
                    }
                }

                return vals;
            }
        }

        /// <summary>
        /// Gets the modification timestamp of the parent file (for differencing disks).
        /// </summary>
        public DateTime DynamicParentTimestamp
        {
            get { return _header.ParentTimestamp; }
        }

        /// <summary>
        /// Gets the unicode name of the parent file (for differencing disks).
        /// </summary>
        public string DynamicParentUnicodeName
        {
            get { return _header.ParentUnicodeName; }
        }

        /// <summary>
        /// Gets the unique id of the parent file (for differencing disks).
        /// </summary>
        public Guid DynamicParentUniqueId
        {
            get { return _header.ParentUniqueId; }
        }

        /// <summary>
        /// Gets the Features bit field.
        /// </summary>
        public int Features
        {
            get { return (int)_footer.Features; }
        }

        /// <summary>
        /// Gets the file format version packed as an integer.
        /// </summary>
        public int FileFormatVersion
        {
            get { return (int)_footer.FileFormatVersion; }
        }

        /// <summary>
        /// Gets the checksum of the file's 'footer'.
        /// </summary>
        public int FooterChecksum
        {
            get { return (int)_footer.Checksum; }
        }

        /// <summary>
        /// Gets the geometry of the disk.
        /// </summary>
        public Geometry Geometry
        {
            get { return _footer.Geometry; }
        }

        /// <summary>
        /// Gets the original size of the disk (in bytes).
        /// </summary>
        public long OriginalSize
        {
            get { return _footer.OriginalSize; }
        }

        /// <summary>
        /// Gets a flag indicating if the disk has associated saved VM memory state.
        /// </summary>
        public byte SavedState
        {
            get { return _footer.SavedState; }
        }

        /// <summary>
        /// Gets the unique identity of this disk.
        /// </summary>
        public Guid UniqueId
        {
            get { return _footer.UniqueId; }
        }
    }
}