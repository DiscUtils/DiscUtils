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
    public class RpcMessageHeader
    {
        public RpcMessageHeader()
        {
        }

        public RpcMessageHeader(XdrDataReader reader)
        {
            TransactionId = reader.ReadUInt32();
            RpcMessageType type = (RpcMessageType)reader.ReadInt32();
            if (type != RpcMessageType.Reply)
            {
                throw new NotSupportedException("Parsing RPC call messages");
            }

            ReplyHeader = new RpcReplyHeader(reader);
        }

        public bool IsSuccess
        {
            get
            {
                return ReplyHeader != null && ReplyHeader.Status == RpcReplyStatus.Accepted &&
                       ReplyHeader.AcceptReply.AcceptStatus == RpcAcceptStatus.Success;
            }
        }

        public RpcReplyHeader ReplyHeader { get; set; }

        public uint TransactionId { get; set; }

        public void Write(XdrDataWriter writer)
        {
            writer.Write(TransactionId);
            writer.Write((int)RpcMessageType.Reply);
            ReplyHeader.Write(writer);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RpcMessageHeader);
        }

        public bool Equals(RpcMessageHeader other)
        {
            if (other == null)
            {
                return false;
            }

            return other.IsSuccess == IsSuccess
                && other.TransactionId == TransactionId
                && object.Equals(other.ReplyHeader, ReplyHeader);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(IsSuccess, TransactionId, ReplyHeader);
        }


        public static RpcMessageHeader Accepted(uint transactionId)
        {
            return new RpcMessageHeader()
            {
                TransactionId = transactionId,
                ReplyHeader = new RpcReplyHeader()
                {
                    Status = RpcReplyStatus.Accepted,
                    AcceptReply = new RpcAcceptedReplyHeader()
                    {
                        AcceptStatus = RpcAcceptStatus.Success,
                        Verifier = RpcAuthentication.Null()
                    }
                }
            };
        }

        public static RpcMessageHeader ProcedureUnavailable(uint transactionId)
        {
            return new RpcMessageHeader()
            {
                TransactionId = transactionId,
                ReplyHeader = new RpcReplyHeader()
                {
                    Status = RpcReplyStatus.Accepted,
                    AcceptReply = new RpcAcceptedReplyHeader()
                    {
                        AcceptStatus = RpcAcceptStatus.ProcedureUnavailable,
                        MismatchInfo = new RpcMismatchInfo(),
                        Verifier = RpcAuthentication.Null()
                    }
                }
            };
        }
    }
}