//
// Copyright (c) 2017, Bianco Veigel
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
using DiscUtils.Vfs;

namespace DiscUtils.Btrfs
{
    internal class Directory : File, IVfsDirectory<DirEntry, File>
    {
        public Directory(ulong treeId, ulong objectId, Context context):base(treeId, objectId, context)
        {
            
        }

        private Dictionary<string, DirEntry> _allEntries;

        public ICollection<DirEntry> AllEntries
        {
            get
            {
                if (_allEntries != null)
                    return _allEntries.Values;
                var result = new Dictionary<string, DirEntry>();
                var tree = Context.GetFsTree(TreeId);
                throw new NotImplementedException();
                _allEntries = result;
                return result.Values;
            }
        }

        public DirEntry Self
        {
            get { return null; }
        }

        public DirEntry GetEntryByName(string name)
        {
            foreach (DirEntry entry in AllEntries)
            {
                if (entry.FileName == name)
                {
                    return entry;
                }
            }

            return null;
        }

        public DirEntry CreateNewFile(string name)
        {
            throw new NotImplementedException();
        }
    }
}
