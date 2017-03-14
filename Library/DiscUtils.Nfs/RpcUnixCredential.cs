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

namespace DiscUtils.Nfs
{
    /// <summary>
    /// RPC credentials used for accessing an access-controlled server.
    /// </summary>
    /// <remarks>Note there is no server-side authentication with these credentials,
    /// instead the client is assumed to be trusted.</remarks>
    public sealed class RpcUnixCredential : RpcCredentials
    {
        /// <summary>
        /// Default credentials (nobody).
        /// </summary>
        /// <remarks>
        /// There is no standard UID/GID for nobody.  This default credential
        /// assumes 65534 for both the user and group.
        /// </remarks>
        public static readonly RpcUnixCredential Default = new RpcUnixCredential(65534, 65534);

        private readonly int _gid;
        private readonly int[] _gids;

        private readonly string _machineName;
        private readonly int _uid;

        /// <summary>
        /// Initializes a new instance of the RpcUnixCredential class.
        /// </summary>
        /// <param name="user">The user's unique id (UID).</param>
        /// <param name="primaryGroup">The user's primary group id (GID).</param>
        public RpcUnixCredential(int user, int primaryGroup)
            : this(user, primaryGroup, new int[] { }) {}

        /// <summary>
        /// Initializes a new instance of the RpcUnixCredential class.
        /// </summary>
        /// <param name="user">The user's unique id (UID).</param>
        /// <param name="primaryGroup">The user's primary group id (GID).</param>
        /// <param name="groups">The user's supplementary group ids.</param>
        public RpcUnixCredential(int user, int primaryGroup, int[] groups)
        {
            _machineName = Environment.MachineName;
            _uid = user;
            _gid = primaryGroup;
            _gids = groups;
        }

        internal override RpcAuthFlavour AuthFlavour
        {
            get { return RpcAuthFlavour.Unix; }
        }

        internal override void Write(XdrDataWriter writer)
        {
            writer.Write(0);
            writer.Write(_machineName);
            writer.Write(_uid);
            writer.Write(_gid);
            if (_gids == null)
            {
                writer.Write(0);
            }
            else
            {
                writer.Write(_gids.Length);
                for (int i = 0; i < _gids.Length; ++i)
                {
                    writer.Write(_gids[i]);
                }
            }
        }
    }
}