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
using DiscUtils.Streams;

namespace DiscUtils.Btrfs.Base
{
    internal class Key : IByteArraySerializable
    {
        public static readonly int Length = 0x11;

        public Key()
        {
        }

        public Key(ulong objectId, ItemType type, ulong offset) : this()
        {
            ObjectId = objectId;
            ItemType = type;
            Offset = offset;
        }

        public Key(ulong objectId, ItemType type) : this(objectId, type, 0UL)
        {
        }

        public Key(ReservedObjectId objectId, ItemType type) : this((ulong)objectId, type)
        {
        }

        /// <summary>
        /// Object ID. Each tree has its own set of Object IDs.
        /// </summary>
        public ulong ObjectId { get; private set; }

        public ReservedObjectId ReservedObjectId
        {
            get { return (ReservedObjectId)ObjectId; }
        }

        /// <summary>
        /// Item type.
        /// </summary>
        public ItemType ItemType { get; private set; }

        /// <summary>
        /// Offset. The meaning depends on the item type. 
        /// </summary>
        public ulong Offset { get; private set; }

        public int Size
        {
            get { return Length; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            ObjectId = EndianUtilities.ToUInt64LittleEndian(buffer, offset);
            ItemType = (ItemType)buffer[offset +0x8];
            Offset = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x9);
            return Size;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            if (Enum.IsDefined(typeof(ReservedObjectId), ObjectId))
                return $"{ReservedObjectId}|{ItemType}|{Offset}";
            return $"{ObjectId}|{ItemType}|{Offset}";
        }
    }
}
