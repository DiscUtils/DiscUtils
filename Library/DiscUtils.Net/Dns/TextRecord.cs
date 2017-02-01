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
using System.Collections.Generic;
using System.Text;

namespace DiscUtils.Net.Dns
{
    /// <summary>
    /// Represents a DNS TXT record.
    /// </summary>
    public sealed class TextRecord : ResourceRecord
    {
        internal TextRecord(string name, RecordType type, RecordClass rClass, DateTime expiry, PacketReader reader)
            : base(name, type, rClass, expiry)
        {
            Values = new Dictionary<string, byte[]>();

            ushort dataLen = reader.ReadUShort();
            int pos = reader.Position;

            while (reader.Position < pos + dataLen)
            {
                int valueLen = reader.ReadByte();
                byte[] valueBinary = reader.ReadBytes(valueLen);

                StoreValue(valueBinary);
            }
        }

        /// <summary>
        /// Gets the values encoded in this record.
        /// </summary>
        /// <remarks>For data fidelity, the data is returned in byte form - typically
        /// the encoded data is actually ASCII or UTF-8.</remarks>
        public Dictionary<string, byte[]> Values { get; }

        private void StoreValue(byte[] value)
        {
            int i = 0;
            while (i < value.Length && value[i] != '=')
            {
                ++i;
            }

            if (i < value.Length)
            {
                byte[] data = new byte[value.Length - (i + 1)];
                Array.Copy(value, i + 1, data, 0, data.Length);
                Values[Encoding.ASCII.GetString(value, 0, i)] = data;
            }
            else
            {
                Values[Encoding.ASCII.GetString(value)] = null;
            }
        }
    }
}