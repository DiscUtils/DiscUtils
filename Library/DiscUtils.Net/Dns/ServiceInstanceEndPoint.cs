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
    using System.Collections.Generic;
    using System.Net;

    /// <summary>
    /// Represents an endpoint (address, port, etc) that provides a DNS-SD service instance.
    /// </summary>
    public sealed class ServiceInstanceEndPoint
    {
        private int _priority;
        private int _weight;
        private int _port;
        private string _address;
        private IPEndPoint[] _ipEndPoints;

        internal ServiceInstanceEndPoint(int priority, int weight, int port, string address, IPEndPoint[] ipEndPoints)
        {
            _priority = priority;
            _weight = weight;
            _port = port;
            _address = address;
            _ipEndPoints = ipEndPoints;
        }

        /// <summary>
        /// Gets the priority of this EndPoint (lower value is higher priority).
        /// </summary>
        public int Priority
        {
            get { return _priority; }
        }

        /// <summary>
        /// Gets the relative weight of this EndPoint when randomly choosing between EndPoints of equal priority.
        /// </summary>
        public int Weight
        {
            get { return _weight; }
        }

        /// <summary>
        /// Gets the DNS address of this EndPoint.
        /// </summary>
        public string DnsAddress
        {
            get { return _address; }
        }

        /// <summary>
        /// Gets the port of this EndPoint.
        /// </summary>
        public int Port
        {
            get { return _port; }
        }

        /// <summary>
        /// Gets the IP addresses (as IPEndPoint instances) of this EndPoint.
        /// </summary>
        public IEnumerable<IPEndPoint> IPEndPoints
        {
            get { return _ipEndPoints; }
        }
    }
}
