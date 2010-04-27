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


namespace DiscUtils.Nfs
{
    internal enum RpcRejectedStatus
    {
        RpcMismatch = 0,
        AuthError = 1,
    }

    internal enum RpcAuthenticationStatus
    {
        None = 0,
        BadCredentials = 1,
        RejectedCredentials = 2,
        BadVerifier = 3,
        RejectedVerifier = 4,
        TooWeak = 5
    }

    internal class RpcRejectedReplyHeader
    {
        public RpcRejectedStatus Status;
        public RpcMismatchInfo MismatchInfo;
        public RpcAuthenticationStatus AuthenticationStatus;

        public RpcRejectedReplyHeader(XdrDataReader reader)
        {
            Status = (RpcRejectedStatus)reader.ReadInt32();
            if (Status == RpcRejectedStatus.RpcMismatch)
            {
                MismatchInfo = new RpcMismatchInfo(reader);
            }
            else
            {
                AuthenticationStatus = (RpcAuthenticationStatus)reader.ReadInt32();
            }
        }
    }
}
