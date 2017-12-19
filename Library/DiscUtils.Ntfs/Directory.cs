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
using System.Globalization;
using System.IO;
using System.Text;
using DiscUtils.Internal;

namespace DiscUtils.Ntfs
{
    using DirectoryIndexEntry =
        KeyValuePair<FileNameRecord, FileRecordReference>;

    internal class Directory : File
    {
        private IndexView<FileNameRecord, FileRecordReference> _index;

        public Directory(INtfsContext context, FileRecord baseRecord)
            : base(context, baseRecord) {}

        private IndexView<FileNameRecord, FileRecordReference> Index
        {
            get
            {
                if (_index == null && StreamExists(AttributeType.IndexRoot, "$I30"))
                {
                    _index = new IndexView<FileNameRecord, FileRecordReference>(GetIndex("$I30"));
                }

                return _index;
            }
        }

        public bool IsEmpty
        {
            get { return Index.Count == 0; }
        }

        public IEnumerable<DirectoryEntry> GetAllEntries(bool filter)
        {
            IEnumerable<DirectoryIndexEntry> entries = filter ? FilterEntries(Index.Entries) : Index.Entries;

            foreach (DirectoryIndexEntry entry in entries)
            {
                yield return new DirectoryEntry(this, entry.Value, entry.Key);
            }
        }

        public void UpdateEntry(DirectoryEntry entry)
        {
            Index[entry.Details] = entry.Reference;
            UpdateRecordInMft();
        }

        public override void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "DIRECTORY (" + base.ToString() + ")");
            writer.WriteLine(indent + "  File Number: " + IndexInMft);

            if (Index != null)
            {
                foreach (DirectoryIndexEntry entry in Index.Entries)
                {
                    writer.WriteLine(indent + "  DIRECTORY ENTRY (" + entry.Key.FileName + ")");
                    writer.WriteLine(indent + "    MFT Ref: " + entry.Value);
                    entry.Key.Dump(writer, indent + "    ");
                }
            }
        }

        public override string ToString()
        {
            return base.ToString() + @"\";
        }

        internal new static Directory CreateNew(INtfsContext context, FileAttributeFlags parentDirFlags)
        {
            Directory dir = (Directory)context.AllocateFile(FileRecordFlags.IsDirectory);

            StandardInformation.InitializeNewFile(
                dir,
                FileAttributeFlags.Archive | (parentDirFlags & FileAttributeFlags.Compressed));

            // Create the index root attribute by instantiating a new index
            dir.CreateIndex("$I30", AttributeType.FileName, AttributeCollationRule.Filename);

            dir.UpdateRecordInMft();

            return dir;
        }

        internal DirectoryEntry GetEntryByName(string name)
        {
            string searchName = name;

            int streamSepPos = name.IndexOf(':');
            if (streamSepPos >= 0)
            {
                searchName = name.Substring(0, streamSepPos);
            }

            DirectoryIndexEntry entry = Index.FindFirst(new FileNameQuery(searchName, _context.UpperCase));
            if (entry.Key != null)
            {
                return new DirectoryEntry(this, entry.Value, entry.Key);
            }
            return null;
        }

        internal DirectoryEntry AddEntry(File file, string name, FileNameNamespace nameNamespace)
        {
            if (name.Length > 255)
            {
                throw new IOException("Invalid file name, more than 255 characters: " + name);
            }
            if (name.IndexOfAny(new[] { '\0', '/' }) != -1)
            {
                throw new IOException(@"Invalid file name, contains '\0' or '/': " + name);
            }

            FileNameRecord newNameRecord = file.GetFileNameRecord(null, true);
            newNameRecord.FileNameNamespace = nameNamespace;
            newNameRecord.FileName = name;
            newNameRecord.ParentDirectory = MftReference;

            NtfsStream nameStream = file.CreateStream(AttributeType.FileName, null);
            nameStream.SetContent(newNameRecord);

            file.HardLinkCount++;
            file.UpdateRecordInMft();

            Index[newNameRecord] = file.MftReference;

            Modified();
            UpdateRecordInMft();

            return new DirectoryEntry(this, file.MftReference, newNameRecord);
        }

        internal void RemoveEntry(DirectoryEntry dirEntry)
        {
            File file = _context.GetFileByRef(dirEntry.Reference);

            FileNameRecord nameRecord = dirEntry.Details;

            Index.Remove(dirEntry.Details);

            foreach (NtfsStream stream in file.GetStreams(AttributeType.FileName, null))
            {
                FileNameRecord streamName = stream.GetContent<FileNameRecord>();
                if (nameRecord.Equals(streamName))
                {
                    file.RemoveStream(stream);
                    break;
                }
            }

            file.HardLinkCount--;
            file.UpdateRecordInMft();

            Modified();
            UpdateRecordInMft();
        }

        internal string CreateShortName(string name)
        {
            string baseName = string.Empty;
            string ext = string.Empty;

            int lastPeriod = name.LastIndexOf('.');

            int i = 0;
            while (baseName.Length < 6 && i < name.Length && i != lastPeriod)
            {
                char upperChar = char.ToUpperInvariant(name[i]);
                if (Utilities.Is8Dot3Char(upperChar))
                {
                    baseName += upperChar;
                }

                ++i;
            }

            if (lastPeriod >= 0)
            {
                i = lastPeriod + 1;
                while (ext.Length < 3 && i < name.Length)
                {
                    char upperChar = char.ToUpperInvariant(name[i]);
                    if (Utilities.Is8Dot3Char(upperChar))
                    {
                        ext += upperChar;
                    }

                    ++i;
                }
            }

            i = 1;
            string candidate;
            do
            {
                string suffix = string.Format(CultureInfo.InvariantCulture, "~{0}", i);
                candidate = baseName.Substring(0, Math.Min(8 - suffix.Length, baseName.Length)) + suffix +
                            (ext.Length > 0 ? "." + ext : string.Empty);
                i++;
            } while (GetEntryByName(candidate) != null);

            return candidate;
        }

        private List<DirectoryIndexEntry> FilterEntries(IEnumerable<DirectoryIndexEntry> entriesIter)
        {
            List<DirectoryIndexEntry> entries = new List<DirectoryIndexEntry>();

            // Weed out short-name entries for files and any hidden / system / metadata files.
            foreach (var entry in entriesIter)
            {
                if ((entry.Key.Flags & FileAttributeFlags.Hidden) != 0 && _context.Options.HideHiddenFiles)
                {
                    continue;
                }
                if ((entry.Key.Flags & FileAttributeFlags.System) != 0 && _context.Options.HideSystemFiles)
                {
                    continue;
                }
                if (entry.Value.MftIndex < 24 && _context.Options.HideMetafiles)
                {
                    continue;
                }
                if (entry.Key.FileNameNamespace == FileNameNamespace.Dos && _context.Options.HideDosFileNames)
                {
                    continue;
                }
                entries.Add(entry);
            }

            return entries;
        }

        private sealed class FileNameQuery : IComparable<byte[]>
        {
            private readonly byte[] _query;
            private readonly UpperCase _upperCase;

            public FileNameQuery(string query, UpperCase upperCase)
            {
                _query = Encoding.Unicode.GetBytes(query);
                _upperCase = upperCase;
            }

            public int CompareTo(byte[] buffer)
            {
                // Note: this is internal knowledge of FileNameRecord structure - but for performance
                // reasons, we don't want to decode the entire structure.  In fact can avoid the string
                // conversion as well.
                byte fnLen = buffer[0x40];
                return _upperCase.Compare(_query, 0, _query.Length, buffer, 0x42, fnLen * 2);
            }
        }
    }
}