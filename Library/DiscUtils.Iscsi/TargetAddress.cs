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

        /// <summary>
        /// Initializes a new instance of the TargetAddress class.
        /// </summary>
        /// <param name="address">The IP address (or FQDN) of the Target.</param>
        /// <param name="port">The network port of the Target.</param>
        /// <param name="targetGroupTag">The Group Tag of the Target.</param>
        public TargetAddress(string address, int port, string targetGroupTag)
        {
            NetworkAddress = address;
            NetworkPort = port;
            TargetGroupTag = targetGroupTag;
        }

        /// <summary>
        /// Gets the IP address (or FQDN) of the Target.
        /// </summary>
        public string NetworkAddress { get; }

        /// <summary>
        /// Gets the network port of the Target.
        /// </summary>
        public int NetworkPort { get; }

        /// <summary>
        /// Gets the Group Tag of the Target.
        /// </summary>
        public string TargetGroupTag { get; }

        /// <summary>
        /// Parses a Target address in string form.
        /// </summary>
        /// <param name="address">The address to parse.</param>
        /// <returns>The structured address.</returns>
        public static TargetAddress Parse(string address)
        {
            int addrEnd = address.IndexOfAny(new[] { ':', ',' });
            if (addrEnd == -1)
            {
                return new TargetAddress(address, DefaultPort, string.Empty);
            }

            string addr = address.Substring(0, addrEnd);
            int port = DefaultPort;
            string targetGroupTag = string.Empty;

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
        /// <returns>The string in 'host:port,targetgroup' format.</returns>
        public override string ToString()
        {
            string result = NetworkAddress;
            if (NetworkPort != DefaultPort)
            {
                result += ":" + NetworkPort;
            }

            if (!string.IsNullOrEmpty(TargetGroupTag))
            {
                result += "," + TargetGroupTag;
            }

            return result;
        }

        /// <summary>
        /// Gets the target address as a URI.
        /// </summary>
        /// <returns>The target address in the form: iscsi://host[:port][/grouptag].</returns>
        public Uri ToUri()
        {
            UriBuilder builder = new UriBuilder();
            builder.Scheme = "iscsi";
            builder.Host = NetworkAddress;
            builder.Port = NetworkPort != DefaultPort ? NetworkPort : -1;
            builder.Path = string.IsNullOrEmpty(TargetGroupTag) ? string.Empty : TargetGroupTag;
            return builder.Uri;
        }
    }
}