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
    using System;
    using System.IO;
    using System.Collections.Generic;
    using DiscUtils.Streams;

    internal class BlockDirectory : IByteArraySerializable
    {
        public const uint HeaderMagic = 0x58443242;

        public const uint HeaderMagicV5 = 0x58444233;

        public uint Magic { get; private set; }

        public uint LeafCount { get; private set; }

        public uint LeafStale { get; private set; }

        public BlockDirectoryDataFree[] BestFree { get; private set; }

        public BlockDirectoryData[] Entries { get; private set; }

        public SuperBlock SuperBlock { get; private set; }

        public bool sb_ok = false;

        public int Size
        {
            get { return 16 + 3*32; }
        }
        public BlockDirectory(SuperBlock sb)
        {
            SuperBlock = sb;
            sb_ok = true;
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            if (sb_ok == false)
                throw new IOException("not initial SuperBlock");

            Magic = EndianUtilities.ToUInt32BigEndian(buffer, offset);
            BestFree = new BlockDirectoryDataFree[3];
            if (Magic == BlockDirectory.HeaderMagicV5)
            {
                offset += 48;//xfs_dir3_blk_hdr
            }
            else
            {
                offset += 0x4;
            }

            for (int i = 0; i < BestFree.Length; i++)
            {
                var free = new BlockDirectoryDataFree();
                offset += free.ReadFrom(buffer, offset);
                BestFree[i] = free;
            }

            if (Magic == BlockDirectory.HeaderMagicV5)
            {
                offset += 4;//__be32 xfs_dir3_data_hdr.pad
            }

            LeafStale = EndianUtilities.ToUInt32BigEndian(buffer, buffer.Length - 0x4);
            LeafCount = EndianUtilities.ToUInt32BigEndian(buffer, buffer.Length - 0x8);
            var entries = new List<BlockDirectoryData>();
            var eof = buffer.Length - 0x8 - LeafCount*0x8;
            while (offset < eof)
            {
                BlockDirectoryData entry;
                if (buffer[offset] == 0xff && buffer[offset + 0x1] == 0xff)
                {
                    //unused
                    entry = new BlockDirectoryDataUnused(SuperBlock);
                }
                else
                {
                    entry = new BlockDirectoryDataEntry(SuperBlock);
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
