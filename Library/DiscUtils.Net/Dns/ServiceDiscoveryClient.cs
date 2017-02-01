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
using System.Text;

namespace DiscUtils.Net.Dns
{
    /// <summary>
    /// Provides access to DNS-SD functionality.
    /// </summary>
    public sealed class ServiceDiscoveryClient : IDisposable
    {
        private readonly UnicastDnsClient _dnsClient;
        private MulticastDnsClient _mDnsClient;

        /// <summary>
        /// Initializes a new instance of the ServiceDiscoveryClient class.
        /// </summary>
        public ServiceDiscoveryClient()
        {
            _mDnsClient = new MulticastDnsClient();
            _dnsClient = new UnicastDnsClient();
        }

        /// <summary>
        /// Disposes of this instance.
        /// </summary>
        public void Dispose()
        {
            if (_mDnsClient != null)
            {
                _mDnsClient.Dispose();
                _mDnsClient = null;
            }
        }

        /// <summary>
        /// Flushes any cached data.
        /// </summary>
        public void FlushCache()
        {
            _mDnsClient.FlushCache();
            _dnsClient.FlushCache();
        }

        /// <summary>
        /// Queries for all the different types of service available on the local network.
        /// </summary>
        /// <returns>An array of service types, for example "_http._tcp".</returns>
        public string[] LookupServiceTypes()
        {
            return LookupServiceTypes("local.");
        }

        /// <summary>
        /// Queries for all the different types of service available in a domain.
        /// </summary>
        /// <param name="domain">The domain to query.</param>
        /// <returns>An array of service types, for example "_http._tcp".</returns>
        public string[] LookupServiceTypes(string domain)
        {
            List<ResourceRecord> records = DoLookup("_services._dns-sd._udp" + "." + domain, RecordType.Pointer);

            List<string> result = new List<string>();

            foreach (PointerRecord record in records)
            {
                result.Add(record.TargetName.Substring(0, record.TargetName.Length - (domain.Length + 1)));
            }

            return result.ToArray();
        }

        /// <summary>
        /// Queries for all instances of a particular service on the local network, retrieving all details.
        /// </summary>
        /// <param name="service">The service to query, for example "_http._tcp".</param>
        /// <returns>An array of service instances.</returns>
        public ServiceInstance[] LookupInstances(string service)
        {
            return LookupInstances(service, "local.", ServiceInstanceFields.All);
        }

        /// <summary>
        /// Queries for all instances of a particular service on the local network.
        /// </summary>
        /// <param name="service">The service to query, for example "_http._tcp".</param>
        /// <param name="fields">The details to query.</param>
        /// <returns>An array of service instances.</returns>
        /// <remarks>Excluding some fields (for example the IP address) may reduce the time taken.</remarks>
        public ServiceInstance[] LookupInstances(string service, ServiceInstanceFields fields)
        {
            return LookupInstances(service, "local.", fields);
        }

        /// <summary>
        /// Queries for all instances of a particular service on the local network.
        /// </summary>
        /// <param name="service">The service to query, for example "_http._tcp".</param>
        /// <param name="domain">The domain to query.</param>
        /// <param name="fields">The details to query.</param>
        /// <returns>An array of service instances.</returns>
        /// <remarks>Excluding some fields (for example the IP address) may reduce the time taken.</remarks>
        public ServiceInstance[] LookupInstances(string service, string domain, ServiceInstanceFields fields)
        {
            List<ResourceRecord> records = DoLookup(service + "." + domain, RecordType.Pointer);

            List<ServiceInstance> instances = new List<ServiceInstance>();
            foreach (PointerRecord record in records)
            {
                instances.Add(LookupInstance(EncodeName(record.TargetName, record.Name), fields));
            }

            return instances.ToArray();
        }

        /// <summary>
        /// Queries for all instances of a particular service on the local network.
        /// </summary>
        /// <param name="name">The instance to query, for example "My WebServer._http._tcp".</param>
        /// <param name="fields">The details to query.</param>
        /// <returns>The service instance.</returns>
        /// <remarks>Excluding some fields (for example the IP address) may reduce the time taken.</remarks>
        public ServiceInstance LookupInstance(string name, ServiceInstanceFields fields)
        {
            ServiceInstance instance = new ServiceInstance(name);

            if ((fields & ServiceInstanceFields.DisplayName) != 0)
            {
                instance.DisplayName = DecodeDisplayName(name);
            }

            if ((fields & ServiceInstanceFields.Parameters) != 0)
            {
                instance.Parameters = LookupInstanceDetails(name);
            }

            if ((fields & ServiceInstanceFields.DnsAddresses) != 0
                || (fields & ServiceInstanceFields.IPAddresses) != 0)
            {
                instance.EndPoints = LookupInstanceEndpoints(name, fields);
            }

            return instance;
        }

        private static string EncodeName(string fullName, string suffix)
        {
            string instanceName = fullName.Substring(0, fullName.Length - (suffix.Length + 1));

            StringBuilder sb = new StringBuilder();
            foreach (char ch in instanceName)
            {
                if (ch == '.' || ch == '\\')
                {
                    sb.Append('\\');
                }

                sb.Append(ch);
            }

            return sb + "." + suffix;
        }

        private static string DecodeDisplayName(string fullName)
        {
            StringBuilder sb = new StringBuilder();

            int i = 0;
            while (i < fullName.Length)
            {
                char ch = fullName[i++];

                if (ch == '.')
                {
                    return sb.ToString();
                }
                if (ch == '\\')
                {
                    ch = fullName[i++];
                }

                sb.Append(ch);
            }

            return sb.ToString();
        }

        private List<ServiceInstanceEndPoint> LookupInstanceEndpoints(string name, ServiceInstanceFields fields)
        {
            List<ResourceRecord> records = DoLookup(name, RecordType.Service);

            List<ServiceInstanceEndPoint> endpoints = new List<ServiceInstanceEndPoint>();

            foreach (ServiceRecord record in records)
            {
                List<IPEndPoint> ipEndPoints = null;
                if ((fields & ServiceInstanceFields.IPAddresses) != 0)
                {
                    ipEndPoints = new List<IPEndPoint>();

                    List<ResourceRecord> ipRecords = DoLookup(record.Target, RecordType.Address);

                    foreach (IP4AddressRecord ipRecord in ipRecords)
                    {
                        ipEndPoints.Add(new IPEndPoint(ipRecord.Address, record.Port));
                    }

                    List<ResourceRecord> ip6Records = DoLookup(record.Target, RecordType.IP6Address);

                    // foreach (Ip6AddressRecord ipRecord in ipRecords)
                    // {
                    //     ipEndPoints.Add(new IPEndPoint(ipRecord.Address, record.Port));
                    // }
                }

                endpoints.Add(new ServiceInstanceEndPoint(record.Priority, record.Weight, record.Port, record.Target, ipEndPoints.ToArray()));
            }

            return endpoints;
        }

        private Dictionary<string, byte[]> LookupInstanceDetails(string name)
        {
            List<ResourceRecord> records = DoLookup(name, RecordType.Text);

            Dictionary<string, byte[]> details = new Dictionary<string, byte[]>();

            foreach (TextRecord record in records)
            {
                foreach (KeyValuePair<string, byte[]> value in record.Values)
                {
                    details.Add(value.Key, value.Value);
                }
            }

            return details;
        }

        private List<ResourceRecord> DoLookup(string name, RecordType recordType)
        {
            string fullName = DnsClient.NormalizeDomainName(name);

            DnsClient dnsClient;

            if (fullName.EndsWith(".local.", StringComparison.OrdinalIgnoreCase))
            {
                dnsClient = _mDnsClient;
            }
            else
            {
                dnsClient = _dnsClient;
            }

            ResourceRecord[] records = dnsClient.Lookup(fullName, recordType);

            List<ResourceRecord> cleanList = new List<ResourceRecord>();
            foreach (ResourceRecord record in records)
            {
                if (record.RecordType == recordType && string.Compare(fullName, record.Name, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    cleanList.Add(record);
                }
            }

            return cleanList;
        }
    }
}