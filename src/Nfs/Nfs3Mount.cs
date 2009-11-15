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

using System.Collections.Generic;

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
            RpcReply reply = DoSend(new ExportCall(_client.NextTransactionId()));
            if (reply.Header.IsSuccess)
            {
                ExportReply gpReply = new ExportReply(reply);
                return gpReply.Exports;
            }
            else
            {
                throw new RpcException(reply.Header.ReplyHeader);
            }
        }

        public Nfs3MountResult Mount(string dirPath)
        {
            RpcReply reply = DoSend(new MountCall(_client.NextTransactionId(), _client.Credentials, dirPath));
            if (reply.Header.IsSuccess)
            {
                MountReply gpReply = new MountReply(reply);
                if (gpReply.Status == Nfs3Status.Ok)
                {
                    return gpReply.MountResult;
                }
                throw new Nfs3Exception(gpReply.Status);
            }
            else
            {
                throw new RpcException(reply.Header.ReplyHeader);
            }
        }



        private class MountCall : RpcCall
        {
            private string _dirPath;

            public MountCall(uint transaction, RpcCredentials credentials, string dirPath)
                : base(transaction, credentials, ProgramIdentifier, ProgramVersion, 1)
            {
                _dirPath = dirPath;
            }

            public override void Write(XdrDataWriter writer)
            {
                base.Write(writer);
                writer.Write(_dirPath);
            }
        }

        private class MountReply
        {
            private RpcReplyHeader _header;

            public Nfs3Status Status { get; set; }
            public Nfs3MountResult MountResult { get; set; }

            public MountReply(RpcReply reply)
            {
                _header = reply.Header.ReplyHeader;

                Status = (Nfs3Status)reply.BodyReader.ReadInt32();
                if (Status == Nfs3Status.Ok)
                {
                    MountResult = new Nfs3MountResult(reply.BodyReader);
                }
            }
        }

        private class ExportCall : RpcCall
        {
            public ExportCall(uint transaction)
                : base(transaction, null, ProgramIdentifier, ProgramVersion, 5)
            {
            }

            public override void Write(XdrDataWriter writer)
            {
                base.Write(writer);
            }
        }

        private class ExportReply
        {
            private RpcReplyHeader _header;
            public List<Nfs3Export> Exports { get; set; }

            public ExportReply(RpcReply reply)
            {
                _header = reply.Header.ReplyHeader;

                List<Nfs3Export> exports = new List<Nfs3Export>();
                while (reply.BodyReader.ReadBool())
                {
                    exports.Add(new Nfs3Export(reply.BodyReader));
                }
                Exports = exports;
            }
        }
    }
}
