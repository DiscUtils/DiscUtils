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

using System.Text;
using DiscUtils.Streams;

namespace DiscUtils.Btrfs.Base.Items
{
    /// <summary>
    /// From an inode to a name in a directory
    /// </summary>
    internal class DirItem : BaseItem
    {
        public DirItem(Key key) : base(key) { }

        /// <summary>
        /// Key for the <see cref="InodeItem"/> or <see cref="RootItem"/> associated with this entry.
        /// Unused and zeroed out when the entry describes an extended attribute.
        /// </summary>
        public Key ChildLocation { get; private set; }

        /// <summary>
        /// transid
        /// </summary>
        public ulong TransId { get; private set; }

        /// <summary>
        /// (m)
        /// </summary>
        public ushort DataLength { get; private set; }

        /// <summary>
        /// (n)
        /// </summary>
        public ushort NameLength { get; private set; }

        /// <summary>
        /// type of child
        /// </summary>
        public DirItemChildType ChildType { get; private set; }

        /// <summary>
        /// name of item in directory 
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// data of item in directory (empty for normal directory items)
        /// </summary>
        public byte[] Data { get; private set; }

        public override int Size
        {
            get { return 0x1e+NameLength+DataLength; }
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            ChildLocation = EndianUtilities.ToStruct<Key>(buffer, offset);
            TransId = EndianUtilities.ToUInt64LittleEndian(buffer, offset+0x11);
            DataLength = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0x19);
            NameLength = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0x1b);
            ChildType = (DirItemChildType)buffer[offset + 0x1d];
            Name = Encoding.UTF8.GetString(buffer, offset + 0x1e, NameLength);
            Data = EndianUtilities.ToByteArray(buffer, offset + 0x1e + NameLength, DataLength);
            return Size;
        }
    }
}
