//
// Copyright (c) 2016, Bianco Veigel
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

namespace DiscUtils.Xfs
{
    using DiscUtils.Internal;
    using System;

    internal class BTreeInodeNode : BtreeHeader
    {
        public uint[] Keys { get; private set; }

        public uint[] Pointer { get; private set; }

        public Dictionary<uint, BtreeHeader> Children { get; private set; }

        public override int Size
        {
            get { return base.Size + (NumberOfRecords * 0x8); }
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            base.ReadFrom(buffer, offset);
            if (base.Size == 0x38)
            {
                ReadFromVersion5(buffer, offset);
            }
            offset += base.Size;
            if (Level == 0)
                throw new IOException("invalid B+tree level - expected 0");
            Keys = new uint[NumberOfRecords];
            Pointer = new uint[NumberOfRecords];
            for (int i = 0; i < NumberOfRecords; i++)
            {
                Keys[i] = Utilities.ToUInt32BigEndian(buffer, offset);
            }
            for (int i = 0; i < NumberOfRecords; i++)
            {
                Pointer[i] = Utilities.ToUInt32BigEndian(buffer, offset);
            }
            return Size;
        }

        public override void LoadBtree(AllocationGroup ag)
        {
            Children = new Dictionary<uint,BtreeHeader>(NumberOfRecords);
            for (int i = 0; i < NumberOfRecords; i++)
            {
                BtreeHeader child;
                if (Level == 1)
                {
                    child = new BTreeInodeLeave();
                }
                else
                {
                    child = new BTreeInodeNode();
                }
                var data = ag.Context.RawStream;
                data.Position = (Pointer[i] * ag.Context.SuperBlock.Blocksize) + ag.Offset;
                var buffer = Utilities.ReadFully(data, (int)ag.Context.SuperBlock.Blocksize);
                child.InitSize(ag.Context.SuperBlock.SbVersion);
                child.ReadFrom(buffer, 0);
                child.LoadBtree(ag);
                Children.Add(Keys[i], child);
            }
        }
    }
}
