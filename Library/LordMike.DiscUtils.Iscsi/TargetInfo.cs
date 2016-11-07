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

using System.Collections.Generic;

namespace DiscUtils.Iscsi
{
    /// <summary>
    /// Information about an iSCSI Target.
    /// </summary>
    /// <remarks>
    /// A target contains zero or more LUNs.
    /// </remarks>
    public class TargetInfo
    {
        private readonly TargetAddress[] _addresses;

        /// <summary>
        /// Initializes a new instance of the TargetInfo class.
        /// </summary>
        /// <param name="name">The name of the Target.</param>
        /// <param name="addresses">The network addresses of the Target.</param>
        public TargetInfo(string name, TargetAddress[] addresses)
        {
            Name = name;
            _addresses = addresses;
        }

        /// <summary>
        /// Gets the network addresses of the Target.
        /// </summary>
        public IList<TargetAddress> Addresses
        {
            get { return _addresses; }
        }

        /// <summary>
        /// Gets the name of the Target.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the primary address of the Target as a string.
        /// </summary>
        /// <returns>String of the form host[:port][,group]/name.</returns>
        public override string ToString()
        {
            return _addresses[0] + "/" + Name;
        }
    }
}