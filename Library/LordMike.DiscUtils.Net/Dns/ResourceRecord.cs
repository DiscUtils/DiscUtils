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

namespace DiscUtils.Net.Dns
{
    /// <summary>
    /// Base class for all resource records (DNS RRs).
    /// </summary>
    public class ResourceRecord
    {
        internal ResourceRecord(string name, RecordType type, RecordClass rClass, DateTime expiry)
        {
            Name = name;
            RecordType = type;
            Class = rClass;
            Expiry = expiry;
        }

        /// <summary>
        /// Gets the class of record.
        /// </summary>
        public RecordClass Class { get; }

        /// <summary>
        /// Gets the expiry time of the record.
        /// </summary>
        public DateTime Expiry { get; }

        /// <summary>
        /// Gets the name of the resource (domain).
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the type of record.
        /// </summary>
        public RecordType RecordType { get; }

        internal static ResourceRecord ReadFrom(PacketReader reader)
        {
            string name = reader.ReadName();
            RecordType type = (RecordType)reader.ReadUShort();
            RecordClass rClass = (RecordClass)reader.ReadUShort();
            DateTime expiry = DateTime.UtcNow + TimeSpan.FromSeconds(reader.ReadInt());

            switch (type)
            {
                case RecordType.Pointer:
                    return new PointerRecord(name, type, rClass, expiry, reader);

                case RecordType.CanonicalName:
                    return new CanonicalNameRecord(name, type, rClass, expiry, reader);

                case RecordType.Address:
                    return new IP4AddressRecord(name, type, rClass, expiry, reader);

                case RecordType.Text:
                    return new TextRecord(name, type, rClass, expiry, reader);

                case RecordType.Service:
                    return new ServiceRecord(name, type, rClass, expiry, reader);

                default:
                    int len = reader.ReadUShort();
                    reader.Position += len;
                    return new ResourceRecord(name, type, rClass, expiry);
            }
        }
    }
}