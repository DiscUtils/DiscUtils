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
using System.Net.Sockets;

namespace DiscUtils.Net.Dns
{
    /// <summary>
    /// Implements the Multicast DNS (mDNS) protocol.
    /// </summary>
    /// <remarks>
    /// This implementation is a hybrid of a 'proper' mDNS resolver and a classic DNS resolver
    /// configured to use the mDNS multicast address.  The implementation is aware of some of
    /// the unique semantics of mDNS, but because it is loaded in arbitrary processes cannot
    /// claim port 5353.  It attempts to honour the spirit of mDNS to the extent possible whilst
    /// not binding to port 5353.
    /// </remarks>
    public sealed class MulticastDnsClient : DnsClient, IDisposable
    {
        private Dictionary<string, Dictionary<RecordType, List<ResourceRecord>>> _cache;
        private ushort _nextTransId;

        private readonly Dictionary<ushort, Transaction> _transactions;
        private UdpClient _udpClient;

        /// <summary>
        /// Initializes a new instance of the MulticastDnsClient class.
        /// </summary>
        public MulticastDnsClient()
        {
            _nextTransId = (ushort)new Random().Next();
            _transactions = new Dictionary<ushort, Transaction>();
            _udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            _udpClient.BeginReceive(ReceiveCallback, null);
            _cache = new Dictionary<string, Dictionary<RecordType, List<ResourceRecord>>>();
        }

        /// <summary>
        /// Disposes of this instance.
        /// </summary>
        public void Dispose()
        {
            if (_udpClient != null)
            {
                ((IDisposable)_udpClient).Dispose();
                _udpClient = null;
            }
        }

        /// <summary>
        /// Flushes any cached DNS records.
        /// </summary>
        public override void FlushCache()
        {
            lock (_transactions)
            {
                _cache = new Dictionary<string, Dictionary<RecordType, List<ResourceRecord>>>();
            }
        }

        /// <summary>
        /// Looks up a record in DNS.
        /// </summary>
        /// <param name="name">The name to lookup.</param>
        /// <param name="type">The type of record requested.</param>
        /// <returns>The records returned by the DNS server, if any.</returns>
        public override ResourceRecord[] Lookup(string name, RecordType type)
        {
            string normName = NormalizeDomainName(name);

            lock (_transactions)
            {
                ExpireRecords();

                Dictionary<RecordType, List<ResourceRecord>> typeRecords;
                if (_cache.TryGetValue(normName.ToUpperInvariant(), out typeRecords))
                {
                    List<ResourceRecord> records;
                    if (typeRecords.TryGetValue(type, out records))
                    {
                        return records.ToArray();
                    }
                }
            }

            return QueryNetwork(name, type);
        }

        private static void AddRecord(Dictionary<string, Dictionary<RecordType, List<ResourceRecord>>> store, ResourceRecord record)
        {
            Dictionary<RecordType, List<ResourceRecord>> nameRec;
            if (!store.TryGetValue(record.Name.ToUpperInvariant(), out nameRec))
            {
                nameRec = new Dictionary<RecordType, List<ResourceRecord>>();
                store[record.Name.ToUpperInvariant()] = nameRec;
            }

            List<ResourceRecord> records;
            if (!nameRec.TryGetValue(record.RecordType, out records))
            {
                records = new List<ResourceRecord>();
                nameRec.Add(record.RecordType, records);
            }

            records.Add(record);
        }

        private ResourceRecord[] QueryNetwork(string name, RecordType type)
        {
            ushort transactionId = _nextTransId++;
            string normName = NormalizeDomainName(name);

            Transaction transaction = new Transaction();
            try
            {
                lock (_transactions)
                {
                    _transactions.Add(transactionId, transaction);
                }

                PacketWriter writer = new PacketWriter(1800);
                Message msg = new Message();
                msg.TransactionId = transactionId;
                msg.Flags = new MessageFlags(false, OpCode.Query, false, false, false, false, ResponseCode.Success);
                msg.Questions.Add(new Question { Name = normName, Type = type, Class = RecordClass.Internet });

                msg.WriteTo(writer);

                byte[] msgBytes = writer.GetBytes();

                IPEndPoint mDnsAddress = new IPEndPoint(IPAddress.Parse("224.0.0.251"), 5353);
                lock (_udpClient)
                {
                    _udpClient.Send(msgBytes, msgBytes.Length, mDnsAddress);
                }

                transaction.CompleteEvent.WaitOne(2000);
            }
            finally
            {
                lock (_transactions)
                {
                    _transactions.Remove(transactionId);
                }
            }

            return transaction.Answers.ToArray();
        }

        private void ExpireRecords()
        {
            DateTime now = DateTime.UtcNow;

            List<string> removeNames = new List<string>();

            foreach (KeyValuePair<string, Dictionary<RecordType, List<ResourceRecord>>> nameRecord in _cache)
            {
                List<RecordType> removeTypes = new List<RecordType>();

                foreach (KeyValuePair<RecordType, List<ResourceRecord>> typeRecords in nameRecord.Value)
                {
                    int i = 0;
                    while (i < typeRecords.Value.Count)
                    {
                        if (typeRecords.Value[i].Expiry < now)
                        {
                            typeRecords.Value.RemoveAt(i);
                        }
                        else
                        {
                            ++i;
                        }
                    }

                    if (typeRecords.Value.Count == 0)
                    {
                        removeTypes.Add(typeRecords.Key);
                    }
                }

                foreach (RecordType recordType in removeTypes)
                {
                    nameRecord.Value.Remove(recordType);
                }

                if (nameRecord.Value.Count == 0)
                {
                    removeNames.Add(nameRecord.Key);
                }
            }

            foreach (string name in removeNames)
            {
                _cache.Remove(name);
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                IPEndPoint sender = null;
                byte[] packetBytes;

                lock (_udpClient)
                {
                    packetBytes = _udpClient.EndReceive(ar, ref sender);
                }

                PacketReader reader = new PacketReader(packetBytes);

                Message msg = Message.Read(reader);

                lock (_transactions)
                {
                    Transaction transaction;
                    _transactions.TryGetValue(msg.TransactionId, out transaction);

                    foreach (ResourceRecord answer in msg.AdditionalRecords)
                    {
                        AddRecord(_cache, answer);
                    }

                    foreach (ResourceRecord answer in msg.Answers)
                    {
                        if (transaction != null)
                        {
                            transaction.Answers.Add(answer);
                        }

                        AddRecord(_cache, answer);
                    }
                }
            }
            finally
            {
                lock (_udpClient)
                {
                    _udpClient.BeginReceive(ReceiveCallback, null);
                }
            }
        }
    }
}