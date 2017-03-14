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

namespace DiscUtils.SquashFs
{
    internal sealed class BuilderDirectory : BuilderNode
    {
        private readonly List<Entry> _children;
        private readonly Dictionary<string, Entry> _index;
        private DirectoryInode _inode;

        public BuilderDirectory()
        {
            _children = new List<Entry>();
            _index = new Dictionary<string, Entry>();
        }

        public override Inode Inode
        {
            get { return _inode; }
        }

        public void AddChild(string name, BuilderNode node)
        {
            if (name.Contains(@"\\"))
            {
                throw new ArgumentException("Single level of path must be provided", nameof(name));
            }

            if (_index.ContainsKey(name))
            {
                throw new IOException("The directory entry '" + name + "' already exists");
            }

            Entry newEntry = new Entry { Name = name, Node = node };
            _children.Add(newEntry);
            _index.Add(name, newEntry);
        }

        public BuilderNode GetChild(string name)
        {
            Entry result;
            if (_index.TryGetValue(name, out result))
            {
                return result.Node;
            }

            return null;
        }

        public override void Reset()
        {
            foreach (Entry entry in _children)
            {
                entry.Node.Reset();
            }

            _inode = new DirectoryInode();
        }

        public override void Write(BuilderContext context)
        {
            if (_written)
            {
                return;
            }

            _children.Sort();

            foreach (Entry entry in _children)
            {
                entry.Node.Write(context);
            }

            WriteDirectory(context);

            WriteInode(context);

            _written = true;
        }

        private void WriteDirectory(BuilderContext context)
        {
            MetadataRef startPos = context.DirectoryWriter.Position;

            int currentChild = 0;
            int numDirs = 0;
            while (currentChild < _children.Count)
            {
                long thisBlock = _children[currentChild].Node.InodeRef.Block;
                int firstInode = _children[currentChild].Node.InodeNumber;

                int count = 1;
                while (currentChild + count < _children.Count
                       && _children[currentChild + count].Node.InodeRef.Block == thisBlock
                       && _children[currentChild + count].Node.InodeNumber - firstInode < 0x7FFF)
                {
                    ++count;
                }

                DirectoryHeader hdr = new DirectoryHeader
                {
                    Count = count - 1,
                    InodeNumber = firstInode,
                    StartBlock = (int)thisBlock
                };

                hdr.WriteTo(context.IoBuffer, 0);
                context.DirectoryWriter.Write(context.IoBuffer, 0, hdr.Size);

                for (int i = 0; i < count; ++i)
                {
                    Entry child = _children[currentChild + i];
                    DirectoryRecord record = new DirectoryRecord
                    {
                        Offset = (ushort)child.Node.InodeRef.Offset,
                        InodeNumber = (short)(child.Node.InodeNumber - firstInode),
                        Type = child.Node.Inode.Type,
                        Name = child.Name
                    };

                    record.WriteTo(context.IoBuffer, 0);
                    context.DirectoryWriter.Write(context.IoBuffer, 0, record.Size);

                    if (child.Node.Inode.Type == InodeType.Directory
                        || child.Node.Inode.Type == InodeType.ExtendedDirectory)
                    {
                        ++numDirs;
                    }
                }

                currentChild += count;
            }

            long size = context.DirectoryWriter.DistanceFrom(startPos);
            if (size > uint.MaxValue)
            {
                throw new NotImplementedException("Writing large directories");
            }

            NumLinks = numDirs + 2; // +1 for self, +1 for parent

            _inode.StartBlock = (uint)startPos.Block;
            _inode.Offset = (ushort)startPos.Offset;
            _inode.FileSize = (uint)size + 3; // For some reason, always +3
        }

        private void WriteInode(BuilderContext context)
        {
            FillCommonInodeData(context);
            _inode.Type = InodeType.Directory;

            InodeRef = context.InodeWriter.Position;

            _inode.WriteTo(context.IoBuffer, 0);

            context.InodeWriter.Write(context.IoBuffer, 0, _inode.Size);
        }

        private class Entry : IComparable<Entry>
        {
            public string Name;
            public BuilderNode Node;

            public int CompareTo(Entry other)
            {
                return string.CompareOrdinal(Name, other.Name);
            }
        }
    }
}