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

namespace DiscUtils.Xfs
{
    using DiscUtils.Streams;
    using System;
    using System.Collections.Generic;

    internal class LeafDirectory : IByteArraySerializable
    {
        private readonly Context _context;
        public const uint HeaderMagic = 0x58443244;

        public const ulong LeafOffset = (1* (1UL << (32 + 3)));

        public uint Magic { get; protected set; }
        
        public BlockDirectoryDataFree[] BestFree { get; private set; }

        public BlockDirectoryData[] Entries { get; private set; }

        public virtual int Size
        {
            get { return 16 + 3*32; }
        }

        protected virtual int ReadHeader(byte[] buffer, int offset)
        {
            Magic = EndianUtilities.ToUInt32BigEndian(buffer, offset);
            return 0x4;
        }

        protected virtual int HeaderPadding
        {
            get { return 0; }
        }

        public LeafDirectory(Context context)
        {
            _context = context;
        }

        public virtual bool HasValidMagic
        {
            get { return Magic == HeaderMagic; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            offset += ReadHeader(buffer, offset);
            BestFree = new BlockDirectoryDataFree[3];
            for (int i = 0; i < BestFree.Length; i++)
            {
                var free = new BlockDirectoryDataFree();
                offset += free.ReadFrom(buffer, offset);
                BestFree[i] = free;
            }
            offset += HeaderPadding;
            
            var entries = new List<BlockDirectoryData>();
            var eof = buffer.Length;
            while (offset < eof)
            {
                BlockDirectoryData entry;
                if (buffer[offset] == 0xff && buffer[offset + 0x1] == 0xff)
                {
                    //unused
                    entry = new BlockDirectoryDataUnused();
                }
                else
                {
                    entry = new BlockDirectoryDataEntry(_context);
                }
                offset += entry.ReadFrom(buffer, offset);
                entries.Add(entry);
            }
            Entries = entries.ToArray();
            return buffer.Length - offset;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }
    }
}
