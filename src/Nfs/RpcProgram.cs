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

using System.IO;

namespace DiscUtils.Nfs
{
    internal abstract class RpcProgram
    {
        protected RpcClient _client;

        public const uint RpcVersion = 2;
        public abstract int Identifier { get; }
        public abstract int Version { get; }

        protected RpcProgram(RpcClient client)
        {
            _client = client;
        }

        public void NullProc()
        {
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, null, 0);
            RpcReply reply = DoSend(ms);
            if (reply.Header.IsSuccess)
            {
                return;
            }
            else
            {
                throw new RpcException(reply.Header.ReplyHeader);
            }
        }

        protected RpcReply DoSend(MemoryStream ms)
        {
            RpcTcpTransport transport = _client.GetTransport(Identifier, Version);

            byte[] buffer = ms.ToArray();
            buffer = transport.Send(buffer);

            XdrDataReader reader = new XdrDataReader(new MemoryStream(buffer));
            RpcMessageHeader header = new RpcMessageHeader(reader);
            return new RpcReply() { Header = header, BodyReader = reader };
        }

        protected XdrDataWriter StartCallMessage(MemoryStream ms, RpcCredentials credentials, uint procedure)
        {
            XdrDataWriter writer = new XdrDataWriter(ms);

            writer.Write(_client.NextTransactionId());
            writer.Write((int)RpcMessageType.Call);

            RpcCallHeader hdr = new RpcCallHeader();
            hdr.RpcVersion = RpcVersion;
            hdr.Program = (uint)Identifier;
            hdr.Version = (uint)Version;
            hdr.Proc = procedure;
            hdr.Credentials = new RpcAuthentication(credentials ?? new RpcNullCredentials());
            hdr.Verifier = RpcAuthentication.Null();
            hdr.Write(writer); 

            return writer;
        }
    }
}
