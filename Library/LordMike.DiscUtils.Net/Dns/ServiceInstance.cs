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

    /// <summary>
    /// Represents an instance of a type of DNS-SD service.
    /// </summary>
    public sealed class ServiceInstance
    {
        private string _name;
        private string _displayName;
        private Dictionary<string, byte[]> _parameters;
        private IList<ServiceInstanceEndPoint> _endpoints;

        internal ServiceInstance(string name)
        {
            _name = name;
        }

        /// <summary>
        /// Gets the network name for the service instance (think of this as the unique key).
        /// </summary>
        public string Name
        {
            get { return _name; }
        }

        /// <summary>
        /// Gets the display name for the service instance.
        /// </summary>
        public string DisplayName
        {
            get { return _displayName; }
            internal set { _displayName = value; }
        }

        /// <summary>
        /// Gets the parameters of the service instance.
        /// </summary>
        public Dictionary<string, byte[]> Parameters
        {
            get { return _parameters; }
            internal set { _parameters = value; }
        }

        /// <summary>
        /// Gets the EndPoints that service this instance.
        /// </summary>
        public IList<ServiceInstanceEndPoint> EndPoints
        {
            get { return _endpoints; }
            internal set { _endpoints = value; }
        }
    }
}
