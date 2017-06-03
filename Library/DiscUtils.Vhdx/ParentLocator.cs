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
using System.Collections.Generic;
using System.IO;
using System.Text;
using DiscUtils.Streams;

namespace DiscUtils.Vhdx
{
    internal sealed class ParentLocator : IByteArraySerializable
    {
        private static readonly Guid LocatorTypeGuid = new Guid("B04AEFB7-D19E-4A81-B789-25B8E9445913");

        public ushort Count;
        public Guid LocatorType = LocatorTypeGuid;
        public ushort Reserved = 0;

        public Dictionary<string, string> Entries { get; private set; } = new Dictionary<string, string>();

        public int Size
        {
            get
            {
                if (Entries.Count != 0)
                {
                    throw new NotImplementedException();
                }

                return 20;
            }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            LocatorType = EndianUtilities.ToGuidLittleEndian(buffer, offset + 0);
            if (LocatorType != LocatorTypeGuid)
            {
                throw new IOException("Unrecognized Parent Locator type: " + LocatorType);
            }

            Entries = new Dictionary<string, string>();

            Count = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 18);
            for (ushort i = 0; i < Count; ++i)
            {
                int kvOffset = offset + 20 + i * 12;
                int keyOffset = EndianUtilities.ToInt32LittleEndian(buffer, kvOffset + 0);
                int valueOffset = EndianUtilities.ToInt32LittleEndian(buffer, kvOffset + 4);
                int keyLength = EndianUtilities.ToUInt16LittleEndian(buffer, kvOffset + 8);
                int valueLength = EndianUtilities.ToUInt16LittleEndian(buffer, kvOffset + 10);

                string key = Encoding.Unicode.GetString(buffer, keyOffset, keyLength);
                string value = Encoding.Unicode.GetString(buffer, valueOffset, valueLength);

                Entries[key] = value;
            }

            return 0;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            if (Entries.Count != 0)
            {
                throw new NotImplementedException();
            }

            Count = (ushort)Entries.Count;

            EndianUtilities.WriteBytesLittleEndian(LocatorType, buffer, offset + 0);
            EndianUtilities.WriteBytesLittleEndian(Reserved, buffer, offset + 16);
            EndianUtilities.WriteBytesLittleEndian(Count, buffer, offset + 18);
        }
    }
}