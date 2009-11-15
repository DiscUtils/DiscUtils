//
// Copyright (c) 2008-2009, Kenneth Bell
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
    public sealed class RpcUnixCredential : RpcCredentials
    {
        private int _stamp;
        private string _machineName;
        private int _uid;
        private int _gid;
        private int[] _gids;

        public RpcUnixCredential(int uid, int gid, int[] gids)
        {
            _machineName = Environment.MachineName;
            _uid = uid;
            _gid = gid;
            _gids = gids;
        }

        internal override RpcAuthFlavour AuthFlavour
        {
            get { return RpcAuthFlavour.Unix; }
        }

        internal override void Write(XdrDataWriter writer)
        {
            writer.Write(_stamp);
            writer.Write(_machineName);
            writer.Write(_uid);
            writer.Write(_gid);
            if (_gids == null)
            {
                writer.Write((int)0);
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
