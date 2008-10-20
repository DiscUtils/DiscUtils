//
// Copyright (c) 2008, Kenneth Bell
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
using System.Text.RegularExpressions;

namespace DiscUtils.Iso9660
{
    internal class ReaderDirectoryInfo : DiscDirectoryInfo
    {
        private CDReader reader;
        private Encoding enc;
        private ReaderDirectoryInfo parent;

        private DirectoryRecord record;
        private List<DirectoryRecord> records;

        public ReaderDirectoryInfo(CDReader reader, ReaderDirectoryInfo parent, DirectoryRecord record, Encoding enc)
        {
            this.reader = reader;
            this.parent = parent;
            this.record = record;
            this.enc = enc;

            byte[] buffer = new byte[2048];
            Stream extent = reader.GetExtentStream(record);

            uint totalLength = record.DataLength;
            uint totalRead = 0;
            while (totalRead < totalLength)
            {
                uint bytesRead = (uint)Utilities.ReadFully(extent, buffer, 0, buffer.Length);
                if (bytesRead != Math.Min(buffer.Length, totalLength - totalRead))
                {
                    throw new IOException("Failed to read whole directory");
                }
                totalRead += (uint)bytesRead;

                records = new List<DirectoryRecord>();
                uint pos = 0;
                while (pos < buffer.Length && buffer[pos] != 0)
                {
                    DirectoryRecord dr;
                    uint length = (uint)DirectoryRecord.ReadFrom(buffer, (int)pos, enc, out dr);

                    // If this is the 'self' entry, then use it to limit the amount of data written.
                    // The only time this matters is when this instance has been created from a PathTableRecord,
                    // in which case record.DataLength is uint.MaxValue!
                    if (dr.FileIdentifier == "\0")
                    {
                        totalLength = Math.Min(totalLength, dr.DataLength);
                    }

                    records.Add(dr);

                    pos += length;
                }
            }
        }

        public override string Name
        {
            get
            {
                return (record.FileIdentifier == "\0") ? @"\" : record.FileIdentifier;
            }
        }

        public override string FullName
        {
            get
            {
                if (record.FileIdentifier == "\0")
                {
                    return @"\";
                }
                else
                {
                    return Parent.FullName + record.FileIdentifier + @"\";
                }
            }
        }

        public override FileAttributes Attributes
        {
            get
            {
                FileAttributes attrs = FileAttributes.Directory | FileAttributes.ReadOnly;
                if ((record.Flags & FileFlags.Hidden) != 0) { attrs |= FileAttributes.Hidden; }
                return attrs;
            }
        }

        public override DiscDirectoryInfo Parent
        {
            get { return parent; }
        }

        public override bool Exists
        {
            // We don't support arbitrary DirectoryInfo's (yet) - they always represent a real dir.
            get { return true; }
        }

        public override DateTime CreationTime
        {
            get { return record.RecordingDateAndTime.ToLocalTime(); }
        }

        public override DateTime CreationTimeUtc
        {
            get { return record.RecordingDateAndTime; }
        }

        public override DateTime LastAccessTime
        {
            get { return CreationTime; }
        }

        public override DateTime LastAccessTimeUtc
        {
            get { return CreationTimeUtc; }
        }

        public override DateTime LastWriteTime
        {
            get { return CreationTime; }
        }

        public override DateTime LastWriteTimeUtc
        {
            get { return CreationTimeUtc; }
        }

        public override void Create()
        {
            throw new NotSupportedException();
        }

        public override DiscDirectoryInfo[] GetDirectories()
        {
            List<DiscDirectoryInfo> dirs = new List<DiscDirectoryInfo>();
            foreach (DirectoryRecord r in records)
            {
                if ((r.Flags & FileFlags.Directory) != 0 && r.FileIdentifier != "\0" && r.FileIdentifier != "\x01")
                {
                    dirs.Add(new ReaderDirectoryInfo(reader, this, r, enc));
                }
            }
            return dirs.ToArray();
        }

        public override DiscDirectoryInfo[] GetDirectories(string pattern)
        {
            return SearchDirectories(pattern, false).ToArray();
        }

        public override DiscDirectoryInfo[] GetDirectories(string pattern, SearchOption option)
        {
            return SearchDirectories(pattern, option == SearchOption.AllDirectories).ToArray();
        }

        public override DiscFileInfo[] GetFiles()
        {
            List<DiscFileInfo> files = new List<DiscFileInfo>();
            foreach (DirectoryRecord r in records)
            {
                if ((r.Flags & FileFlags.Directory) == 0)
                {
                    files.Add(new ReaderFileInfo(reader, this, r));
                }
            }
            return files.ToArray();
        }

        public override DiscFileInfo[] GetFiles(string pattern)
        {
            return SearchFiles(pattern, false).ToArray();
        }

        public override DiscFileInfo[] GetFiles(string pattern, SearchOption option)
        {
            return SearchFiles(pattern, option == SearchOption.AllDirectories).ToArray();
        }

        public override DiscFileSystemInfo[] GetFileSystemInfos()
        {
            List<DiscFileSystemInfo> results = new List<DiscFileSystemInfo>();
            foreach (DirectoryRecord r in records)
            {
                if ((r.Flags & FileFlags.Directory) == 0)
                {
                    results.Add(new ReaderFileInfo(reader, this, r));
                }
                else
                {
                    results.Add(new ReaderDirectoryInfo(reader, this, r, enc));
                }
            }
            return results.ToArray();
        }

        public override DiscFileSystemInfo[] GetFileSystemInfos(string pattern)
        {
            throw new NotImplementedException();
        }

        private List<DiscFileInfo> SearchFiles(string pattern, bool subFolders)
        {
            string fullPattern = pattern;
            if (!pattern.Contains(";"))
            {
                fullPattern += ";*";
            }

            Regex re = Utilities.ConvertWildcardsToRegEx(fullPattern);

            List<DiscFileInfo> results = new List<DiscFileInfo>();
            DoSearch(results, re, subFolders);
            return results;
        }

        private List<DiscDirectoryInfo> SearchDirectories(string pattern, bool subFolders)
        {
            Regex re = Utilities.ConvertWildcardsToRegEx(pattern);

            List<DiscDirectoryInfo> results = new List<DiscDirectoryInfo>();
            DoSearch(results, re, subFolders);
            return results;
        }

        private List<DiscFileSystemInfo> SearchAll(string pattern, bool subFolders)
        {
            List<DiscDirectoryInfo> dirs = SearchDirectories(pattern, subFolders);
            List<DiscFileInfo> files = SearchFiles(pattern, subFolders);

            List<DiscFileSystemInfo> results = new List<DiscFileSystemInfo>(dirs.Count + files.Count);

            foreach (DiscFileSystemInfo d in dirs)
            {
                results.Add(d);
            }

            foreach (DiscFileSystemInfo f in files)
            {
                results.Add(f);
            }

            return results;
        }

        private void DoSearch(List<DiscFileInfo> results, Regex regex, bool subFolders)
        {
            foreach (DirectoryRecord r in records)
            {
                if ((r.Flags & FileFlags.Directory) == 0)
                {
                    if (regex.IsMatch(IsoUtilities.NormalizeFileName(r.FileIdentifier)))
                    {
                        results.Add(new ReaderFileInfo(reader, this, r));
                    }
                }
                else if( subFolders && !IsoUtilities.IsSpecialDirectory(r))
                {
                    ReaderDirectoryInfo subFolder = new ReaderDirectoryInfo(reader, this, r, enc);
                    subFolder.DoSearch(results, regex, subFolders);
                }
            }
        }

        private void DoSearch(List<DiscDirectoryInfo> results, Regex regex, bool subFolders)
        {
            foreach (DirectoryRecord r in records)
            {
                if ((r.Flags & FileFlags.Directory) != 0)
                {
                    if (regex.IsMatch(r.FileIdentifier))
                    {
                        results.Add(new ReaderDirectoryInfo(reader, this, r, enc));
                    }

                    if (subFolders && !IsoUtilities.IsSpecialDirectory(r))
                    {
                        ReaderDirectoryInfo subFolder = new ReaderDirectoryInfo(reader, this, r, enc);
                        subFolder.DoSearch(results, regex, subFolders);
                    }
                }
            }
        }
    }
}
