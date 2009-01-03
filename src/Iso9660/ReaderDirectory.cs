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
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscUtils.Iso9660
{
    internal class ReaderDirectory
    {
        private CDReader _reader;
        private PathTableRecord _ptr;
        private Encoding _enc;

        private List<DirectoryRecord> _records;
        private ReaderDirectory _parent;

        public ReaderDirectory(CDReader reader, PathTableRecord ptr, Encoding enc, ReaderDirectory parent)
        {
            _reader = reader;
            _ptr = ptr;
            _enc = enc;
            _parent = parent;

            byte[] buffer = new byte[2048];
            Stream extent = reader.GetDirectoryExtentStream(ptr.LocationOfExtent);

            _records = new List<DirectoryRecord>();

            uint totalLength = uint.MaxValue; // Will correct later...
            uint totalRead = 0;
            while (totalRead < totalLength)
            {
                uint bytesRead = (uint)Utilities.ReadFully(extent, buffer, 0, buffer.Length);
                if (bytesRead != Math.Min(buffer.Length, totalLength - totalRead))
                {
                    throw new IOException("Failed to read whole directory");
                }
                totalRead += (uint)bytesRead;

                uint pos = 0;
                while (pos < buffer.Length && buffer[pos] != 0)
                {
                    DirectoryRecord dr;
                    uint length = (uint)DirectoryRecord.ReadFrom(buffer, (int)pos, enc, out dr);

                    // If this is the 'self' entry, then use it to limit the amount of data read.
                    if (dr.FileIdentifier == "\0")
                    {
                        totalLength = Math.Min(totalLength, dr.DataLength);
                    }

                    _records.Add(dr);

                    pos += length;
                }
            }
        }

        public ReaderDirectory Parent
        {
            get
            {
                if (_parent == null)
                {
                    _parent = new ReaderDirectory(_reader, _reader.LookupPathTable((ushort)(_ptr.ParentDirectoryNumber - 1)), _enc, null);
                }

                return _parent;
            }
        }

        public string FullName
        {
            get
            {
                if (_ptr.DirectoryIdentifier == "\0")
                {
                    return @"\";
                }
                else
                {
                    return Parent.FullName + _ptr.DirectoryIdentifier + @"\";
                }
            }
        }

        public bool TryGetFile(string name, out DirectoryRecord result)
        {
            bool anyVerMatch = (name.IndexOf(';') < 0);
            string normName = IsoUtilities.NormalizeFileName(name).ToUpper(CultureInfo.InvariantCulture);
            if (anyVerMatch)
            {
                normName = normName.Substring(0, normName.LastIndexOf(';') + 1);
            }

            foreach (DirectoryRecord r in _records)
            {
                if ((r.Flags & FileFlags.Directory) == 0)
                {
                    string toComp = IsoUtilities.NormalizeFileName(r.FileIdentifier).ToUpper(CultureInfo.InvariantCulture);
                    if (!anyVerMatch && toComp == normName)
                    {
                        result = r;
                        return true;
                    }
                    else if (anyVerMatch && toComp.StartsWith(normName, StringComparison.Ordinal))
                    {
                        result = r;
                        return true;
                    }
                }
            }

            result = new DirectoryRecord();
            return false;
        }

        internal List<DirectoryRecord> GetRecords()
        {
            return _records;
        }

        internal List<string> SearchFiles(string pattern, bool subFolders)
        {
            string fullPattern = pattern;
            if (!pattern.Contains(";"))
            {
                fullPattern += ";*";
            }

            Regex re = Utilities.ConvertWildcardsToRegEx(fullPattern);

            List<string> results = new List<string>();
            DoSearch(results, subFolders, null, re);
            return results;
        }

        internal List<string> SearchDirectories(string pattern, bool subFolders)
        {
            Regex re = Utilities.ConvertWildcardsToRegEx(pattern);

            List<string> results = new List<string>();
            DoSearch(results, subFolders, re, null);
            return results;
        }

        internal List<string> SearchFileInfos(string pattern, bool subFolders)
        {
            Regex dirPattern = Utilities.ConvertWildcardsToRegEx(pattern);
            Regex filePattern = dirPattern;
            if (!pattern.Contains(";"))
            {
                filePattern = Utilities.ConvertWildcardsToRegEx(pattern + ";*");
            }

            List<string> results = new List<string>();
            DoSearch(results, subFolders, dirPattern, filePattern);
            return results;
        }

        private void DoSearch(List<string> results, bool subFolders, Regex dirPattern, Regex filePattern)
        {
            foreach (DirectoryRecord r in _records)
            {
                if ((r.Flags & FileFlags.Directory) != 0)
                {
                    if (!IsoUtilities.IsSpecialDirectory(r))
                    {
                        if (dirPattern != null && dirPattern.IsMatch(IsoUtilities.NormalizeDirectoryNameForSearch(r.FileIdentifier)))
                        {
                            results.Add(Utilities.CombinePaths(FullName, r.FileIdentifier) + @"\");
                        }

                        if (subFolders)
                        {
                            ReaderDirectory subFolder = _reader.GetDirectory(Utilities.CombinePaths(FullName, r.FileIdentifier));
                            subFolder.DoSearch(results, true, dirPattern, filePattern);
                        }
                    }
                }
                else if (filePattern != null && filePattern.IsMatch(IsoUtilities.NormalizeFileName(r.FileIdentifier)))
                {
                    results.Add(Utilities.CombinePaths(FullName, r.FileIdentifier));
                }
            }
        }
    }
}
