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
    internal sealed class PortMap2 : RpcProgram
    {
        public const int ProgramIdentifier = 100000;
        public const int ProgramVersion = 2;

        public PortMap2(RpcClient client)
            : base(client) {}

        public override int Identifier
        {
            get { return ProgramIdentifier; }
        }

        public override int Version
        {
            get { return ProgramVersion; }
        }

        public int GetPort(int program, int version, PortMap2Protocol protocol)
        {
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, null, PortMapProc2.GetPort);

            new PortMap2Mapping()
            {
                Program = program,
                Version = version,
                Protocol = protocol,
                Port = 0
            }.Write(writer);

            RpcReply reply = DoSend(ms);
            if (reply.Header.IsSuccess)
            {
                var port = new PortMap2Port(reply.BodyReader);
                return (int)reply.BodyReader.ReadUInt32();
            }
            throw new RpcException(reply.Header.ReplyHeader);
        }
    }
}
