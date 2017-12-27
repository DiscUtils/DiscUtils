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
    public class RpcRejectedReplyHeader
    {
        public RpcAuthenticationStatus AuthenticationStatus;
        public RpcMismatchInfo MismatchInfo;
        public RpcRejectedStatus Status;

        public RpcRejectedReplyHeader()
        {
        }

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

        public void Write(XdrDataWriter writer)
        {
            writer.Write((int)Status);
            if (Status == RpcRejectedStatus.RpcMismatch)
            {
                MismatchInfo.Write(writer);
            }
            else
            {
                writer.Write((int)AuthenticationStatus);
            }
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RpcRejectedReplyHeader);
        }

        public bool Equals(RpcRejectedReplyHeader other)
        {
            if (other == null)
            {
                return false;
            }

            return other.Status == Status
                && object.Equals(other.MismatchInfo, MismatchInfo)
                && other.AuthenticationStatus == AuthenticationStatus;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Status, MismatchInfo, AuthenticationStatus);
        }
    }
}