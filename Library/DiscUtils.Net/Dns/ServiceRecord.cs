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
    /// Represents a DNS SRV record.
    /// </summary>
    public sealed class ServiceRecord : ResourceRecord
    {
        private readonly ushort _port;
        private readonly ushort _priority;
        private readonly ushort _weight;

        internal ServiceRecord(string name, RecordType type, RecordClass rClass, DateTime expiry, PacketReader reader)
            : base(name, type, rClass, expiry)
        {
            ushort dataLen = reader.ReadUShort();
            int pos = reader.Position;

            _priority = reader.ReadUShort();
            _weight = reader.ReadUShort();
            _port = reader.ReadUShort();
            Target = reader.ReadName();

            reader.Position = pos + dataLen;
        }

        /// <summary>
        /// Gets the network port at which the service can be accessed.
        /// </summary>
        public int Port
        {
            get { return _port; }
        }

        /// <summary>
        /// Gets the priority associated with this service record (lower value is higher priority).
        /// </summary>
        public int Priority
        {
            get { return _priority; }
        }

        /// <summary>
        /// Gets the DNS name at which the service can be accessed.
        /// </summary>
        public string Target { get; }

        /// <summary>
        /// Gets the relative weight associated with this service record when randomly choosing between records of equal priority.
        /// </summary>
        public int Weight
        {
            get { return _weight; }
        }
    }
}