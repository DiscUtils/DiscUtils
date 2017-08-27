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

namespace DiscUtils.Btrfs.Base
{
    internal class InternalNode:NodeHeader
    {
        /// <summary>
        /// key pointers
        /// </summary>
        public KeyPointer[] KeyPointers { get; private set; }

        /// <summary>
        /// data at <see cref="KeyPointers"/>
        /// </summary>
        public NodeHeader[] Nodes { get; private set; }

        public override int Size
        {
            get { return (int)(base.Size + ItemCount*KeyPointer.Length); }
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            offset += base.ReadFrom(buffer, offset);
            KeyPointers = new KeyPointer[ItemCount];
            if (KeyPointers.Length == 0) throw new IOException("invalid InteralNode without KeyPointers");
            for (int i = 0; i < ItemCount; i++)
            {
                KeyPointers[i] = new KeyPointer();
                offset += KeyPointers[i].ReadFrom(buffer, offset);
            }
            Nodes = new NodeHeader[ItemCount];
            return Size;
        }

        public override IEnumerable<BaseItem> Find(Key key, Context context)
        {
            if (KeyPointers[0].Key.ObjectId > key.ObjectId)
                yield break;
            var i = 1;
            while (i < KeyPointers.Length && KeyPointers[i].Key.ObjectId < key.ObjectId)
            {
                i++;
            }
            for (int j = i-1; j < KeyPointers.Length; j++)
            {
                var keyPtr = KeyPointers[j];
                if (keyPtr.Key.ObjectId > key.ObjectId)
                    yield break;
                if (Nodes[j] == null)
                    Nodes[j] = context.ReadTree(keyPtr.BlockNumber, Level);
                foreach (var item in Nodes[j].Find(key, context))
                {
                    yield return item;
                }
            }
        }
    }
}
