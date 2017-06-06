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

using System;
using System.Globalization;
using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.Ntfs
{
    internal sealed class ReparsePointRecord : IByteArraySerializable, IDiagnosticTraceable
    {
        public byte[] Content;
        public uint Tag;

        public int Size
        {
            get { return 8 + Content.Length; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            Tag = EndianUtilities.ToUInt32LittleEndian(buffer, offset);
            ushort length = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 4);
            Content = new byte[length];
            Array.Copy(buffer, offset + 8, Content, 0, length);
            return 8 + length;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            EndianUtilities.WriteBytesLittleEndian(Tag, buffer, offset);
            EndianUtilities.WriteBytesLittleEndian((ushort)Content.Length, buffer, offset + 4);
            EndianUtilities.WriteBytesLittleEndian((ushort)0, buffer, offset + 6);
            Array.Copy(Content, 0, buffer, offset + 8, Content.Length);
        }

        public void Dump(TextWriter writer, string linePrefix)
        {
            writer.WriteLine(linePrefix + "                Tag: " + Tag.ToString("x", CultureInfo.InvariantCulture));

            string hex = string.Empty;
            for (int i = 0; i < Math.Min(Content.Length, 32); ++i)
            {
                hex = hex + string.Format(CultureInfo.InvariantCulture, " {0:X2}", Content[i]);
            }

            writer.WriteLine(linePrefix + "               Data:" + hex + (Content.Length > 32 ? "..." : string.Empty));
        }
    }
}