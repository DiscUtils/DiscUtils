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
    internal class InodeRef : BaseItem
    {
        public InodeRef(Key key) : base(key) { }

        /// <summary>
        ///  index in the directory  
        /// </summary>
        public ulong Index { get; private set; }

        /// <summary>
        /// (n)
        /// </summary>
        public ushort NameLength { get; private set; }

        /// <summary>
        ///  name in the directory 
        /// </summary>
        public string Name { get; private set; }
        
        public override int Size
        {
            get { return 0xa+NameLength; }
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            Index = EndianUtilities.ToUInt64LittleEndian(buffer, offset);
            NameLength = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0x8);
            Name = Encoding.UTF8.GetString(buffer, offset + 0xa, NameLength);
            return Size;
        }
    }
}
