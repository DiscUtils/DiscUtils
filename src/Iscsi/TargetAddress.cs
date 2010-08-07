//
// Copyright (c) 2008-2010, Kenneth Bell
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
using System.Globalization;

namespace DiscUtils.Iscsi
{
    /// <summary>
    /// Information about an iSCSI Target.
    /// </summary>
    /// <remarks>
    /// A target contains zero or more LUNs.
    /// </remarks>
    public class TargetAddress
    {
        internal const int DefaultPort = 3260;

        private string _networkAddress;
        private int _networkPort;
        private string _targetGroupTag;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="address">The IP address (or FQDN) of the Target.</param>
        /// <param name="port">The network port of the Target</param>
        /// <param name="targetGroupTag">The Group Tag of the Target</param>
        public TargetAddress(string address, int port, string targetGroupTag)
        {
            _networkAddress = address;
            _networkPort = port;
            _targetGroupTag = targetGroupTag;
        }

        /// <summary>
        /// The IP address (or FQDN) of the Target.
        /// </summary>
        public string NetworkAddress
        {
            get { return _networkAddress; }
        }

        /// <summary>
        /// The network port of the Target.
        /// </summary>
        public int NetworkPort
        {
            get { return _networkPort; }
        }

        /// <summary>
        /// The Group Tag of the Target.
        /// </summary>
        public string TargetGroupTag
        {
            get { return _targetGroupTag; }
        }

        /// <summary>
        /// Parses a Target address in string form.
        /// </summary>
        /// <param name="address">The address to parse</param>
        /// <returns>The structured address</returns>
        public static TargetAddress Parse(string address)
        {
            int addrEnd = address.IndexOfAny(new char[] { ':', ',' });
            if (addrEnd == -1)
            {
                return new TargetAddress(address, DefaultPort, "");
            }

            string addr = address.Substring(0, addrEnd);
            int port = DefaultPort;
            string targetGroupTag = "";

            int focus = addrEnd;
            if (address[focus] == ':')
            {
                int portStart = addrEnd + 1;
                int portEnd = address.IndexOf(',', portStart);

                if (portEnd == -1)
                {
                    port = int.Parse(address.Substring(portStart), CultureInfo.InvariantCulture);
                    focus = address.Length;
                }
                else
                {
                    port = int.Parse(address.Substring(portStart, portEnd - portStart), CultureInfo.InvariantCulture);
                    focus = portEnd;
                }
            }

            if (focus < address.Length)
            {
                targetGroupTag = address.Substring(focus + 1);
            }

            return new TargetAddress(addr, port, targetGroupTag);
        }

        /// <summary>
        /// Gets the TargetAddress in string format.
        /// </summary>
        /// <returns>The string in 'host:port,targetgroup' format</returns>
        public override string ToString()
        {
            string result = NetworkAddress;
            if (_networkPort != DefaultPort)
            {
                result += ":" + _networkPort;
            }
            if (!string.IsNullOrEmpty(_targetGroupTag))
            {
                result += "," + _targetGroupTag;
            }
            return result;
        }

        /// <summary>
        /// Gets the target address as a URI.
        /// </summary>
        /// <returns>The target address in the form: iscsi://host[:port][/grouptag]</returns>
        public Uri ToUri()
        {
            UriBuilder builder = new UriBuilder();
            builder.Scheme = "iscsi";
            builder.Host = NetworkAddress;
            builder.Port = (_networkPort != DefaultPort) ? _networkPort : -1;
            builder.Path = string.IsNullOrEmpty(_targetGroupTag) ? "" : _targetGroupTag;
            return builder.Uri;
        }
    }
}
