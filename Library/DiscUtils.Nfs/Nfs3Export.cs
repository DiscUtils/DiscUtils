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

namespace DiscUtils.Nfs
{
    public sealed class Nfs3Export
    {
        internal Nfs3Export(XdrDataReader reader)
        {
            DirPath = reader.ReadString(Nfs3Mount.MaxPathLength);

            List<string> groups = new List<string>();
            while (reader.ReadBool())
            {
                groups.Add(reader.ReadString(Nfs3Mount.MaxNameLength));
            }

            Groups = groups;
        }

        public Nfs3Export()
        {
        }

        public string DirPath { get; set; }

        public List<string> Groups { get; set; }

        internal void Write(XdrDataWriter writer)
        {
            writer.Write(DirPath);

            foreach (var group in Groups)
            {
                writer.Write(true);
                writer.Write(group);
            }

            writer.Write(false);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Nfs3Export);
        }

        public bool Equals(Nfs3Export other)
        {
            if (other == null)
            {
                return false;
            }

            if (!string.Equals(other.DirPath, DirPath))
            {
                return false;
            }

            if (other.Groups == null || Groups == null)
            {
                return false;
            }

            for (int i = 0; i < Groups.Count; i++)
            {
                if (!string.Equals(other.Groups[i], Groups[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(DirPath, Groups);
        }
    }
}