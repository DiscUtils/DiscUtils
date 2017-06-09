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
using DiscUtils.Internal;

namespace DiscUtils.Btrfs.Base.Items
{
    /// <summary>
    /// The contents of a file
    /// </summary>
    internal class ExtentData : BaseItem
    {
        /// <summary>
        ///  generation 
        /// </summary>
        public ulong Generation { get; private set; }

        /// <summary>
        ///  (n) size of decoded extent 
        /// </summary>
        public ulong DecodedSize { get; private set; }

        /// <summary>
        ///  compression (0=none, 1=zlib, 2=LZO) 
        /// </summary>
        public ExtentDataCompression Compression { get; private set; }

        /// <summary>
        ///  encryption (0=none) 
        /// </summary>
        public bool Encryption { get; private set; }

        /// <summary>
        ///  type (0=inline, 1=regular, 2=prealloc) 
        /// </summary>
        public ExtentDataType Type { get; private set; }

        /// <summary>
        /// If the extent is inline, the bytes are the data bytes (n bytes in case no compression/encryption/other encoding is used)
        /// </summary>
        public byte[] InlineData { get; private set; }

        /// <summary>
        ///  (ea) logical address of extent. If this is zero, the extent is sparse and consists of all zeroes. 
        /// </summary>
        public ulong ExtentAddress { get; private set; }

        /// <summary>
        ///  (es) size of extent 
        /// </summary>
        public ulong ExtentSize { get; private set; }

        /// <summary>
        ///  (o) offset within the extent 
        /// </summary>
        public ulong ExtentOffset { get; private set; }

        /// <summary>
        ///  (s) logical number of bytes in file 
        /// </summary>
        public ulong LogicalSize { get; private set; }

        public override int Size
        {
            get { return Type == ExtentDataType.Inline ? InlineData.Length + 0x15 : 0x35; }
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            Generation = Utilities.ToUInt64LittleEndian(buffer, offset);
            DecodedSize = Utilities.ToUInt64LittleEndian(buffer, offset+0x8);
            Compression = (ExtentDataCompression)buffer[offset + 0x10];
            Encryption = buffer[offset + 0x10] != 0;
            //12 	2 	UINT 	other encoding (0=none)
            Type = (ExtentDataType)buffer[offset + 0x14];

            if (Type == ExtentDataType.Inline)
            {
                InlineData = Utilities.ToByteArray(buffer, offset + 0x15, buffer.Length - (offset + 0x15));
            }
            else
            {
                ExtentAddress = Utilities.ToUInt64LittleEndian(buffer, offset + 0x15);
                ExtentSize = Utilities.ToUInt64LittleEndian(buffer, offset + 0x1d);
                ExtentOffset = Utilities.ToUInt64LittleEndian(buffer, offset + 0x25);
                LogicalSize = Utilities.ToUInt64LittleEndian(buffer, offset + 0x2d);
            }

            return Size;
        }
    }
}
