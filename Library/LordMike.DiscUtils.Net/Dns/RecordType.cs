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

namespace DiscUtils.Net.Dns
{
    /// <summary>
    /// Enumeration of the known DNS record types.
    /// </summary>
    public enum RecordType
    {
        /// <summary>
        ///  No record type defined.
        /// </summary>
        None = 0,

        /// <summary>
        /// DNS A record.
        /// </summary>
        Address = 1,

        /// <summary>
        /// DNS NS record.
        /// </summary>
        NameServer = 2,

        /// <summary>
        /// DNS MD record.
        /// </summary>
        MailDestination = 3,

        /// <summary>
        /// DNS MF record.
        /// </summary>
        MailForwarder = 4,

        /// <summary>
        /// DNS CNAME record.
        /// </summary>
        CanonicalName = 5,

        /// <summary>
        /// DNS SOA record.
        /// </summary>
        StartOfAuthority = 6,

        /// <summary>
        /// DNS MB record.
        /// </summary>
        Mailbox = 7,

        /// <summary>
        /// DNS MG record.
        /// </summary>
        MailGroup = 8,

        /// <summary>
        /// DNS MR record.
        /// </summary>
        MailRename = 9,

        /// <summary>
        /// DNS NULL record.
        /// </summary>
        Null = 10,

        /// <summary>
        /// DNS WKS record.
        /// </summary>
        WellKnownService = 11,

        /// <summary>
        /// DNS PTR record.
        /// </summary>
        Pointer = 12,

        /// <summary>
        /// DNS HINFO record.
        /// </summary>
        HostInformation = 13,

        /// <summary>
        /// DNS MINFO record.
        /// </summary>
        MailboxInformation = 14,

        /// <summary>
        /// DNS MX record.
        /// </summary>
        MailExchange = 15,

        /// <summary>
        /// DNS TXT record.
        /// </summary>
        Text = 16,

        /// <summary>
        /// DNS RP record.
        /// </summary>
        ResponsiblePerson = 17,

        /// <summary>
        /// DNS AAAA record.
        /// </summary>
        IP6Address = 28,

        /// <summary>
        /// DNS SRV record.
        /// </summary>
        Service = 33,

        /// <summary>
        /// DNS AXFR record.
        /// </summary>
        ZoneTransfer = 252,

        /// <summary>
        /// DNS MAILB record.
        /// </summary>
        MailboxRecords = 253,

        /// <summary>
        /// DNS MAILA record.
        /// </summary>
        MailAgentRecords = 254,

        /// <summary>
        /// Wildcard matching all records (*).
        /// </summary>
        All = 255
    }
}