//
// Copyright (c) 2008-2009, Kenneth Bell
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
using DiscUtils.Ntfs.Attributes;
using DirectoryIndexEntry = System.Collections.Generic.KeyValuePair<DiscUtils.Ntfs.FileNameRecord, DiscUtils.Ntfs.FileReference>;

namespace DiscUtils.Ntfs
{
    internal class Directory : File
    {
        private static IComparer<FileNameRecord> _fileNameComparer = new FileNameComparer();

        private Index<FileNameRecord, FileReference> _index;


        public Directory(NtfsFileSystem fileSystem, FileRecord baseRecord)
            : base(fileSystem, baseRecord)
        {
            _index = new Index<FileNameRecord, FileReference>(this, "$I30", _fileSystem.BiosParameterBlock, _fileNameComparer);
        }

        internal DirectoryEntry GetEntryByName(string name)
        {
            string searchName = name;

            int streamSepPos = name.IndexOf(':');
            if (streamSepPos >= 0)
            {
                searchName = name.Substring(0, streamSepPos);
            }

            DirectoryIndexEntry entry = _index.FindFirst(new FileNameQuery(searchName));
            if (entry.Key != null && entry.Value != null)
            {
                return new DirectoryEntry(entry.Value, entry.Key);
            }
            else
            {
                return null;
            }
        }

        public IEnumerable<DirectoryEntry> GetAllEntries()
        {
            List<DirectoryIndexEntry> entries = FilterEntries(_index.Entries);

            foreach (var entry in entries)
            {
                yield return new DirectoryEntry(entry.Value, entry.Key);
            }
        }

        public override void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "DIRECTORY (" + _baseRecord.ToString() + ")");
            writer.WriteLine(indent + "  File Number: " + _baseRecord.MasterFileTableIndex);

            foreach (FileAttributeRecord attrRec in _baseRecord.Attributes)
            {
                BaseAttribute.FromRecord(_fileSystem, attrRec).Dump(writer, indent + "  ");
            }
        }

        public override string ToString()
        {
            return base.ToString() + @"\";
        }

        private List<DirectoryIndexEntry> FilterEntries(IEnumerable<DirectoryIndexEntry> entriesIter)
        {
            List<DirectoryIndexEntry> entries = new List<DirectoryIndexEntry>(entriesIter);

            // Weed out short-name versions of files where there's a long name
            // and any hidden / system / metadata files.
            Dictionary<FileReference, DirectoryIndexEntry> byRefIndex = new Dictionary<FileReference, DirectoryIndexEntry>();
            int i = 0;
            while (i < entries.Count)
            {
                DirectoryIndexEntry entry = entries[i];

                if (((entry.Key.Flags & FileNameRecordFlags.Hidden) != 0) && _fileSystem.Options.HideHiddenFiles)
                {
                    entries.RemoveAt(i);
                }
                else if (((entry.Key.Flags & FileNameRecordFlags.System) != 0) && _fileSystem.Options.HideSystemFiles)
                {
                    entries.RemoveAt(i);
                }
                else if (entry.Value.MftIndex < 24 && _fileSystem.Options.HideMetafiles)
                {
                    entries.RemoveAt(i);
                }
                else if (byRefIndex.ContainsKey(entry.Value))
                {
                    DirectoryIndexEntry storedEntry = byRefIndex[entry.Value];
                    if (Utilities.Is8Dot3(storedEntry.Key.FileName))
                    {
                        // Make this the definitive entry for the file
                        byRefIndex[entry.Value] = entry;

                        // Remove the old one from the 'entries' array.
                        for (int j = i - 1; j >= 0; --j)
                        {
                            if (entries[j].Value == entry.Value)
                            {
                                entries.RemoveAt(j);
                            }
                        }
                    }
                    else
                    {
                        // Remove this entry
                        entries.RemoveAt(i);
                    }
                }
                else
                {
                    // Only increment if there's no collision - if there was one
                    // we'll have removed an earlier entry in the array, effectively
                    // moving us on one.
                    byRefIndex.Add(entry.Value, entry);
                    ++i;
                }
            }

            return entries;
        }

        private sealed class FileNameComparer : IComparer<FileNameRecord>
        {
            public int Compare(FileNameRecord x, FileNameRecord y)
            {
                return string.Compare(x.FileName, y.FileName, StringComparison.OrdinalIgnoreCase);
            }
        }

        private sealed class FileNameQuery : IComparable<FileNameRecord>
        {
            private string _query;

            public FileNameQuery(string query)
            {
                _query = query;
            }

            public int CompareTo(FileNameRecord other)
            {
                return string.Compare(_query, other.FileName, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
