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
using DiscUtils.Streams;
using DiscUtils.Vfs;

namespace DiscUtils.Ext
{
    internal class Directory : File, IVfsDirectory<DirEntry, File>
    {
        public Directory(Context context, uint inodeNum, Inode inode)
            : base(context, inodeNum, inode) {}

        public ICollection<DirEntry> AllEntries
        {
            get
            {
                List<DirEntry> dirEntries = new List<DirEntry>();

                IBuffer content = FileContent;
                uint blockSize = Context.SuperBlock.BlockSize;

                byte[] blockData = new byte[blockSize];
                uint relBlock = 0;

                long pos = 0;
                while (pos < Inode.FileSize)
                {
                    StreamUtilities.ReadMaximum(content, blockSize * (long)relBlock, blockData, 0, (int)blockSize);

                    int blockPos = 0;
                    while (blockPos < blockSize)
                    {
                        DirectoryRecord r = new DirectoryRecord(Context.Options.FileNameEncoding);
                        int numRead = r.ReadFrom(blockData, blockPos);

                        if (r.Inode != 0 && r.Name != "." && r.Name != "..")
                        {
                            dirEntries.Add(new DirEntry(r));
                        }

                        blockPos += numRead;
                    }

                    ++relBlock;
                    pos += blockSize;
                }

                return dirEntries;
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