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
    /// <summary>
    /// NFS status codes.
    /// </summary>
    public enum Nfs3Status {
        /// <summary>
        /// Indicates the call completed successfully.
        /// </summary>
        Ok = 0,

        /// <summary>
        /// The operation was not allowed because the caller is either not a
        /// privileged user (root) or not the owner of the target of the operation.
        /// </summary>
        NotOwner = 1,

        /// <summary>
        /// The file or directory name specified does not exist.
        /// </summary>
        NoSuchEntity = 2,

        /// <summary>
        /// A hard error (for example, a disk error) occurred while processing
        /// the requested operation.
        /// </summary>
        IOError = 5,

        /// <summary>
        /// No such device or address.
        /// </summary>
        NoSuchDeviceOrAddress = 6,

        /// <summary>
        /// The caller does not have the correct permission to perform the requested
        /// operation. Contrast this with NotOwner, which restricts itself to owner
        /// or privileged user permission failures.
        /// </summary>
        AccessDenied = 13,

        /// <summary>
        /// The file specified already exists.
        /// </summary>
        FileExists = 17,

        /// <summary>
        /// Attempt to do a cross-device hard link.
        /// </summary>
        AttemptedCrossDeviceHardLink = 18,

        /// <summary>
        /// No such device.
        /// </summary>
        NoSuchDevice = 19,

        /// <summary>
        /// The caller specified a non-directory in a directory operation.
        /// </summary>
        NotDirectory = 20,

        /// <summary>
        /// The caller specified a directory in a non-directory operation.
        /// </summary>
        IsADirectory = 21,

        /// <summary>
        /// Invalid argument or unsupported argument for an operation.
        /// </summary>
        InvalidArgument = 22,

        /// <summary>
        /// The operation would have caused a file to grow beyond the server's
        /// limit.
        /// </summary>
        FileTooLarge = 27,

        /// <summary>
        /// The operation would have caused the server's file system to exceed its
        /// limit.
        /// </summary>
        NoSpaceAvailable = 28,

        /// <summary>
        /// A modifying operation was attempted on a read-only file system.
        /// </summary>
        ReadOnlyFileSystem = 30,

        /// <summary>
        /// Too many hard links.
        /// </summary>
        TooManyHardLinks = 31,

        /// <summary>
        /// The filename in an operation was too long.
        /// </summary>
        NameTooLong = 63,

        /// <summary>
        /// An attempt was made to remove a directory that was not empty.
        /// </summary>
        DirectoryNotEmpty = 66,

        /// <summary>
        /// The user's resource limit on the server has been exceeded.
        /// </summary>
        QuotaHardLimitExceeded = 69,

        /// <summary>
        /// The file referred to no longer exists or access to it has been revoked.
        /// </summary>
        StaleFileHandle = 70,

        /// <summary>
        /// The file handle given in the arguments referred to a file on a non-local
        /// file system on the server.
        /// </summary>
        TooManyRemoteAccessLevels = 71,

        /// <summary>
        /// The file handle failed internal consistency checks.
        /// </summary>
        BadFileHandle = 10001,

        /// <summary>
        /// Update synchronization mismatch was detected during a SETATTR operation.
        /// </summary>
        UpdateSynchronizationError = 10002,

        /// <summary>
        /// Directory enumeration cookie is stale.
        /// </summary>
        StaleCookie = 10003,

        /// <summary>
        /// Operation is not supported.
        /// </summary>
        NotSupported = 10004,

        /// <summary>
        /// Buffer or request is too small.
        /// </summary>
        TooSmall = 10005,

        /// <summary>
        /// An error occurred on the server which does not map to any of the legal NFS
        /// version 3 protocol error values.
        /// </summary>
        ServerFault = 10006,

        /// <summary>
        /// An attempt was made to create an object of a type not supported by the
        /// server.
        /// </summary>
        BadType = 10007,

        /// <summary>
        /// The server initiated the request, but was not able to complete it in a
        /// timely fashion.
        /// </summary>
        /// <remarks>
        /// The client should wait and then try the request with a new RPC transaction ID.
        /// For example, this error should be returned from a server that supports
        /// hierarchical storage and receives a request to process a file that has been
        /// migrated. In this case, the server should start the immigration process and
        /// respond to client with this error.
        /// </remarks>
        SlowJukebox = 10008,

        /// <summary>
        /// An unknown error occured.
        /// </summary>
        Unknown = -1
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



        public Nfs3GetAttributesResult GetAttributes(Nfs3FileHandle handle)
        {
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, _client.Credentials, 1);
            handle.Write(writer);
            writer.Write(false);

            RpcReply reply = DoSend(ms);
            if (reply.Header.IsSuccess)
            {
                return new Nfs3GetAttributesResult(reply.BodyReader);
            }
            else
            {
                throw new RpcException(reply.Header.ReplyHeader);
            }
        }

        public Nfs3ModifyResult SetAttributes(Nfs3FileHandle handle, Nfs3SetAttributes newAttributes)
        {
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, _client.Credentials, 2);
            handle.Write(writer);
            newAttributes.Write(writer);
            writer.Write(false);

            RpcReply reply = DoSend(ms);
            if (reply.Header.IsSuccess)
            {
                return new Nfs3ModifyResult(reply.BodyReader);
            }
            else
            {
                throw new RpcException(reply.Header.ReplyHeader);
            }
        }

        public Nfs3LookupResult Lookup(Nfs3FileHandle dir, string name)
        {
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, _client.Credentials, 3);
            dir.Write(writer);
            writer.Write(name);

            RpcReply reply = DoSend(ms);
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
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, _client.Credentials, 4);
            handle.Write(writer);
            writer.Write((int)requested);

            RpcReply reply = DoSend(ms);
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
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, _client.Credentials, 6);
            handle.Write(writer);
            writer.Write(position);
            writer.Write(count);

            RpcReply reply = DoSend(ms);
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
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, _client.Credentials, 7);
            handle.Write(writer);
            writer.Write(position);
            writer.Write(count);
            writer.Write((int)0); // UNSTABLE
            writer.WriteBuffer(buffer, bufferOffset, count);

            RpcReply reply = DoSend(ms);
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
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, _client.Credentials, 8);
            dirHandle.Write(writer);
            writer.Write(name);
            writer.Write((int)(createNew ? 1 : 0));
            attributes.Write(writer);

            RpcReply reply = DoSend(ms);
            if (reply.Header.IsSuccess)
            {
                return new Nfs3CreateResult(reply.BodyReader);
            }
            else
            {
                throw new RpcException(reply.Header.ReplyHeader);
            }
        }

        public Nfs3CreateResult MakeDirectory(Nfs3FileHandle dirHandle, string name, Nfs3SetAttributes attributes)
        {
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, _client.Credentials, 9);
            dirHandle.Write(writer);
            writer.Write(name);
            attributes.Write(writer);

            RpcReply reply = DoSend(ms);
            if (reply.Header.IsSuccess)
            {
                return new Nfs3CreateResult(reply.BodyReader);
            }
            else
            {
                throw new RpcException(reply.Header.ReplyHeader);
            }
        }

        public Nfs3ModifyResult Remove(Nfs3FileHandle dirHandle, string name)
        {
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, _client.Credentials, 12);
            dirHandle.Write(writer);
            writer.Write(name);

            RpcReply reply = DoSend(ms);
            if (reply.Header.IsSuccess)
            {
                return new Nfs3ModifyResult(reply.BodyReader);
            }
            else
            {
                throw new RpcException(reply.Header.ReplyHeader);
            }
        }

        public Nfs3ModifyResult RemoveDirectory(Nfs3FileHandle dirHandle, string name)
        {
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, _client.Credentials, 13);
            dirHandle.Write(writer);
            writer.Write(name);

            RpcReply reply = DoSend(ms);
            if (reply.Header.IsSuccess)
            {
                return new Nfs3ModifyResult(reply.BodyReader);
            }
            else
            {
                throw new RpcException(reply.Header.ReplyHeader);
            }
        }

        public Nfs3RenameResult Rename(Nfs3FileHandle fromDirHandle, string fromName, Nfs3FileHandle toDirHandle, string toName)
        {
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, _client.Credentials, 14);
            fromDirHandle.Write(writer);
            writer.Write(fromName);
            toDirHandle.Write(writer);
            writer.Write(toName);

            RpcReply reply = DoSend(ms);
            if (reply.Header.IsSuccess)
            {
                return new Nfs3RenameResult(reply.BodyReader);
            }
            else
            {
                throw new RpcException(reply.Header.ReplyHeader);
            }
        }

        public Nfs3ReadDirPlusResult ReadDirPlus(Nfs3FileHandle dir, ulong cookie, byte[] cookieVerifier, uint dirCount, uint maxCount)
        {
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, _client.Credentials, 17);
            dir.Write(writer);
            writer.Write(cookie);
            writer.WriteBytes(cookieVerifier ?? new byte[Nfs3.CookieVerifierSize]);
            writer.Write(dirCount);
            writer.Write(maxCount);

            RpcReply reply = DoSend(ms);
            if (reply.Header.IsSuccess)
            {
                return new Nfs3ReadDirPlusResult(reply.BodyReader);
            }
            else
            {
                throw new RpcException(reply.Header.ReplyHeader);
            }
        }

        public Nfs3FileSystemInfoResult FileSystemInfo(Nfs3FileHandle fileHandle)
        {
            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = StartCallMessage(ms, _client.Credentials, 19);
            fileHandle.Write(writer);

            RpcReply reply = DoSend(ms);
            if (reply.Header.IsSuccess)
            {
                Nfs3FileSystemInfoResult fsiReply = new Nfs3FileSystemInfoResult(reply.BodyReader);
                if (fsiReply.Status == Nfs3Status.Ok)
                {
                    return fsiReply;
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
    }
}
