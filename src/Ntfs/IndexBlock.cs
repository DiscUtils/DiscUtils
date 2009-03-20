//
// Copyright (c) 2008-2009, Kenneth Bell
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

namespace DiscUtils.Ntfs
{
    internal class IndexBlock<K, D> : FixupRecordBase
        where K : IByteArraySerializable, new()
        where D : IByteArraySerializable, new()
    {
        public ulong LogSequenceNumber;
        public ulong IndexBlockVcn; // Virtual Cluster Number (maybe in sectors sometimes...?)

        public IndexNode<K, D> Node;

        private Index<K, D> _index;
        private IndexNode<K, D> _parentNode;
        private IndexEntry<K, D> _parentEntry;
        private long _streamPosition;

        public IndexBlock(Index<K, D> index, IndexNode<K, D> parentNode, IndexEntry<K, D> parentEntry, BiosParameterBlock bpb)
            : base(bpb.BytesPerSector)
        {
            _index = index;
            _parentNode = parentNode;
            _parentEntry = parentEntry;

            Stream stream = index.AllocationStream;
            _streamPosition = _parentEntry.ChildrenVirtualCluster * bpb.BytesPerSector * bpb.SectorsPerCluster;
            stream.Position = _streamPosition;
            byte[] buffer = Utilities.ReadFully(stream, (int)index.IndexBufferSize);
            FromBytes(buffer, 0);
        }

        protected override void Read(byte[] buffer, int offset)
        {
            // Skip FixupRecord fields...
            LogSequenceNumber = Utilities.ToUInt64LittleEndian(buffer, offset + 0x08);
            IndexBlockVcn = Utilities.ToUInt64LittleEndian(buffer, offset + 0x10);
            Node = new IndexNode<K, D>(Store, _index, _parentNode, buffer, offset + 0x18);
        }

        protected override ushort Write(byte[] buffer, int offset, ushort updateSeqSize)
        {
            Utilities.WriteBytesLittleEndian(LogSequenceNumber, buffer, offset + 0x08);
            Utilities.WriteBytesLittleEndian(IndexBlockVcn, buffer, offset + 0x10);
            return (ushort)(0x18 + Node.WriteTo(buffer, offset + 0x18, updateSeqSize));
        }

        protected override int CalcSize(int updateSeqSize)
        {
            throw new NotImplementedException();
        }

        public void Store(IndexNode<K, D> node)
        {
            Node = node;
            WriteToDisk();
        }

        internal void WriteToDisk()
        {
            byte[] buffer = new byte[_index.IndexBufferSize];
            ToBytes(buffer, 0);

            Stream stream = _index.AllocationStream;
            stream.Position = _streamPosition;
            stream.Write(buffer, 0, buffer.Length);
        }
    }
}
