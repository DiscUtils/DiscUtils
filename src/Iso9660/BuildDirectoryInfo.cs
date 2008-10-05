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
using System.Linq;
using System.Text;

namespace DiscUtils.Iso9660
{
    public class BuildDirectoryInfo : BuildDirectoryMember
    {
        private BuildDirectoryInfo parent;
        private Dictionary<string, BuildDirectoryMember> members;
        private int hierarchyDepth;

        internal BuildDirectoryInfo(string name, BuildDirectoryInfo parent)
            : base(name, MakeShortDirName(name, parent))
        {
            this.parent = (parent == null) ? this : parent;
            this.hierarchyDepth = (parent == null) ? 0 : parent.hierarchyDepth + 1;
            this.members = new Dictionary<string, BuildDirectoryMember>();
        }

        internal void Add(BuildDirectoryMember member)
        {
            members.Add(member.Name, member);
        }

        public override BuildDirectoryInfo Parent
        {
            get { return parent; }
        }

        internal int HierarchyDepth
        {
            get { return hierarchyDepth; }
        }

        public bool TryGetMember(string name, out BuildDirectoryMember member)
        {
            return members.TryGetValue(name, out member);
        }

        internal override long GetDataSize(Encoding enc)
        {
            long total = 0;
            foreach (BuildDirectoryMember m in members.Values)
            {
                total += m.GetDirectoryRecordSize(enc);
            }
            return total + (34 * 2); // Two pseudo entries (self & parent)
        }

        internal uint GetPathTableEntrySize(Encoding enc)
        {
            int nameBytes = enc.GetByteCount(PickName(null, enc));

            return (uint)(8 + nameBytes + (((nameBytes & 0x1) == 1) ? 1 : 0));
        }

        internal int Write(byte[] buffer, int offset, Dictionary<BuildDirectoryMember, uint> locationTable, Encoding enc)
        {
            int pos = 0;

            List<BuildDirectoryMember> sorted = members.Values.ToList();
            sorted.Sort(BuildDirectoryMember.SortedComparison);

            // Two pseudo entries, effectively '.' and '..'
            pos += WriteMember(this, "\0", buffer, offset + pos, locationTable, Encoding.ASCII);
            pos += WriteMember(parent, "\x01", buffer, offset + pos, locationTable, Encoding.ASCII);

            foreach (BuildDirectoryMember m in sorted)
            {
                pos += WriteMember(m, null, buffer, offset + pos, locationTable, enc);
            }
            return pos;
        }

        private static int WriteMember(BuildDirectoryMember m, string nameOverride, byte[] buffer, int offset, Dictionary<BuildDirectoryMember, uint> locationTable, Encoding enc)
        {

            DirectoryRecord dr = new DirectoryRecord();
            dr.FileIdentifier = m.PickName(nameOverride, enc);
            dr.LocationOfExtent = locationTable[m];
            dr.DataLength = (uint)m.GetDataSize(enc);
            dr.RecordingDateAndTime = m.CreationTime;
            dr.Flags = (m is BuildDirectoryInfo) ? FileFlags.Directory : FileFlags.None;
            return dr.WriteTo(buffer, offset, enc);
        }

        private static string MakeShortDirName(string longName, BuildDirectoryInfo dir)
        {
            if (Utilities.isValidDirectoryName(longName))
            {
                return longName;
            }

            char[] shortNameChars = longName.ToUpper().ToCharArray();
            for (int i = 0; i < shortNameChars.Length; ++i)
            {
                if (!Utilities.isValidDChar(shortNameChars[i]) && shortNameChars[i] != '.' && shortNameChars[i] != ';')
                {
                    shortNameChars[i] = '_';
                }
            }

            return new string(shortNameChars);
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
                    char xChar = (i < x.Length) ? x[i] : padChar;
                    char yChar = (i < y.Length) ? y[i] : padChar;

                    if (xChar != yChar)
                    {
                        return xChar - yChar;
                    }
                }

                return 0;
            }
        }

        internal static readonly Comparer<BuildDirectoryInfo> PathTableSortComparison = new PathTableComparison();
    }

}
