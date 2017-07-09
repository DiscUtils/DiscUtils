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
using System.IO;
using DiscUtils.Btrfs.Base;
using DiscUtils.Btrfs.Base.Items;
using DiscUtils.Vfs;

namespace DiscUtils.Btrfs
{
    internal class Context : VfsContext
    {
        public Context()
        {
            FsTrees = new Dictionary<ulong, NodeHeader>();
        }

        public Stream RawStream { get; set; }

        public SuperBlock SuperBlock { get; set; }

        internal NodeHeader ChunkTreeRoot { get; set; }

        internal NodeHeader RootTreeRoot { get; set; }

        internal Dictionary<ulong, NodeHeader> FsTrees { get; }

        internal NodeHeader GetFsTree(ulong treeId)
        {
            NodeHeader tree;
            if (FsTrees.TryGetValue(treeId, out tree))
                return tree;
            var rootItem = RootTreeRoot.FindFirst<RootItem>(new Key(treeId, ItemType.RootItem));
            if (rootItem == null)
                return null;
            tree = ReadTree(rootItem.ByteNr, rootItem.Level);
            FsTrees[treeId] = tree;
            return tree;
        }

        internal ulong MapToPhysical(ulong logical)
        {
            if (ChunkTreeRoot != null)
            {
                var nodes = ChunkTreeRoot.Find<ChunkItem>(new Key(ReservedObjectId.FirstChunkTree, ItemType.ChunkItem));
                foreach(var chunk in nodes)
                {
                    if (chunk.Key.ItemType != ItemType.ChunkItem) continue;
                    if (chunk.Key.Offset > logical) continue;
                    if (chunk.Key.Offset + chunk.ChunkSize < logical) continue;
                    CheckStriping(chunk.Type);
                    if (chunk.StripeCount < 1)
                        throw new IOException("Invalid stripe count in ChunkItem");
                    var stripe = chunk.Stripes[0];
                    return stripe.Offset + (logical - chunk.Key.Offset);
                }
            }
            foreach (var chunk in SuperBlock.SystemChunkArray)
            {
                if (chunk.Key.ItemType != ItemType.ChunkItem) continue;
                if (chunk.Key.Offset > logical) continue;
                if (chunk.Key.Offset  + chunk.ChunkSize < logical) continue;

                CheckStriping(chunk.Type);
                if (chunk.StripeCount <1)
                    throw new IOException("Invalid stripe count in ChunkItem");
                var stripe = chunk.Stripes[0];
                return stripe.Offset + (logical - chunk.Key.Offset);
            }
            throw new IOException("no matching ChunkItem found");
        }

        internal NodeHeader ReadTree(ulong logical, byte level)
        {
            var physical = MapToPhysical(logical);
            RawStream.Seek((long)physical, SeekOrigin.Begin);
            var dataSize = level > 0 ? SuperBlock.NodeSize : SuperBlock.LeafSize;
            var buffer = new byte[dataSize];
            RawStream.Read(buffer, 0, buffer.Length);
            var result = NodeHeader.Create(buffer, 0);
            var internalNode = result as InternalNode;
            if (internalNode != null)
            {
                for (int i = 0; i < internalNode.KeyPointers.Length; i++)
                {
                    var keyPtr = internalNode.KeyPointers[i];
                    internalNode.Nodes[i] = ReadTree(keyPtr.BlockNumber, (byte)(level - 1));
                }
            }
            return result;
        }

        private void CheckStriping(BlockGroupFlag flags)
        {
            if ((flags & BlockGroupFlag.Raid0) == BlockGroupFlag.Raid0)
                throw new IOException("Raid0 not supported");
            if ((flags & BlockGroupFlag.Raid10) == BlockGroupFlag.Raid0)
                throw new IOException("Raid10 not supported");
            if ((flags & BlockGroupFlag.Raid5) == BlockGroupFlag.Raid0)
                throw new IOException("Raid5 not supported");
            if ((flags & BlockGroupFlag.Raid6) == BlockGroupFlag.Raid0)
                throw new IOException("Raid6 not supported");
        }

        internal BaseItem FindKey(ReservedObjectId objectId, ItemType type)
        {
            return FindKey((ulong)objectId, type);
        }

        internal BaseItem FindKey(ulong objectId, ItemType type)
        {
            var key = new Key(objectId,type);
            return FindKey(key);
        }

        internal BaseItem FindKey(Key key)
        {
            switch (key.ItemType)
            {
                case ItemType.RootItem:
                    return RootTreeRoot.FindFirst(key);
                case ItemType.DirItem:
                    return RootTreeRoot.FindFirst(key);
                default:
                    throw new NotImplementedException();
            }
        }

        internal IEnumerable<BaseItem> FindKey(ulong treeId, Key key)
        {
            var tree = GetFsTree(treeId);
            switch (key.ItemType)
            {
                case ItemType.DirItem:
                    return tree.Find(key);
                default:
                    throw new NotImplementedException();
            }
        }

        internal IEnumerable<T> FindKey<T>(ulong treeId, Key key) where T:BaseItem
        {
            var tree = GetFsTree(treeId);
            switch (key.ItemType)
            {
                case ItemType.DirItem:
                case ItemType.ExtentData:
                    return tree.Find<T>(key);
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
