//
// Copyright (c) 2008-2011, Kenneth Bell
// Copyright (c) 2017, Quamotion
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
    public class RpcAcceptedReplyHeader
    {
        public RpcAcceptStatus AcceptStatus;
        public RpcMismatchInfo MismatchInfo;
        public RpcAuthentication Verifier;

        public RpcAcceptedReplyHeader()
        {
        }

        public RpcAcceptedReplyHeader(XdrDataReader reader)
        {
            Verifier = new RpcAuthentication(reader);
            AcceptStatus = (RpcAcceptStatus)reader.ReadInt32();
            if (AcceptStatus == RpcAcceptStatus.ProgramVersionMismatch)
            {
                MismatchInfo = new RpcMismatchInfo(reader);
            }
        }

        public void Write(XdrDataWriter writer)
        {
            Verifier.Write(writer);
            writer.Write((int)AcceptStatus);
            if (AcceptStatus == RpcAcceptStatus.ProgramVersionMismatch)
            {
                MismatchInfo.Write(writer);
            }
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RpcAcceptedReplyHeader);
        }

        public bool Equals(RpcAcceptedReplyHeader other)
        {
            if (other == null)
            {
                return false;
            }

            return object.Equals(other.Verifier, Verifier)
                && other.AcceptStatus == AcceptStatus
                && object.Equals(other.MismatchInfo, MismatchInfo);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Verifier, AcceptStatus, MismatchInfo);
        }
    }
}