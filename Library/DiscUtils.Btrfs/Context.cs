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
using System.IO;
using DiscUtils.Btrfs.Base;
using DiscUtils.Btrfs.Base.Items;
using DiscUtils.Vfs;

namespace DiscUtils.Btrfs
{
    internal class Context : VfsContext
    {
        public Stream RawStream { get; set; }

        public SuperBlock SuperBlock { get; set; }

        internal NodeHeader ChunkTreeRoot { get; set; }

        internal NodeHeader RootTreeRoot { get; set; }

        internal ulong MapToPhysical(ulong logical)
        {
            if (ChunkTreeRoot != null)
            {
                var node = (LeafNode)ChunkTreeRoot;
                for (int i = 0; i < node.Items.Length; i++)
                {
                    var chunkKey = node.Items[i].Key;
                    if (chunkKey.ItemType != ItemType.ChunkItem) continue;
                    if (chunkKey.Offset > logical) continue;
                    var chunk = (ChunkItem)node.NodeData[i];
                    if (chunkKey.Offset + chunk.ChunkSize < logical) continue;
                    CheckStriping(chunk.Type);
                    if (chunk.StripeCount < 1)
                        throw new IOException("Invalid stripe count in ChunkItem");
                    var stripe = chunk.Stripes[0];
                    return stripe.Offset + (logical - chunkKey.Offset);
                }
            }
            foreach (Tuple<Key, ChunkItem> tuple in SuperBlock.SystemChunkArray)
            {
                if (tuple.Item1.ItemType != ItemType.ChunkItem) continue;
                if (tuple.Item1.Offset > logical) continue;
                if (tuple.Item1.Offset  + tuple.Item2.ChunkSize < logical) continue;

                CheckStriping(tuple.Item2.Type);
                if (tuple.Item2.StripeCount <1)
                    throw new IOException("Invalid stripe count in ChunkItem");
                var stripe = tuple.Item2.Stripes[0];
                return stripe.Offset;
            }
            throw new IOException("no matching ChunkItem found");
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
    }
}
