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
using DiscUtils.Btrfs.Base.Items;
using DiscUtils.Streams;

namespace DiscUtils.Btrfs.Base
{
    internal abstract class NodeHeader : IByteArraySerializable
    {
        public static readonly int Length = 0x65;

        /// <summary>
        /// Checksum of everything past this field (from 20 to the end of the node)
        /// </summary>
        public byte[] Checksum { get; private set; }

        /// <summary>
        /// FS UUID
        /// </summary>
        public Guid FsUuid { get; private set; }

        /// <summary>
        /// Logical address of this node
        /// </summary>
        public ulong LogicalAddress { get; private set; }

        /// <summary>
        /// Flags
        /// </summary>
        public long Flags { get; private set; }

        /// <summary>
        /// Backref. Rev.: always 1 (MIXED) for new filesystems; 0 (OLD) indicates an old filesystem.
        /// </summary>
        public byte BackrefRevision { get; private set; }
        /// <summary>
        /// Chunk tree UUID
        /// </summary>
        public Guid ChunkTreeUuid { get; private set; }

        /// <summary>
        /// Logical address of this node
        /// </summary>
        public ulong Generation { get; private set; }

        /// <summary>
        /// The ID of the tree that contains this node
        /// </summary>
        public ulong TreeId { get; private set; }

        /// <summary>
        /// Number of items
        /// </summary>
        public uint ItemCount { get; private set; }
        /// <summary>
        /// Level (0 for leaf nodes)
        /// </summary>
        public byte Level { get; private set; }

        public virtual int Size
        {
            get { return Length; }
        }

        public virtual int ReadFrom(byte[] buffer, int offset)
        {
            Checksum = EndianUtilities.ToByteArray(buffer, offset, 0x20);
            FsUuid = EndianUtilities.ToGuidLittleEndian(buffer, offset + 0x20);
            LogicalAddress = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x30);
            //todo validate shift
            Flags = EndianUtilities.ToInt64LittleEndian(buffer, offset + 0x38)>>8;
            BackrefRevision = buffer[offset + 0x3f];
            ChunkTreeUuid = EndianUtilities.ToGuidLittleEndian(buffer, offset + 0x40);
            Generation = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x50);

            TreeId = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x58);
            ItemCount = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0x60);
            Level = buffer[offset + 0x64];
            return Length;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public static NodeHeader Create(byte[] buffer, int offset)
        {
            var level = buffer[offset + 0x64];
            NodeHeader result;
            if (level == 0)
                result = new LeafNode();
            else
                result = new InternalNode();
            result.ReadFrom(buffer, offset);
            return result;
        }

        public abstract IEnumerable<BaseItem> Find(Key key, Context context);

        public BaseItem FindFirst(Key key, Context context)
        {
            foreach (var item in Find(key, context))
            {
                return item;
            }
            return null;
        }

        public T FindFirst<T>(Key key, Context context) where T : BaseItem
        {
            foreach (var item in Find<T>(key, context))
            {
                return item;
            }
            return null;
        }

        public IEnumerable<T> Find<T>(Key key, Context context) where T : BaseItem
        {
            foreach (var item in Find(key, context))
            {
                var typed = item as T;
                if (typed != null)
                    yield return typed;
            }
        }
    }
}
