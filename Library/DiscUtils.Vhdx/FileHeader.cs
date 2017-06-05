//
// Copyright (c) 2008-2012, Kenneth Bell
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
using System.Text;
using DiscUtils.Streams;

namespace DiscUtils.Vhdx
{
    internal sealed class FileHeader : IByteArraySerializable
    {
        public const ulong VhdxSignature = 0x656C696678646876;
        public string Creator;

        public ulong Signature = VhdxSignature;

        public bool IsValid
        {
            get { return Signature == VhdxSignature; }
        }

        public int Size
        {
            get { return (int)(64 * Sizes.OneKiB); }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            Signature = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0);
            Creator = Encoding.Unicode.GetString(buffer, offset + 8, 256 * 2).TrimEnd('\0');

            return Size;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            Array.Clear(buffer, offset, Size);
            EndianUtilities.WriteBytesLittleEndian(Signature, buffer, offset + 0);
            Encoding.Unicode.GetBytes(Creator, 0, Creator.Length, buffer, offset + 8);
        }
    }
}