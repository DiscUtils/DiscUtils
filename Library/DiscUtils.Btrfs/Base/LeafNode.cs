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

using System.Collections.Generic;
using System.IO;
using DiscUtils.Btrfs.Base.Items;
using DiscUtils.Streams;

namespace DiscUtils.Btrfs.Base
{
    internal class LeafNode:NodeHeader
    {
        /// <summary>
        /// key pointers
        /// </summary>
        public NodeItem[] Items { get; private set; }

        public BaseItem[] NodeData { get; private set; }

        public override int Size
        {
            get { return (int)(base.Size + ItemCount*KeyPointer.Length); }
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            var itemOffset = base.ReadFrom(buffer, offset);
            Items = new NodeItem[ItemCount];
            NodeData = new BaseItem[ItemCount];
            for (int i = 0; i < ItemCount; i++)
            {
                Items[i] = new NodeItem();
                itemOffset += Items[i].ReadFrom(buffer, itemOffset);
                switch (Items[i].Key.ObjectId)
                {
                    case (ulong)ReservedObjectId.CsumItem:
                    case (ulong)ReservedObjectId.TreeReloc:
                        continue;
                    default:
                        NodeData[i] = CreateItem(Items[i], buffer, Length + offset);
                        break;
                }
            }
            return Size;
        }

        private BaseItem CreateItem(NodeItem item, byte[] buffer, int offset)
        {
            var data = EndianUtilities.ToByteArray(buffer, (int)(offset + item.DataOffset), (int)item.DataSize);
            BaseItem result;
            switch (item.Key.ItemType)
            {
                case ItemType.ChunkItem:
                    result = new ChunkItem(item.Key);
                    break;
                case ItemType.DevItem:
                    result = new DevItem(item.Key);
                    break;
                case ItemType.RootItem:
                    result = new RootItem(item.Key);
                    break;
                case ItemType.InodeRef:
                    result = new InodeRef(item.Key);
                    break;
                case ItemType.InodeItem:
                    result = new InodeItem(item.Key);
                    break;
                case ItemType.DirItem:
                    result = new DirItem(item.Key);
                    break;
                case ItemType.DirIndex:
                    result = new DirIndex(item.Key);
                    break;
                case ItemType.ExtentData:
                    result = new ExtentData(item.Key);
                    break;
                case ItemType.RootRef:
                    result = new RootRef(item.Key);
                    break;
                case ItemType.RootBackref:
                    result = new RootBackref(item.Key);
                    break;
                case ItemType.XattrItem:
                    result = new XattrItem(item.Key);
                    break;
                case ItemType.OrphanItem:
                    result = new OrphanItem(item.Key);
                    break;
                default:
                    throw new IOException($"Unsupported item type {item.Key.ItemType}");
            }
            result.ReadFrom(data, 0);
            return result;
        }

        public override IEnumerable<BaseItem> Find(Key key, Context context)
        {
            for (int i = 0; i < Items.Length; i++)
            {
                if (Items[i].Key.ObjectId > key.ObjectId)
                    break;
                if (Items[i].Key.ObjectId == key.ObjectId && Items[i].Key.ItemType == key.ItemType)
                    yield return NodeData[i];
            }
        }
    }
}
