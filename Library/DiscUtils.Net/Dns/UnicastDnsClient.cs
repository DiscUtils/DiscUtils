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
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DiscUtils.Net.Dns
{
    /// <summary>
    /// Implements the (conventional) unicast DNS protocol.
    /// </summary>
    public sealed class UnicastDnsClient : DnsClient
    {
        private ushort _nextTransId;
        private readonly IPEndPoint[] _servers;
        private readonly int maxRetries = 3;

        private readonly int responseTimeout = 2000;

        /// <summary>
        /// Initializes a new instance of the UnicastDnsClient class.
        /// </summary>
        /// <remarks>
        /// This constructor attempts to detect the DNS servers in use by the local
        /// OS, and use those servers.
        /// </remarks>
        public UnicastDnsClient()
            : this(GetDefaultDnsServers()) {}

        /// <summary>
        /// Initializes a new instance of the UnicastDnsClient class, using nominated DNS servers.
        /// </summary>
        /// <param name="servers">The servers to use (non-standard ports may be specified).</param>
        public UnicastDnsClient(params IPEndPoint[] servers)
        {
            _nextTransId = (ushort)new Random().Next();
            _servers = servers;
        }

        /// <summary>
        /// Initializes a new instance of the UnicastDnsClient class, using nominated DNS servers.
        /// </summary>
        /// <param name="servers">The servers to use (the default DNS port, 53, is used).</param>
        public UnicastDnsClient(params IPAddress[] servers)
        {
            _nextTransId = (ushort)new Random().Next();
            _servers = new IPEndPoint[servers.Length];
            for (int i = 0; i < servers.Length; ++i)
            {
                _servers[i] = new IPEndPoint(servers[i], 53);
            }
        }

        /// <summary>
        /// Flushes any cached DNS records.
        /// </summary>
        public override void FlushCache()
        {
            // Nothing to do.
        }

        /// <summary>
        /// Looks up a record in DNS.
        /// </summary>
        /// <param name="name">The name to lookup.</param>
        /// <param name="type">The type of record requested.</param>
        /// <returns>The records returned by the DNS server, if any.</returns>
        public override ResourceRecord[] Lookup(string name, RecordType type)
        {
            ushort transactionId = _nextTransId++;
            string normName = NormalizeDomainName(name);

            using (UdpClient udpClient = new UdpClient(0))
            {
                IAsyncResult result = udpClient.BeginReceive(null, null);

                PacketWriter writer = new PacketWriter(1800);
                Message msg = new Message();
                msg.TransactionId = transactionId;
                msg.Flags = new MessageFlags(false, OpCode.Query, false, false, false, false, ResponseCode.Success);
                msg.Questions.Add(new Question { Name = normName, Type = type, Class = RecordClass.Internet });

                msg.WriteTo(writer);

                byte[] msgBytes = writer.GetBytes();

                foreach (IPEndPoint server in _servers)
                {
                    udpClient.Send(msgBytes, msgBytes.Length, server);
                }

                for (int i = 0; i < maxRetries; ++i)
                {
                    DateTime now = DateTime.UtcNow;
                    while (result.AsyncWaitHandle.WaitOne(Math.Max(responseTimeout - (DateTime.UtcNow - now).Milliseconds, 0)))
                    {
                        try
                        {
                            IPEndPoint sourceEndPoint = null;
                            byte[] packetBytes = udpClient.EndReceive(result, ref sourceEndPoint);
                            PacketReader reader = new PacketReader(packetBytes);

                            Message response = Message.Read(reader);

                            if (response.TransactionId == transactionId)
                            {
                                return response.Answers.ToArray();
                            }
                        }
                        catch
                        {
                            // Do nothing - bad packet (probably...)
                        }
                    }
                }
            }

            return null;
        }

        private static IPAddress[] GetDefaultDnsServers()
        {
            Dictionary<IPAddress, object> addresses = new Dictionary<IPAddress, object>();

            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (IPAddress address in nic.GetIPProperties().DnsAddresses)
                    {
                        if (address.AddressFamily == AddressFamily.InterNetwork && !addresses.ContainsKey(address))
                        {
                            addresses.Add(address, null);
                        }
                    }
                }
            }

            IPAddress[] addressArray = new IPAddress[addresses.Count];
            addresses.Keys.CopyTo(addressArray, 0);
            return addressArray;
        }
    }
}