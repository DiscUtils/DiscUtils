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

namespace DiscUtils.Iso9660
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using DiscUtils.Vfs;

    internal sealed class ReaderDirEntry : VfsDirEntry
    {
        private IsoContext _context;
        private DirectoryRecord _record;
        private SuspRecords _suspRecords;
        private string _fileName;
        private DateTime _lastAccessTimeUtc;
        private DateTime _lastWriteTimeUtc;
        private DateTime _creationTimeUtc;

        public ReaderDirEntry(IsoContext context, DirectoryRecord dirRecord)
        {
            _context = context;
            _record = dirRecord;
            _fileName = _record.FileIdentifier;

            if (context.SuspDetected && _record.SystemUseData != null)
            {
                _suspRecords = new SuspRecords(_context, _record.SystemUseData, 0);
            }

            if (!string.IsNullOrEmpty(_context.RockRidgeIdentifier))
            {
                // The full name is taken from this record, even if it's a child-link record
                List<SystemUseEntry> nameEntries = _suspRecords.GetEntries(_context.RockRidgeIdentifier, "NM");
                StringBuilder rrName = new StringBuilder();
                if (nameEntries != null && nameEntries.Count > 0)
                {
                    foreach (PosixNameSystemUseEntry nameEntry in nameEntries)
                    {
                        rrName.Append(nameEntry.NameData);
                    }

                    _fileName = rrName.ToString();
                }

                // If this is a Rock Ridge child link, replace the dir record with that from the 'self' record
                // in the child directory.
                ChildLinkSystemUseEntry clEntry = _suspRecords.GetEntry<ChildLinkSystemUseEntry>(_context.RockRidgeIdentifier, "CL");
                if (clEntry != null)
                {
                    _context.DataStream.Position = clEntry.ChildDirLocation * _context.VolumeDescriptor.LogicalBlockSize;
                    byte[] firstSector = Utilities.ReadFully(_context.DataStream, _context.VolumeDescriptor.LogicalBlockSize);

                    DirectoryRecord.ReadFrom(firstSector, 0, _context.VolumeDescriptor.CharacterEncoding, out _record);
                    if (_record.SystemUseData != null)
                    {
                        _suspRecords = new SuspRecords(_context, _record.SystemUseData, 0);
                    }
                }
            }

            _lastAccessTimeUtc = _record.RecordingDateAndTime;
            _lastWriteTimeUtc = _record.RecordingDateAndTime;
            _creationTimeUtc = _record.RecordingDateAndTime;

            if (!string.IsNullOrEmpty(_context.RockRidgeIdentifier))
            {
                FileTimeSystemUseEntry tfEntry = _suspRecords.GetEntry<FileTimeSystemUseEntry>(_context.RockRidgeIdentifier, "TF");

                if ((tfEntry.TimestampsPresent & FileTimeSystemUseEntry.Timestamps.Access) != 0)
                {
                    _lastAccessTimeUtc = tfEntry.AccessTime;
                }

                if ((tfEntry.TimestampsPresent & FileTimeSystemUseEntry.Timestamps.Modify) != 0)
                {
                    _lastWriteTimeUtc = tfEntry.ModifyTime;
                }

                if ((tfEntry.TimestampsPresent & FileTimeSystemUseEntry.Timestamps.Creation) != 0)
                {
                    _creationTimeUtc = tfEntry.CreationTime;
                }
            }
        }

        public SuspRecords SuspRecords
        {
            get { return _suspRecords; }
        }

        public DirectoryRecord Record
        {
            get { return _record; }
        }

        public override bool IsDirectory
        {
            get
            {
                return (_record.Flags & FileFlags.Directory) != 0;
            }
        }

        public override bool IsSymlink
        {
            get { return false; }
        }

        public override string FileName
        {
            get { return _fileName; }
        }

        public override bool HasVfsTimeInfo
        {
            get { return true; }
        }

        public override DateTime LastAccessTimeUtc
        {
            get { return _lastAccessTimeUtc; }
        }

        public override DateTime LastWriteTimeUtc
        {
            get { return _lastWriteTimeUtc; }
        }

        public override DateTime CreationTimeUtc
        {
            get { return _creationTimeUtc; }
        }

        public override bool HasVfsFileAttributes
        {
            get { return true; }
        }

        public override FileAttributes FileAttributes
        {
            get
            {
                FileAttributes attrs = (FileAttributes)0;

                if (!string.IsNullOrEmpty(_context.RockRidgeIdentifier))
                {
                    // If Rock Ridge PX info is present, derive the attributes from the RR info.
                    PosixFileInfoSystemUseEntry pfi = _suspRecords.GetEntry<PosixFileInfoSystemUseEntry>(_context.RockRidgeIdentifier, "PX");
                    if (pfi != null)
                    {
                        attrs = Utilities.FileAttributesFromUnixFileType((UnixFileType)((pfi.FileMode >> 12) & 0xF));
                    }

                    if (_fileName.StartsWith(".", StringComparison.Ordinal))
                    {
                        attrs |= FileAttributes.Hidden;
                    }
                }

                attrs |= FileAttributes.ReadOnly;

                if ((_record.Flags & FileFlags.Directory) != 0)
                {
                    attrs |= FileAttributes.Directory;
                }

                if ((_record.Flags & FileFlags.Hidden) != 0)
                {
                    attrs |= FileAttributes.Hidden;
                }

                return attrs;
            }
        }

        public override long UniqueCacheId
        {
            get { return (((long)_record.LocationOfExtent) << 32) | _record.DataLength; }
        }
    }
}
