//
// Copyright (c) 2008-2010, Kenneth Bell
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
    internal class IndexBlock : FixupRecordBase
    {
        /// <summary>
        /// Size of meta-data placed at start of a block.
        /// </summary>
        private const int FieldSize = 0x18;

        public ulong LogSequenceNumber;
        public ulong IndexBlockVcn; // Virtual Cluster Number (maybe in sectors sometimes...?)

        private IndexNode _node;

        private Index _index;
        private IndexNode _parentNode;
        private long _streamPosition;

        public IndexBlock(Index index, IndexNode parentNode, IndexEntry parentEntry, BiosParameterBlock bpb)
            : base("INDX", bpb.BytesPerSector)
        {
            _index = index;
            _parentNode = parentNode;

            Stream stream = index.AllocationStream;
            _streamPosition = parentEntry.ChildrenVirtualCluster * bpb.BytesPerSector * bpb.SectorsPerCluster;
            stream.Position = _streamPosition;
            byte[] buffer = Utilities.ReadFully(stream, (int)index.IndexBufferSize);
            FromBytes(buffer, 0);
        }

        private IndexBlock(Index index, IndexNode parentNode, long vcn, BiosParameterBlock bpb)
            : base("INDX", bpb.BytesPerSector, bpb.IndexBufferSize)
        {
            _index = index;
            _parentNode = parentNode;

            IndexBlockVcn = (ulong)vcn;

            _streamPosition = vcn * bpb.BytesPerSector * bpb.SectorsPerCluster;

            _node = new IndexNode(WriteToDisk, UpdateSequenceSize, _index, _parentNode, (uint)bpb.IndexBufferSize - FieldSize);

            WriteToDisk();
        }

        public IndexNode Node
        {
            get { return _node; }
        }

        protected override void Read(byte[] buffer, int offset)
        {
            // Skip FixupRecord fields...
            LogSequenceNumber = Utilities.ToUInt64LittleEndian(buffer, offset + 0x08);
            IndexBlockVcn = Utilities.ToUInt64LittleEndian(buffer, offset + 0x10);
            _node = new IndexNode(WriteToDisk, UpdateSequenceSize, _index, _parentNode, buffer, offset + FieldSize);
        }

        protected override ushort Write(byte[] buffer, int offset)
        {
            Utilities.WriteBytesLittleEndian(LogSequenceNumber, buffer, offset + 0x08);
            Utilities.WriteBytesLittleEndian(IndexBlockVcn, buffer, offset + 0x10);
            return (ushort)(FieldSize + Node.WriteTo(buffer, offset + FieldSize));
        }

        protected override int CalcSize()
        {
            throw new NotImplementedException();
        }

        internal void WriteToDisk()
        {
            byte[] buffer = new byte[_index.IndexBufferSize];
            ToBytes(buffer, 0);

            Stream stream = _index.AllocationStream;
            stream.Position = _streamPosition;
            stream.Write(buffer, 0, buffer.Length);
            stream.Flush();
        }

        internal static IndexBlock Initialize(Index index, IndexNode parentNode, IndexEntry parentEntry, BiosParameterBlock bpb)
        {
            return new IndexBlock(index, parentNode, parentEntry.ChildrenVirtualCluster, bpb);
        }
    }
}
