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
    /// Base class for DNS clients.
    /// </summary>
    public abstract class DnsClient
    {
        /// <summary>
        /// Flushes any cached DNS records.
        /// </summary>
        public abstract void FlushCache();

        /// <summary>
        /// Looks up a record in DNS.
        /// </summary>
        /// <param name="name">The name to lookup.</param>
        /// <param name="type">The type of record requested.</param>
        /// <returns>The records returned by the DNS server, if any.</returns>
        public abstract ResourceRecord[] Lookup(string name, RecordType type);

        internal static string NormalizeDomainName(string name)
        {
            string[] labels = name.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

            return string.Join(".", labels) + ".";
        }
    }
}