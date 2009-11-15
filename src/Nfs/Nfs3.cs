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
    internal enum Nfs3Status {
        Ok = 0,
        NotOwner = 1,
        NoSuchEntity = 2,
        IoError = 5,
        NFS3ERR_NXIO = 6,
        AccessDenied = 13,
        NFS3ERR_EXIST = 17,
        NFS3ERR_XDEV = 18,
        NFS3ERR_NODEV = 19,
        NotDirectory = 20,
        NFS3ERR_ISDIR = 21,
        InvalidArgument = 22,
        NFS3ERR_FBIG = 27,
        NFS3ERR_NOSPC = 28,
        NFS3ERR_ROFS = 30,
        NFS3ERR_MLINK = 31,
        NameTooLong = 63,
        NFS3ERR_NOTEMPTY = 66,
        NFS3ERR_DQUOT = 69,
        NFS3ERR_STALE = 70,
        NFS3ERR_REMOTE = 71,
        NFS3ERR_BADHANDLE = 10001,
        NFS3ERR_NOT_SYNC = 10002,
        NFS3ERR_BAD_COOKIE = 10003,
        NotSupported = 10004,
        NFS3ERR_TOOSMALL = 10005,
        ServerFault = 10006,
        NFS3ERR_BADTYPE = 10007,
        NFS3ERR_JUKEBOX = 10008
    };



    internal sealed class Nfs3 : RpcProgram
    {
        public const int ProgramIdentifier = 100003;
        public const int ProgramVersion = 3;

        public const int MaxFileHandleSize = 64;
        public const int CookieVerifierSize = 8;
        public const int CreateVerifierSize = 8;
        public const int WriteVerifierSize = 8;

        public Nfs3(RpcClient client)
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



        public Nfs3FileSystemInfo FileSystemInfo(Nfs3FileHandle fileHandle)
        {
            RpcReply reply = DoSend(new FsInfoCall(_client.NextTransactionId(), _client.Credentials, fileHandle));
            if (reply.Header.IsSuccess)
            {
                FsInfoReply fsiReply = new FsInfoReply(reply);
                if (fsiReply.Status == Nfs3Status.Ok)
                {
                    return fsiReply.FileSystemInfo;
                }
                else
                {
                    throw new Nfs3Exception(fsiReply.Status);
                }
            }
            else
            {
                throw new RpcException(reply.Header.ReplyHeader);
            }
        }

        public Nfs3SetAttributesResult SetAttributes(Nfs3FileHandle dirHandle, Nfs3SetAttributes newAttributes)
        {
            RpcReply reply = DoSend(new SetAttributesCall(_client.NextTransactionId(), _client.Credentials, dirHandle, newAttributes));
            if (reply.Header.IsSuccess)
            {
                return new Nfs3SetAttributesResult(reply.BodyReader);
            }
            else
            {
                throw new RpcException(reply.Header.ReplyHeader);
            }
        }

        public Nfs3LookupResult Lookup(Nfs3FileHandle dir, string name)
        {
            RpcReply reply = DoSend(new LookupCall(_client.NextTransactionId(), _client.Credentials, dir, name));
            if (reply.Header.IsSuccess)
            {
                return new Nfs3LookupResult(reply.BodyReader);
            }
            else
            {
                throw new RpcException(reply.Header.ReplyHeader);
            }
        }

        public Nfs3AccessResult Access(Nfs3FileHandle handle, Nfs3AccessPermissions requested)
        {
            RpcReply reply = DoSend(new AccessCall(_client.NextTransactionId(), _client.Credentials, handle, requested));
            if (reply.Header.IsSuccess)
            {
                return new Nfs3AccessResult(reply.BodyReader);
            }
            else
            {
                throw new RpcException(reply.Header.ReplyHeader);
            }
        }

        public Nfs3ReadResult Read(Nfs3FileHandle handle, long position, int count)
        {
            RpcReply reply = DoSend(new ReadCall(_client.NextTransactionId(), _client.Credentials, handle, position, count));
            if (reply.Header.IsSuccess)
            {
                return new Nfs3ReadResult(reply.BodyReader);
            }
            else
            {
                throw new RpcException(reply.Header.ReplyHeader);
            }
        }

        public Nfs3WriteResult Write(Nfs3FileHandle handle, long position, byte[] buffer, int bufferOffset, int count)
        {
            RpcReply reply = DoSend(new WriteCall(_client.NextTransactionId(), _client.Credentials, handle, position, buffer, bufferOffset, count));
            if (reply.Header.IsSuccess)
            {
                return new Nfs3WriteResult(reply.BodyReader);
            }
            else
            {
                throw new RpcException(reply.Header.ReplyHeader);
            }
        }

        public Nfs3CreateResult Create(Nfs3FileHandle dirHandle, string name, bool createNew, Nfs3SetAttributes attributes)
        {
            RpcReply reply = DoSend(new CreateCall(_client.NextTransactionId(), _client.Credentials, dirHandle, name, createNew, attributes));
            if (reply.Header.IsSuccess)
            {
                return new Nfs3CreateResult(reply.BodyReader);
            }
            else
            {
                throw new RpcException(reply.Header.ReplyHeader);
            }
        }

        public Nfs3SetAttributesResult Remove(Nfs3FileHandle dirHandle, string name)
        {
            RpcReply reply = DoSend(new RemoveCall(_client.NextTransactionId(), _client.Credentials, dirHandle, name));
            if (reply.Header.IsSuccess)
            {
                return new Nfs3SetAttributesResult(reply.BodyReader);
            }
            else
            {
                throw new RpcException(reply.Header.ReplyHeader);
            }
        }

        public Nfs3ReadDirPlusResult ReadDirPlus(Nfs3FileHandle dir, ulong cookie, byte[] cookieVerifier, uint dirCount, uint maxCount)
        {
            RpcReply reply = DoSend(new ReadDirPlusCall(_client.NextTransactionId(), _client.Credentials, dir, cookie, cookieVerifier, dirCount, maxCount));
            if (reply.Header.IsSuccess)
            {
                return new Nfs3ReadDirPlusResult(reply.BodyReader);
            }
            else
            {
                throw new RpcException(reply.Header.ReplyHeader);
            }
        }

        private class SetAttributesCall : RpcCall
        {
            private Nfs3FileHandle _handle;
            private Nfs3SetAttributes _setAttrs;

            public SetAttributesCall(uint transaction, RpcCredentials credentials, Nfs3FileHandle fileHandle, Nfs3SetAttributes setAttrs)
                : base(transaction, credentials, ProgramIdentifier, ProgramVersion, 2)
            {
                _handle = fileHandle;
                _setAttrs = setAttrs;
            }

            public override void Write(XdrDataWriter writer)
            {
                base.Write(writer);
                _handle.Write(writer);
                _setAttrs.Write(writer);
                writer.Write(false);
            }
        }

        private class FsInfoCall : RpcCall
        {
            private Nfs3FileHandle _fileHandle;

            public FsInfoCall(uint transaction, RpcCredentials credentials, Nfs3FileHandle fileHandle)
                : base(transaction, credentials, ProgramIdentifier, ProgramVersion, 19)
            {
                _fileHandle = fileHandle;
            }

            public override void Write(XdrDataWriter writer)
            {
                base.Write(writer);
                _fileHandle.Write(writer);
            }
        }

        private class FsInfoReply
        {
            private RpcReplyHeader _header;

            public Nfs3Status Status { get; set; }
            public Nfs3FileAttributes PostOpAttributes { get; set; }
            public Nfs3FileSystemInfo FileSystemInfo { get; set; }

            public FsInfoReply(RpcReply reply)
            {
                _header = reply.Header.ReplyHeader;

                XdrDataReader reader = reply.BodyReader;

                Status = (Nfs3Status)reader.ReadInt32();
                if (reader.ReadBool())
                {
                    PostOpAttributes = new Nfs3FileAttributes(reader);
                }
                if (Status == Nfs3Status.Ok)
                {
                    FileSystemInfo = new Nfs3FileSystemInfo(reader);
                }
            }
        }

        private class LookupCall : RpcCall
        {
            private Nfs3FileHandle _dirHandle;
            private string _name;

            public LookupCall(uint transaction, RpcCredentials credentials, Nfs3FileHandle dirHandle, string name)
                : base(transaction, credentials, ProgramIdentifier, ProgramVersion, 3)
            {
                _dirHandle = dirHandle;
                _name = name;
            }

            public override void Write(XdrDataWriter writer)
            {
                base.Write(writer);
                _dirHandle.Write(writer);
                writer.Write(_name);
            }
        }

        private class AccessCall : RpcCall
        {
            private Nfs3FileHandle _dirHandle;
            private Nfs3AccessPermissions _requested;

            public AccessCall(uint transaction, RpcCredentials credentials, Nfs3FileHandle handle, Nfs3AccessPermissions requested)
                : base(transaction, credentials, ProgramIdentifier, ProgramVersion, 4)
            {
                _dirHandle = handle;
                _requested = requested;
            }

            public override void Write(XdrDataWriter writer)
            {
                base.Write(writer);
                _dirHandle.Write(writer);
                writer.Write((int)_requested);
            }
        }

        private class ReadCall : RpcCall
        {
            private Nfs3FileHandle _dirHandle;
            private long _offset;
            private int _count;

            public ReadCall(uint transaction, RpcCredentials credentials, Nfs3FileHandle handle, long offset, int count)
                : base(transaction, credentials, ProgramIdentifier, ProgramVersion, 6)
            {
                _dirHandle = handle;
                _offset = offset;
                _count = count;
            }

            public override void Write(XdrDataWriter writer)
            {
                base.Write(writer);
                _dirHandle.Write(writer);
                writer.Write(_offset);
                writer.Write(_count);
            }
        }

        private class WriteCall : RpcCall
        {
            private Nfs3FileHandle _dirHandle;
            private long _offset;
            private byte[] _data;
            private int _dataOffset;
            private int _count;

            public WriteCall(uint transaction, RpcCredentials credentials, Nfs3FileHandle handle, long offset, byte[] data, int dataOffset, int count)
                : base(transaction, credentials, ProgramIdentifier, ProgramVersion, 7)
            {
                _dirHandle = handle;
                _offset = offset;
                _data = data;
                _dataOffset = dataOffset;
                _count = count;
            }

            public override void Write(XdrDataWriter writer)
            {
                base.Write(writer);
                _dirHandle.Write(writer);
                writer.Write(_offset);
                writer.Write(_count);
                writer.Write((int)0); // UNSTABLE
                writer.WriteBuffer(_data, _dataOffset, _count);
            }
        }

        private class CreateCall : RpcCall
        {
            private Nfs3FileHandle _dirHandle;
            private string _name;
            private bool _createNew;
            private Nfs3SetAttributes _setAttributes;

            public CreateCall(uint transaction, RpcCredentials credentials, Nfs3FileHandle dirHandle, string name, bool createNew, Nfs3SetAttributes setAttributes)
                : base(transaction, credentials, ProgramIdentifier, ProgramVersion, 8)
            {
                _dirHandle = dirHandle;
                _name = name;
                _createNew = createNew;
                _setAttributes = setAttributes;
            }

            public override void Write(XdrDataWriter writer)
            {
                base.Write(writer);
                _dirHandle.Write(writer);
                writer.Write(_name);
                writer.Write((int)(_createNew ? 1 : 0));
                _setAttributes.Write(writer);
            }
        }

        private class RemoveCall : RpcCall
        {
            private Nfs3FileHandle _dirHandle;
            private string _name;

            public RemoveCall(uint transaction, RpcCredentials credentials, Nfs3FileHandle dirHandle, string name)
                : base(transaction, credentials, ProgramIdentifier, ProgramVersion, 12)
            {
                _dirHandle = dirHandle;
                _name = name;
            }

            public override void Write(XdrDataWriter writer)
            {
                base.Write(writer);
                _dirHandle.Write(writer);
                writer.Write(_name);
            }
        }

        private class ReadDirPlusCall : RpcCall
        {
            private Nfs3FileHandle _dirHandle;
            private ulong _cookie;
            private byte[] _cookieVerifier;
            private uint _dirCount;
            private uint _maxCount;

            public ReadDirPlusCall(uint transaction, RpcCredentials credentials, Nfs3FileHandle dirHandle, ulong cookie, byte[] cookieVerifier, uint dirCount, uint maxCount)
                : base(transaction, credentials, ProgramIdentifier, ProgramVersion, 17)
            {
                _dirHandle = dirHandle;
                _cookie = cookie;
                _cookieVerifier = cookieVerifier;
                _dirCount = dirCount;
                _maxCount = maxCount;
            }

            public override void Write(XdrDataWriter writer)
            {
                base.Write(writer);
                _dirHandle.Write(writer);
                writer.Write(_cookie);
                writer.WriteBytes(_cookieVerifier ?? new byte[Nfs3.CookieVerifierSize]);
                writer.Write(_dirCount);
                writer.Write(_maxCount);
            }
        }

    }
}
