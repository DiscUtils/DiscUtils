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
using System.Text;
using DiscUtils.CoreCompat;
using DiscUtils.Streams;

namespace DiscUtils.Iso9660
{
    /// <summary>
    /// Represents a directory that will be built into the ISO image.
    /// </summary>
    public sealed class BuildDirectoryInfo : BuildDirectoryMember
    {
        internal static readonly Comparer<BuildDirectoryInfo> PathTableSortComparison = new PathTableComparison();
        private readonly Dictionary<string, BuildDirectoryMember> _members;

        private readonly BuildDirectoryInfo _parent;
        private List<BuildDirectoryMember> _sortedMembers;

        internal BuildDirectoryInfo(string name, BuildDirectoryInfo parent)
            : base(name, MakeShortDirName(name, parent))
        {
            _parent = parent == null ? this : parent;
            HierarchyDepth = parent == null ? 0 : parent.HierarchyDepth + 1;
            _members = new Dictionary<string, BuildDirectoryMember>();
        }

        internal int HierarchyDepth { get; }

        /// <summary>
        /// The parent directory, or <c>null</c> if none.
        /// </summary>
        public override BuildDirectoryInfo Parent
        {
            get { return _parent; }
        }

        /// <summary>
        /// Gets the specified child directory or file.
        /// </summary>
        /// <param name="name">The name of the file or directory to get.</param>
        /// <param name="member">The member found (or <c>null</c>).</param>
        /// <returns><c>true</c> if the specified member was found.</returns>
        internal bool TryGetMember(string name, out BuildDirectoryMember member)
        {
            return _members.TryGetValue(name, out member);
        }

        internal void Add(BuildDirectoryMember member)
        {
            _members.Add(member.Name, member);
            _sortedMembers = null;
        }

        internal override long GetDataSize(Encoding enc)
        {
            List<BuildDirectoryMember> sorted = GetSortedMembers();

            long total = 34 * 2; // Two pseudo entries (self & parent)

            foreach (BuildDirectoryMember m in sorted)
            {
                uint recordSize = m.GetDirectoryRecordSize(enc);

                // If this record would span a sector boundary, then the current sector is
                // zero-padded, and the record goes at the start of the next sector.
                if (total % IsoUtilities.SectorSize + recordSize > IsoUtilities.SectorSize)
                {
                    long padLength = IsoUtilities.SectorSize - total % IsoUtilities.SectorSize;
                    total += padLength;
                }

                total += recordSize;
            }

            return MathUtilities.RoundUp(total, IsoUtilities.SectorSize);
        }

        internal uint GetPathTableEntrySize(Encoding enc)
        {
            int nameBytes = enc.GetByteCount(PickName(null, enc));

            return (uint)(8 + nameBytes + ((nameBytes & 0x1) == 1 ? 1 : 0));
        }

        internal int Write(byte[] buffer, int offset, Dictionary<BuildDirectoryMember, uint> locationTable, Encoding enc)
        {
            int pos = 0;

            List<BuildDirectoryMember> sorted = GetSortedMembers();

            // Two pseudo entries, effectively '.' and '..'
            pos += WriteMember(this, "\0", Encoding.ASCII, buffer, offset + pos, locationTable, enc);
            pos += WriteMember(_parent, "\x01", Encoding.ASCII, buffer, offset + pos, locationTable, enc);

            foreach (BuildDirectoryMember m in sorted)
            {
                uint recordSize = m.GetDirectoryRecordSize(enc);

                if (pos % IsoUtilities.SectorSize + recordSize > IsoUtilities.SectorSize)
                {
                    int padLength = IsoUtilities.SectorSize - pos % IsoUtilities.SectorSize;
                    Array.Clear(buffer, offset + pos, padLength);
                    pos += padLength;
                }

                pos += WriteMember(m, null, enc, buffer, offset + pos, locationTable, enc);
            }

            // Ensure final padding data is zero'd
            int finalPadLength = MathUtilities.RoundUp(pos, IsoUtilities.SectorSize) - pos;
            Array.Clear(buffer, offset + pos, finalPadLength);

            return pos + finalPadLength;
        }

        private static int WriteMember(BuildDirectoryMember m, string nameOverride, Encoding nameEnc, byte[] buffer, int offset, Dictionary<BuildDirectoryMember, uint> locationTable, Encoding dataEnc)
        {
            DirectoryRecord dr = new DirectoryRecord();
            dr.FileIdentifier = m.PickName(nameOverride, nameEnc);
            dr.LocationOfExtent = locationTable[m];
            dr.DataLength = (uint)m.GetDataSize(dataEnc);
            dr.RecordingDateAndTime = m.CreationTime;
            dr.Flags = m is BuildDirectoryInfo ? FileFlags.Directory : FileFlags.None;
            return dr.WriteTo(buffer, offset, nameEnc);
        }

        private static string MakeShortDirName(string longName, BuildDirectoryInfo dir)
        {
            if (IsoUtilities.IsValidDirectoryName(longName))
            {
                return longName;
            }

            char[] shortNameChars = longName.ToUpper(CultureInfo.InvariantCulture).ToCharArray();
            for (int i = 0; i < shortNameChars.Length; ++i)
            {
                if (!IsoUtilities.IsValidDChar(shortNameChars[i]) && shortNameChars[i] != '.' && shortNameChars[i] != ';')
                {
                    shortNameChars[i] = '_';
                }
            }

            return new string(shortNameChars);
        }

        private List<BuildDirectoryMember> GetSortedMembers()
        {
            if (_sortedMembers == null)
            {
                List<BuildDirectoryMember> sorted = new List<BuildDirectoryMember>(_members.Values);
                sorted.Sort(SortedComparison);
                _sortedMembers = sorted;
            }

            return _sortedMembers;
        }

        private class PathTableComparison : Comparer<BuildDirectoryInfo>
        {
            public override int Compare(BuildDirectoryInfo x, BuildDirectoryInfo y)
            {
                if (x.HierarchyDepth != y.HierarchyDepth)
                {
                    return x.HierarchyDepth - y.HierarchyDepth;
                }

                if (x.Parent != y.Parent)
                {
                    return Compare(x.Parent, y.Parent);
                }

                return CompareNames(x.Name, y.Name, ' ');
            }

            private static int CompareNames(string x, string y, char padChar)
            {
                int max = Math.Max(x.Length, y.Length);
                for (int i = 0; i < max; ++i)
                {
                    char xChar = i < x.Length ? x[i] : padChar;
                    char yChar = i < y.Length ? y[i] : padChar;

                    if (xChar != yChar)
                    {
                        return xChar - yChar;
                    }
                }

                return 0;
            }
        }
    }
}