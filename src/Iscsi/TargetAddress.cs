//
// Copyright (c) 2009, Kenneth Bell
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
        private const int DefaultPort = 3260;

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
        /// <param name="str">The address to parse</param>
        /// <returns>The structured address</returns>
        public static TargetAddress Parse(string str)
        {
            int addrEnd = str.IndexOfAny(new char[] { ':', ',' });
            if (addrEnd == -1)
            {
                return new TargetAddress(str, DefaultPort, "");
            }

            string addr = str.Substring(0, addrEnd);
            int port = DefaultPort;
            string targetGroupTag = "";

            int focus = addrEnd;
            if (str[focus] == ':')
            {
                int portStart = addrEnd + 1;
                int portEnd = str.IndexOf(',', portStart);

                if (portEnd == -1)
                {
                    port = int.Parse(str.Substring(portStart));
                    focus = str.Length;
                }
                else
                {
                    port = int.Parse(str.Substring(portStart, portEnd - portStart));
                    focus = portEnd;
                }
            }

            if (focus < str.Length)
            {
                targetGroupTag = str.Substring(focus + 1);
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
    }
}
