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
    public class RpcReplyHeader
    {
        public RpcAcceptedReplyHeader AcceptReply;
        public RpcRejectedReplyHeader RejectedReply;
        public RpcReplyStatus Status;

        public RpcReplyHeader()
        {
        }

        public RpcReplyHeader(XdrDataReader reader)
        {
            Status = (RpcReplyStatus)reader.ReadInt32();
            if (Status == RpcReplyStatus.Accepted)
            {
                AcceptReply = new RpcAcceptedReplyHeader(reader);
            }
            else
            {
                RejectedReply = new RpcRejectedReplyHeader(reader);
            }
        }

        public void Write(XdrDataWriter writer)
        {
            writer.Write((int)Status);

            if (Status == RpcReplyStatus.Accepted)
            {
                AcceptReply.Write(writer);
            }
            else
            {
                RejectedReply.Write(writer);
            }
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RpcReplyHeader);
        }

        public bool Equals(RpcReplyHeader other)
        {
            if (other == null)
            {
                return false;
            }

            return other.Status == Status
                && object.Equals(other.AcceptReply, AcceptReply)
                && object.Equals(other.RejectedReply, RejectedReply);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Status, AcceptReply, RejectedReply);
        }
    }
}