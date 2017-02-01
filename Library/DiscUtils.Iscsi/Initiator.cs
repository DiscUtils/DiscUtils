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
    /// Class representing an iSCSI initiator.
    /// </summary>
    /// <remarks>Normally, this is the first class instantiated when talking to an iSCSI Portal (i.e. network entity).
    /// Create an instance and configure it, before communicating with the Target.</remarks>
    public class Initiator
    {
        private const int DefaultPort = 3260;
        private string _password;

        private string _userName;

        /// <summary>
        /// Sets credentials used to authenticate this Initiator to the Target.
        /// </summary>
        /// <param name="userName">The user name.</param>
        /// <param name="password">The password, should be at least 12 characters.</param>
        public void SetCredentials(string userName, string password)
        {
            _userName = userName;
            _password = password;
        }

        /// <summary>
        /// Connects to a Target.
        /// </summary>
        /// <param name="target">The Target to connect to.</param>
        /// <returns>The session representing the target connection.</returns>
        public Session ConnectTo(TargetInfo target)
        {
            return ConnectTo(target.Name, target.Addresses);
        }

        /// <summary>
        /// Connects to a Target.
        /// </summary>
        /// <param name="target">The Target to connect to.</param>
        /// <param name="addresses">The list of addresses for the target.</param>
        /// <returns>The session representing the target connection.</returns>
        public Session ConnectTo(string target, params string[] addresses)
        {
            TargetAddress[] addressObjs = new TargetAddress[addresses.Length];
            for (int i = 0; i < addresses.Length; ++i)
            {
                addressObjs[i] = TargetAddress.Parse(addresses[i]);
            }

            return ConnectTo(target, addressObjs);
        }

        /// <summary>
        /// Connects to a Target.
        /// </summary>
        /// <param name="target">The Target to connect to.</param>
        /// <param name="addresses">The list of addresses for the target.</param>
        /// <returns>The session representing the target connection.</returns>
        public Session ConnectTo(string target, IList<TargetAddress> addresses)
        {
            return new Session(SessionType.Normal, target, _userName, _password, addresses);
        }

        /// <summary>
        /// Gets the Targets available from a Portal (i.e. network entity).
        /// </summary>
        /// <param name="address">The address of the Portal.</param>
        /// <returns>The list of Targets available.</returns>
        /// <remarks>If you just have an IP address, use this method to discover the available Targets.</remarks>
        public TargetInfo[] GetTargets(string address)
        {
            return GetTargets(TargetAddress.Parse(address));
        }

        /// <summary>
        /// Gets the Targets available from a Portal (i.e. network entity).
        /// </summary>
        /// <param name="address">The address of the Portal.</param>
        /// <returns>The list of Targets available.</returns>
        /// <remarks>If you just have an IP address, use this method to discover the available Targets.</remarks>
        public TargetInfo[] GetTargets(TargetAddress address)
        {
            using (
                Session session = new Session(SessionType.Discovery, null, _userName, _password,
                    new[] { address }))
            {
                return session.EnumerateTargets();
            }
        }
    }
}