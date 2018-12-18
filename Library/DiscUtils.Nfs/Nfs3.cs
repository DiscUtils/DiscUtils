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
    internal sealed class Nfs3 : RpcProgram
    {
        public const int ProgramIdentifier = RpcIdentifiers.Nfs3ProgramIdentifier;
        public const int ProgramVersion = RpcIdentifiers.Nfs3ProgramVersion;

        public const int MaxFileHandleSize = 64;
        public const int CookieVerifierSize = 8;
        public const int CreateVerifierSize = 8;
        public const int WriteVerifierSize = 8;

        public Nfs3(IRpcClient client)
            : base(client) {}

        public override int Identifier
        {
            get { return ProgramIdentifier; }
        }

        public override int Version
        {
            get { return ProgramVersion; }
        }

        public Nfs3GetAttributesResult GetAttributes(Nfs3FileHandle handle)
        {
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, _client.Credentials, NfsProc3.GetAttr);
            handle.Write(writer);
            writer.Write(false);

            RpcReply reply = DoSend(ms);
            if (reply.Header.IsSuccess)
            {
                return new Nfs3GetAttributesResult(reply.BodyReader);
            }
            throw new RpcException(reply.Header.ReplyHeader);
        }

        public Nfs3ModifyResult SetAttributes(Nfs3FileHandle handle, Nfs3SetAttributes newAttributes)
        {
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, _client.Credentials, NfsProc3.SetAttr);
            handle.Write(writer);
            newAttributes.Write(writer);
            writer.Write(false);

            RpcReply reply = DoSend(ms);
            if (reply.Header.IsSuccess)
            {
                return new Nfs3ModifyResult(reply.BodyReader);
            }
            throw new RpcException(reply.Header.ReplyHeader);
        }

        public Nfs3LookupResult Lookup(Nfs3FileHandle dir, string name)
        {
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, _client.Credentials, NfsProc3.Lookup);
            dir.Write(writer);
            writer.Write(name);

            RpcReply reply = DoSend(ms);
            if (reply.Header.IsSuccess)
            {
                return new Nfs3LookupResult(reply.BodyReader);
            }
            throw new RpcException(reply.Header.ReplyHeader);
        }

        public Nfs3AccessResult Access(Nfs3FileHandle handle, Nfs3AccessPermissions requested)
        {
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, _client.Credentials, NfsProc3.Access);
            handle.Write(writer);
            writer.Write((int)requested);

            RpcReply reply = DoSend(ms);
            if (reply.Header.IsSuccess)
            {
                return new Nfs3AccessResult(reply.BodyReader);
            }
            throw new RpcException(reply.Header.ReplyHeader);
        }

        public Nfs3ReadResult Read(Nfs3FileHandle handle, long position, int count)
        {
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, _client.Credentials, NfsProc3.Read);
            handle.Write(writer);
            writer.Write(position);
            writer.Write(count);

            RpcReply reply = DoSend(ms);
            if (reply.Header.IsSuccess)
            {
                return new Nfs3ReadResult(reply.BodyReader);
            }
            throw new RpcException(reply.Header.ReplyHeader);
        }

        public Nfs3WriteResult Write(Nfs3FileHandle handle, long position, byte[] buffer, int bufferOffset, int count)
        {
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, _client.Credentials, NfsProc3.Write);
            handle.Write(writer);
            writer.Write(position);
            writer.Write(count);
            writer.Write((int)Nfs3StableHow.Unstable);
            writer.WriteBuffer(buffer, bufferOffset, count);

            RpcReply reply = DoSend(ms);
            if (reply.Header.IsSuccess)
            {
                return new Nfs3WriteResult(reply.BodyReader);
            }
            throw new RpcException(reply.Header.ReplyHeader);
        }

        public Nfs3CreateResult Create(Nfs3FileHandle dirHandle, string name, bool createNew,
                                       Nfs3SetAttributes attributes)
        {
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, _client.Credentials, NfsProc3.Create);
            dirHandle.Write(writer);
            writer.Write(name);
            writer.Write(createNew ? 1 : 0);
            attributes.Write(writer);

            RpcReply reply = DoSend(ms);
            if (reply.Header.IsSuccess)
            {
                return new Nfs3CreateResult(reply.BodyReader);
            }
            throw new RpcException(reply.Header.ReplyHeader);
        }

        public Nfs3CreateResult MakeDirectory(Nfs3FileHandle dirHandle, string name, Nfs3SetAttributes attributes)
        {
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, _client.Credentials, NfsProc3.Mkdir);
            dirHandle.Write(writer);
            writer.Write(name);
            attributes.Write(writer);

            RpcReply reply = DoSend(ms);
            if (reply.Header.IsSuccess)
            {
                return new Nfs3CreateResult(reply.BodyReader);
            }
            throw new RpcException(reply.Header.ReplyHeader);
        }

        public Nfs3ModifyResult Remove(Nfs3FileHandle dirHandle, string name)
        {
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, _client.Credentials, NfsProc3.Remove);
            dirHandle.Write(writer);
            writer.Write(name);

            RpcReply reply = DoSend(ms);
            if (reply.Header.IsSuccess)
            {
                return new Nfs3ModifyResult(reply.BodyReader);
            }
            throw new RpcException(reply.Header.ReplyHeader);
        }

        public Nfs3ModifyResult RemoveDirectory(Nfs3FileHandle dirHandle, string name)
        {
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, _client.Credentials, NfsProc3.Rmdir);
            dirHandle.Write(writer);
            writer.Write(name);

            RpcReply reply = DoSend(ms);
            if (reply.Header.IsSuccess)
            {
                return new Nfs3ModifyResult(reply.BodyReader);
            }
            throw new RpcException(reply.Header.ReplyHeader);
        }

        public Nfs3RenameResult Rename(Nfs3FileHandle fromDirHandle, string fromName, Nfs3FileHandle toDirHandle,
                                       string toName)
        {
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, _client.Credentials, NfsProc3.Rename);
            fromDirHandle.Write(writer);
            writer.Write(fromName);
            toDirHandle.Write(writer);
            writer.Write(toName);

            RpcReply reply = DoSend(ms);
            if (reply.Header.IsSuccess)
            {
                return new Nfs3RenameResult(reply.BodyReader);
            }
            throw new RpcException(reply.Header.ReplyHeader);
        }

        public Nfs3ReadDirPlusResult ReadDirPlus(Nfs3FileHandle dir, ulong cookie, ulong cookieVerifier, uint dirCount,
                                                 uint maxCount)
        {
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, _client.Credentials, NfsProc3.ReadDirPlus);
            dir.Write(writer);
            writer.Write(cookie);
            writer.Write(cookieVerifier);
            writer.Write(dirCount);
            writer.Write(maxCount);

            RpcReply reply = DoSend(ms);
            if (reply.Header.IsSuccess)
            {
                return new Nfs3ReadDirPlusResult(reply.BodyReader);
            }
            throw new RpcException(reply.Header.ReplyHeader);
        }

        public Nfs3FileSystemInfoResult FileSystemInfo(Nfs3FileHandle fileHandle)
        {
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, _client.Credentials, NfsProc3.Fsinfo);
            fileHandle.Write(writer);

            RpcReply reply = DoSend(ms);
            if (reply.Header.IsSuccess)
            {
                Nfs3FileSystemInfoResult fsiReply = new Nfs3FileSystemInfoResult(reply.BodyReader);
                if (fsiReply.Status == Nfs3Status.Ok)
                {
                    return fsiReply;
                }
                throw new Nfs3Exception(fsiReply.Status);
            }
            throw new RpcException(reply.Header.ReplyHeader);
        }

        public Nfs3FileSystemStatResult FileSystemStat(Nfs3FileHandle fileHandle)
        {
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, _client.Credentials, NfsProc3.Fsstat);
            fileHandle.Write(writer);

            RpcReply reply = DoSend(ms);
            if (reply.Header.IsSuccess)
            {
                Nfs3FileSystemStatResult statReply = new Nfs3FileSystemStatResult(reply.BodyReader);
                if (statReply.Status == Nfs3Status.Ok)
                {
                    return statReply;
                }
                else
                {
                    throw new Nfs3Exception(statReply.Status);
                }
            }
            else
            {
                throw new RpcException(reply.Header.ReplyHeader);
            }
        }
    }
}
