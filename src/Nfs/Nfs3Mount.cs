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

using System.Collections.Generic;
using System.IO;

namespace DiscUtils.Nfs
{
    internal sealed class Nfs3Mount : RpcProgram
    {
        public const int ProgramIdentifier = 100005;
        public const int ProgramVersion = 3;

        public const int MaxPathLength = 1024;
        public const int MaxNameLength = 255;
        public const int MaxFileHandleSize = 64;

        public Nfs3Mount(RpcClient client)
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



        public List<Nfs3Export> Exports()
        {
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, null, 5);

            RpcReply reply = DoSend(ms);
            if (reply.Header.IsSuccess)
            {
                List<Nfs3Export> exports = new List<Nfs3Export>();
                while (reply.BodyReader.ReadBool())
                {
                    exports.Add(new Nfs3Export(reply.BodyReader));
                }
                return exports;
            }
            else
            {
                throw new RpcException(reply.Header.ReplyHeader);
            }
        }

        public Nfs3MountResult Mount(string dirPath)
        {
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, _client.Credentials, 1);
            writer.Write(dirPath);

            RpcReply reply = DoSend(ms);
            if (reply.Header.IsSuccess)
            {
                Nfs3Status status = (Nfs3Status)reply.BodyReader.ReadInt32();
                if (status == Nfs3Status.Ok)
                {
                    return new Nfs3MountResult(reply.BodyReader);
                }
                throw new Nfs3Exception(status);
            }
            else
            {
                throw new RpcException(reply.Header.ReplyHeader);
            }
        }

    }
}
