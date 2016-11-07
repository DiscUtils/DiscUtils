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

namespace DiscUtils.SquashFs
{
    internal abstract class BuilderNode
    {
        protected bool _written;

        public BuilderNode()
        {
            ModificationTime = DateTime.Now;
        }

        public int GroupId { get; set; }

        public abstract Inode Inode { get; }

        public int InodeNumber { get; set; }

        public MetadataRef InodeRef { get; set; }

        public UnixFilePermissions Mode { get; set; }

        public DateTime ModificationTime { get; set; }

        public int NumLinks { get; set; }

        public int UserId { get; set; }

        public virtual void Reset()
        {
            _written = false;
        }

        public abstract void Write(BuilderContext context);

        protected void FillCommonInodeData(BuilderContext context)
        {
            Inode.Mode = (ushort)Mode;
            Inode.UidKey = context.AllocateId(UserId);
            Inode.GidKey = context.AllocateId(GroupId);
            Inode.ModificationTime = ModificationTime;
            InodeNumber = (int)context.AllocateInode();
            Inode.InodeNumber = (uint)InodeNumber;
            Inode.NumLinks = NumLinks;
        }
    }
}