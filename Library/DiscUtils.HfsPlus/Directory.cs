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
using DiscUtils.Vfs;

namespace DiscUtils.HfsPlus
{
    internal sealed class Directory : File, IVfsDirectory<DirEntry, File>
    {
        public Directory(Context context, CatalogNodeId nodeId, CommonCatalogFileInfo fileInfo)
            : base(context, nodeId, fileInfo) {}

        public ICollection<DirEntry> AllEntries
        {
            get
            {
                List<DirEntry> results = new List<DirEntry>();

                Context.Catalog.VisitRange((key, data) =>
                       {
                           if (key.NodeId == NodeId)
                           {
                               if (data != null && !string.IsNullOrEmpty(key.Name) && DirEntry.IsFileOrDirectory(data))
                               {
                                   results.Add(new DirEntry(key.Name, data));
                               }

                               return 0;
                           }
                           return key.NodeId < NodeId ? -1 : 1;
                       });

                return results;
            }
        }

        public DirEntry Self
        {
            get
            {
                byte[] dirThreadData = Context.Catalog.Find(new CatalogKey(NodeId, string.Empty));

                CatalogThread dirThread = new CatalogThread();
                dirThread.ReadFrom(dirThreadData, 0);

                byte[] dirEntryData = Context.Catalog.Find(new CatalogKey(dirThread.ParentId, dirThread.Name));

                return new DirEntry(dirThread.Name, dirEntryData);
            }
        }

        public DirEntry GetEntryByName(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Attempt to lookup empty file name", nameof(name));
            }

            byte[] dirEntryData = Context.Catalog.Find(new CatalogKey(NodeId, name));
            if (dirEntryData == null)
            {
                return null;
            }

            return new DirEntry(name, dirEntryData);
        }

        public DirEntry CreateNewFile(string name)
        {
            throw new NotSupportedException();
        }
    }
}