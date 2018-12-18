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

using System.IO;

namespace DiscUtils.Nfs
{
    internal abstract class RpcProgram
    {
        public const uint RpcVersion = 2;

        protected IRpcClient _client;

        protected RpcProgram(IRpcClient client)
        {
            _client = client;
        }

        public abstract int Identifier { get; }

        public abstract int Version { get; }

        public void NullProc()
        {
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, null, NfsProc3.Null);
            RpcReply reply = DoSend(ms);
            if (reply.Header.IsSuccess) { }
            else
            {
                throw new RpcException(reply.Header.ReplyHeader);
            }
        }

        protected RpcReply DoSend(MemoryStream ms)
        {
            IRpcTransport transport = _client.GetTransport(Identifier, Version);

            byte[] buffer = ms.ToArray();
            buffer = transport.SendAndReceive(buffer);

            XdrDataReader reader = new XdrDataReader(new MemoryStream(buffer));
            RpcMessageHeader header = new RpcMessageHeader(reader);
            return new RpcReply { Header = header, BodyReader = reader };
        }

        protected XdrDataWriter StartCallMessage(MemoryStream ms, RpcCredentials credentials, PortMapProc2 procedure)
        {
            return StartCallMessage(ms, credentials, (int)procedure);
        }

        protected XdrDataWriter StartCallMessage(MemoryStream ms, RpcCredentials credentials, MountProc3 procedure)
        {
            return StartCallMessage(ms, credentials, (int)procedure);
        }

        protected XdrDataWriter StartCallMessage(MemoryStream ms, RpcCredentials credentials, NfsProc3 procedure)
        {
            return StartCallMessage(ms, credentials, (int)procedure);
        }

        protected XdrDataWriter StartCallMessage(MemoryStream ms, RpcCredentials credentials, int procedure)
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
