//
// Copyright (c) 2008-2011, Kenneth Bell
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

namespace DiscUtils.Ntfs
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    internal class IndexBlock : FixupRecordBase
    {
        /// <summary>
        /// Size of meta-data placed at start of a block.
        /// </summary>
        private const int FieldSize = 0x18;

        private ulong _logSequenceNumber;
        private ulong _indexBlockVcn; // Virtual Cluster Number (maybe in sectors sometimes...?)

        private IndexNode _node;

        private Index _index;
        private bool _isRoot;
        private long _streamPosition;

        public IndexBlock(Index index, bool isRoot, IndexEntry parentEntry, BiosParameterBlock bpb)
            : base("INDX", bpb.BytesPerSector)
        {
            _index = index;
            _isRoot = isRoot;

            Stream stream = index.AllocationStream;
            _streamPosition = index.IndexBlockVcnToPosition(parentEntry.ChildrenVirtualCluster);
            stream.Position = _streamPosition;
            byte[] buffer = Utilities.ReadFully(stream, (int)index.IndexBufferSize);
            FromBytes(buffer, 0);
        }

        private IndexBlock(Index index, bool isRoot, long vcn, BiosParameterBlock bpb)
            : base("INDX", bpb.BytesPerSector, bpb.IndexBufferSize)
        {
            _index = index;
            _isRoot = isRoot;

            _indexBlockVcn = (ulong)vcn;

            _streamPosition = vcn * bpb.BytesPerSector * bpb.SectorsPerCluster;

            _node = new IndexNode(WriteToDisk, UpdateSequenceSize, _index, isRoot, (uint)bpb.IndexBufferSize - FieldSize);

            WriteToDisk();
        }

        public IndexNode Node
        {
            get { return _node; }
        }

        internal static IndexBlock Initialize(Index index, bool isRoot, IndexEntry parentEntry, BiosParameterBlock bpb)
        {
            return new IndexBlock(index, isRoot, parentEntry.ChildrenVirtualCluster, bpb);
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

        protected override void Read(byte[] buffer, int offset)
        {
            // Skip FixupRecord fields...
            _logSequenceNumber = Utilities.ToUInt64LittleEndian(buffer, offset + 0x08);
            _indexBlockVcn = Utilities.ToUInt64LittleEndian(buffer, offset + 0x10);
            _node = new IndexNode(WriteToDisk, UpdateSequenceSize, _index, _isRoot, buffer, offset + FieldSize);
        }

        protected override ushort Write(byte[] buffer, int offset)
        {
            Utilities.WriteBytesLittleEndian(_logSequenceNumber, buffer, offset + 0x08);
            Utilities.WriteBytesLittleEndian(_indexBlockVcn, buffer, offset + 0x10);
            return (ushort)(FieldSize + Node.WriteTo(buffer, offset + FieldSize));
        }

        protected override int CalcSize()
        {
            throw new NotImplementedException();
        }
    }
}
