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


namespace DiscUtils.Nfs
{
    internal enum PortMapperProtocol
    {
        Tcp = 6,
        Udp = 17
    }

    internal sealed class PortMapper : RpcProgram
    {
        public const int ProgramIdentifier = 100000;
        public const int ProgramVersion = 2;

        public PortMapper(RpcClient client)
            : base(client)
        {
        }

        public override int Identifier
        {
            get { return ProgramIdentifier; }
        }

        public override int Version
        {
            get { return ProgramVersion; }
        }

        public int GetPort(int program, int version, PortMapperProtocol protocol)
        {
            RpcReply reply = DoSend(new GetPortCall(_client.NextTransactionId(), ProgramVersion, (uint)program, (uint)version, protocol));
            if (reply.Header.IsSuccess)
            {
                GetPortReply gpReply = new GetPortReply(reply);
                return (int)gpReply.Port;
            }
            else
            {
                throw new RpcException(reply.Header.ReplyHeader);
            }
        }



        private class GetPortCall : RpcCall
        {
            private uint _program;
            private uint _version;
            private PortMapperProtocol _protocol;

            public GetPortCall(uint transaction, int portMapVersion, uint program, uint version, PortMapperProtocol protocol)
                : base(transaction, null, ProgramIdentifier, portMapVersion, 3)
            {
                _program = program;
                _version = version;
                _protocol = protocol;
            }

            public override void Write(XdrDataWriter writer)
            {
                base.Write(writer);
                writer.Write(_program);
                writer.Write(_version);
                writer.Write((uint)_protocol);
                writer.Write((uint)0);
            }
        }

        private class GetPortReply
        {
            private RpcReplyHeader _header;
            public uint Port { get; set; }

            public GetPortReply(RpcReply reply)
            {
                _header = reply.Header.ReplyHeader;
                Port = reply.BodyReader.ReadUInt32();
            }
        }
    }
}
