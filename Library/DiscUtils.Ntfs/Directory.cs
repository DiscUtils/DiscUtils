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
using System.Linq;
using System.Text;
using DiscUtils.Internal;

using DirectoryIndexEntry =
    System.Collections.Generic.KeyValuePair<DiscUtils.Ntfs.FileNameRecord, DiscUtils.Ntfs.FileRecordReference>;

namespace DiscUtils.Ntfs;

internal class Directory : File
{
    public Directory(INtfsContext context, FileRecord baseRecord)
        : base(context, baseRecord) { }

    private IndexView<FileNameRecord, FileRecordReference> Index
    {
        get
        {
            if (field == null && StreamExists(AttributeType.IndexRoot, "$I30"))
            {
                field = new IndexView<FileNameRecord, FileRecordReference>(GetIndex("$I30"));
            }

            return field;
        }

        set;
    }

    public bool IsEmpty => Index.Count == 0;

    public IEnumerable<DirectoryEntry> GetAllEntries(Func<DirectoryIndexEntry, bool> filter)
    {
        var entries = Index.Entries;

        if (filter is not null)
        {
            entries = entries.Where(filter);
        }

        return entries.Select(entry => new DirectoryEntry(this, entry.Value, entry.Key));
    }

    public void UpdateEntry(DirectoryEntry entry)
    {
        Index[entry.Details] = entry.Reference;
        UpdateRecordInMft();
    }

    public override void Dump(TextWriter writer, string indent)
    {
        writer.WriteLine($"{indent}DIRECTORY ({base.ToString()})");
        writer.WriteLine($"{indent}  File Number: {IndexInMft}");

        if (Index != null)
        {
            foreach (var entry in Index.Entries)
            {
                writer.WriteLine($"{indent}  DIRECTORY ENTRY ({entry.Key.FileName})");
                writer.WriteLine($"{indent}    MFT Ref: {entry.Value}");
                entry.Key.Dump(writer, $"{indent}    ");
            }
        }
    }

    public override string ToString()
    {
        return base.ToString() + Path.DirectorySeparatorChar;
    }

    internal static new Directory CreateNew(INtfsContext context, NtfsFileAttributes parentDirFlags)
    {
        var dir = (Directory)context.AllocateFile(FileRecordFlags.IsDirectory);

        StandardInformation.InitializeNewFile(
            dir,
            NtfsFileAttributes.Archive | (parentDirFlags & NtfsFileAttributes.Compressed));

        // Create the index root attribute by instantiating a new index
        dir.CreateIndex("$I30", AttributeType.FileName, AttributeCollationRule.Filename);

        dir.UpdateRecordInMft();

        return dir;
    }

    internal DirectoryEntry? GetEntryByName(string name, bool requireUnique = false)
    {
        var searchName = name;

        var streamSepPos = name.IndexOf(':');
        if (streamSepPos >= 0)
        {
            searchName = name[..streamSepPos];
        }

        using var matches = Index.FindAll(new FileNameQuery(searchName, _context.UpperCase), validate: requireUnique).GetEnumerator();
        if (!matches.MoveNext())
        {
            return null;
        }

        var entry = matches.Current;
        while (requireUnique && matches.MoveNext())
        {
            if (matches.Current.Value != entry.Value)
            {
                throw new InvalidDataException($"The directory has ambiguous entries for {name}");
            }
        }

        return new DirectoryEntry(this, entry.Value, entry.Key);
    }

    internal DirectoryEntry AddEntry(File file, string name, FileNameNamespace nameNamespace)
    {
        if (name.Length > 255)
        {
            throw new IOException($"Invalid file name, more than 255 characters: {name}");
        }

        if (name.AsSpan().IndexOfAny('\0', '/') != -1)
        {
            throw new IOException($@"Invalid file name, contains '\0' or '/': {name}");
        }

        var newNameRecord = file.GetFileNameRecord(null, true);
        newNameRecord.FileNameNamespace = nameNamespace;
        newNameRecord.FileName = name;
        newNameRecord.ParentDirectory = MftReference;

        var nameStream = file.CreateStream(AttributeType.FileName, null);
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
        var file = _context.GetFileByRef(dirEntry.Reference)
            ?? throw new FileNotFoundException($"Cannot find file '{dirEntry.SearchName}', {dirEntry.Reference}");

        var nameRecord = dirEntry.Details;

        Index.Remove(dirEntry.Details);

        foreach (var stream in file.GetStreams(AttributeType.FileName, null))
        {
            var streamName = stream.GetContent<FileNameRecord>();
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
        var baseName = string.Empty;
        var ext = string.Empty;

        var lastPeriod = name.LastIndexOf('.');

        var i = 0;
        while (baseName.Length < 6 && i < name.Length && i != lastPeriod)
        {
            var upperChar = char.ToUpperInvariant(name[i]);
            if (Utilities.Is8Dot3Char(upperChar, ignoreCase: false))
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
                var upperChar = char.ToUpperInvariant(name[i]);
                if (Utilities.Is8Dot3Char(upperChar, ignoreCase: false))
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
            var suffix = $"~{i}";
#if NET6_0_OR_GREATER
            candidate = $"{baseName.AsSpan(0, Math.Min(8 - suffix.Length, baseName.Length))}{suffix}{(ext.Length > 0 ? $".{ext}" : string.Empty)}";
#else
            candidate = $"{baseName[..Math.Min(8 - suffix.Length, baseName.Length)]}{suffix}{(ext.Length > 0 ? $".{ext}" : string.Empty)}";
#endif
            i++;
        } while (GetEntryByName(candidate) != null);

        return candidate;
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
            var fnLen = buffer[0x40];
            return _upperCase.Compare(_query, 0, _query.Length, buffer, 0x42, fnLen * 2);
        }
    }
}