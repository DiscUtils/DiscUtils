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
using DirectoryIndexEntry = System.Collections.Generic.KeyValuePair<DiscUtils.Ntfs.FileNameRecord, DiscUtils.Ntfs.FileReference>;

namespace DiscUtils.Ntfs
{
    internal class Directory : File
    {
        private Index<FileNameRecord, FileReference> _index;


        public Directory(INtfsContext fileSystem, MasterFileTable mft, FileRecord baseRecord)
            : base(fileSystem, baseRecord)
        {
            _index = new Index<FileNameRecord, FileReference>(this, "$I30", _fileSystem.BiosParameterBlock, _fileSystem.UpperCase);
        }

        internal DirectoryEntry GetEntryByName(string name)
        {
            string searchName = name;

            int streamSepPos = name.IndexOf(':');
            if (streamSepPos >= 0)
            {
                searchName = name.Substring(0, streamSepPos);
            }

            DirectoryIndexEntry entry = _index.FindFirst(new FileNameQuery(searchName, _fileSystem.UpperCase));
            if (entry.Key != null && entry.Value != null)
            {
                return new DirectoryEntry(this, entry.Value, entry.Key);
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
                yield return new DirectoryEntry(this, entry.Value, entry.Key);
            }
        }

        public void UpdateEntry(DirectoryEntry entry)
        {
            _index[entry.Details] = entry.Reference;
        }


        internal DirectoryEntry AddEntry(File file, string name)
        {
            FileNameRecord newNameRecord = file.GetFileNameRecord(null, true);
            newNameRecord.FileNameNamespace = FileNameNamespace.Posix;
            newNameRecord.FileName = name;
            newNameRecord.ParentDirectory = MftReference;

            ushort newNameAttrId = file.CreateAttribute(AttributeType.FileName);
            file.SetAttributeContent(newNameAttrId, newNameRecord);

            file.HardLinkCount++;
            file.UpdateRecordInMft();

            _index[newNameRecord] = file.MftReference;

            return new DirectoryEntry(this, file.MftReference, newNameRecord);
        }

        public override void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "DIRECTORY (" + base.ToString() + ")");
            writer.WriteLine(indent + "  File Number: " + IndexInMft);

            foreach (var entry in _index.Entries)
            {
                writer.WriteLine(indent + "  DIRECTORY ENTRY (" + entry.Key.FileName + ")");
                writer.WriteLine(indent + "    MFT Ref: " + entry.Value);
                entry.Key.Dump(writer, indent + "    ");
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

                if (((entry.Key.Flags & FileAttributeFlags.Hidden) != 0) && _fileSystem.Options.HideHiddenFiles)
                {
                    entries.RemoveAt(i);
                }
                else if (((entry.Key.Flags & FileAttributeFlags.System) != 0) && _fileSystem.Options.HideSystemFiles)
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

        private sealed class FileNameQuery : IComparable<FileNameRecord>
        {
            private string _query;
            private IComparer<string> _nameComparer;

            public FileNameQuery(string query, IComparer<string> nameComparer)
            {
                _query = query;
                _nameComparer = nameComparer;
            }

            public int CompareTo(FileNameRecord other)
            {
                return _nameComparer.Compare(_query, other.FileName);
            }
        }
    }
}
