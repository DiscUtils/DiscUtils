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

        public ParentLocator()
        {

        }

        public ParentLocator(String parentUid, String relativePath, String absolutePath)
        {
            Entries.Add("parent_linkage", parentUid);
            Entries.Add("relative_path", relativePath);
            Entries.Add("absolute_win32_path", @"\\?\" + absolutePath);
        }

        public int Size
        {
            get
            {
                int size = 20 + Entries.Count * 12;
                foreach (var entry in Entries)
                {
                    size += Encoding.Unicode.GetByteCount(entry.Key);
                    size += Encoding.Unicode.GetByteCount(entry.Value);
                }

                return size;
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
            Count = (ushort)Entries.Count;

            EndianUtilities.WriteBytesLittleEndian(LocatorType, buffer, offset + 0);
            EndianUtilities.WriteBytesLittleEndian(Reserved, buffer, offset + 16);
            EndianUtilities.WriteBytesLittleEndian(Count, buffer, offset + 18);

            int entryOffset = 0;
            int item = 0;
            foreach(var entry in Entries)
            {
                byte[] keyData = Encoding.Unicode.GetBytes(entry.Key);
                byte[] valueData = Encoding.Unicode.GetBytes(entry.Value);

                Array.Copy(keyData, 0, buffer, offset + 20 + Count * 12 + entryOffset, keyData.Length);
                EndianUtilities.WriteBytesLittleEndian((offset + 20 + Count * 12 + entryOffset), buffer, offset + 20 + item * 12);
                EndianUtilities.WriteBytesLittleEndian((ushort)keyData.Length, buffer, offset + 20 + item * 12 + 8);
                entryOffset += keyData.Length;

                Array.Copy(valueData, 0, buffer, offset + 20 + Count * 12 + entryOffset, valueData.Length);
                EndianUtilities.WriteBytesLittleEndian((offset + 20 + Count * 12 + entryOffset), buffer, offset + 20 + item * 12 + 4);
                EndianUtilities.WriteBytesLittleEndian((ushort)valueData.Length, buffer, offset + 20 + item * 12 + 10);
                entryOffset += valueData.Length;

                ++item;
            }

            
        }
    }
}