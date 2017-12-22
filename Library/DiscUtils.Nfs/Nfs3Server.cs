//
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
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace DiscUtils.Nfs
{
    public class Nfs3Server
    {
        private const int NfsPort = 111;

        public void Run()
        {
            TcpListener listener = new TcpListener(IPAddress.Any, NfsPort);
            listener.Start();

            while (true)
            {
#if NETSTANDARD1_5
                var socket = listener.AcceptSocketAsync().GetAwaiter().GetResult();
#else
                var socket = listener.AcceptSocket();
#endif
                ClientLoop(socket);
            }
        }

        private void ClientLoop(Socket socket)
        {
            using (socket)
            using (NetworkStream stream = new NetworkStream(socket))
            {
                RpcStreamTransport transport = new RpcStreamTransport(stream);
                ClientLoop(transport);
            }
        }

        public void ClientLoop(IRpcTransport transport)
        {
            while (true)
            {
                // Read the client request
                byte[] message = transport.Receive();

                using (MemoryStream input = new MemoryStream(message))
                using (MemoryStream output = new MemoryStream())
                {
                    XdrDataReader reader = new XdrDataReader(input);

                    var transactionId = reader.ReadUInt32();
                    var messageType = (RpcMessageType)reader.ReadInt32();

                    RpcCallHeader header = new RpcCallHeader(reader);
                    RpcMessageHeader responseHeader = RpcMessageHeader.Accepted(transactionId); ;
                    Nfs3CallResult response = null;

                    if (header.Program == Nfs3Mount.ProgramIdentifier)
                    {
                        switch ((MountProc3)header.Proc)
                        {
                            case MountProc3.Mnt:
                                string dirPath = reader.ReadString();

                                response = Mount(dirPath);
                                break;

                            case MountProc3.Export:

                                response = Exports();
                                break;

                            default:
                                throw new NotImplementedException();
                        }
                    }
                    else
                    {
                        switch (header.Proc)
                        {
                            case NfsProc3.Null:
                                // Nothing to do here.
                                break;

                            case NfsProc3.GetAttr:
                                {
                                    var handle = new Nfs3FileHandle(reader);
                                    reader.ReadBool();

                                    response = GetAttributes(handle);
                                    break;
                                }

                            case NfsProc3.SetAttr:
                                {
                                    var handle = new Nfs3FileHandle(reader);
                                    var newAttributes = new Nfs3FileAttributes(reader);
                                    reader.ReadBool();

                                    response = SetAttributes(handle, newAttributes);
                                    break;
                                }

                            case NfsProc3.Lookup:
                                {
                                    var handle = new Nfs3FileHandle(reader);
                                    var name = reader.ReadString();

                                    response = Lookup(handle, name);
                                    break;
                                }

                            case NfsProc3.Access:
                                {
                                    var handle = new Nfs3FileHandle(reader);
                                    var requested = (Nfs3AccessPermissions)reader.ReadInt32();

                                    response = Access(handle, requested);
                                    break;
                                }

                            case NfsProc3.Read:
                                {
                                    var handle = new Nfs3FileHandle(reader);
                                    var position = reader.ReadInt64();
                                    var count = reader.ReadInt32();

                                    response = Read(handle, position, count);
                                    break;
                                }

                            case NfsProc3.Write:
                                {
                                    var handle = new Nfs3FileHandle(reader);
                                    var position = reader.ReadInt64();
                                    var count = reader.ReadInt32();
                                    var howRead = (Nfs3StableHow)reader.ReadInt32();
                                    var buffer = reader.ReadBuffer();

                                    response = Write(handle, position, buffer, count);
                                    break;
                                }

                            case NfsProc3.Create:
                                {
                                    var dirHandle = new Nfs3FileHandle(reader);
                                    var name = reader.ReadString();
                                    var createNew = reader.ReadInt32() == 1 ? true : false;
                                    var attributes = new Nfs3SetAttributes(reader);

                                    response = Create(dirHandle, name, createNew, attributes);
                                    break;
                                }

                            case NfsProc3.Mkdir:
                                {
                                    var dirHandle = new Nfs3FileHandle(reader);
                                    var name = reader.ReadString();
                                    var attributes = new Nfs3SetAttributes(reader);

                                    response = MakeDirectory(dirHandle, name, attributes);
                                    break;
                                }

                            case NfsProc3.Remove:
                                {
                                    var dirHandle = new Nfs3FileHandle(reader);
                                    var name = reader.ReadString();

                                    response = Remove(dirHandle, name);
                                    break;
                                }

                            case NfsProc3.Rmdir:
                                {
                                    var dirHandle = new Nfs3FileHandle(reader);
                                    var name = reader.ReadString();

                                    response = RemoveDirectory(dirHandle, name);
                                    break;
                                }

                            case NfsProc3.Rename:
                                {
                                    var fromDirHandle = new Nfs3FileHandle(reader);
                                    var fromName = reader.ReadString();
                                    var toDirHandle = new Nfs3FileHandle(reader);
                                    var toName = reader.ReadString();

                                    response = Rename(fromDirHandle, fromName, toDirHandle, toName);
                                    break;
                                }

                            case NfsProc3.Readdirplus:
                                {
                                    var dir = new Nfs3FileHandle(reader);
                                    var cookie = reader.ReadUInt64();
                                    var cookieVerifier = reader.ReadUInt64();
                                    var dirCount = reader.ReadUInt32();
                                    var maxCount = reader.ReadUInt32();

                                    response = ReadDirPlus(dir, cookie, cookieVerifier, dirCount, maxCount);
                                    break;
                                }

                            case NfsProc3.Fsinfo:
                                {
                                    var fileHandle = new Nfs3FileHandle(reader);

                                    response = FileSystemInfo(fileHandle);
                                    break;
                                }

                            case NfsProc3.Fsstat:
                                {
                                    var fileHandle = new Nfs3FileHandle(reader);

                                    response = FileSystemStat(fileHandle);
                                    break;
                                }

                            default:
                                responseHeader = RpcMessageHeader.ProcedureUnavailable(transactionId);
                                break;
                        }
                    }

                    XdrDataWriter writer = new XdrDataWriter(output);

                    responseHeader.Write(writer);

                    if (response != null)
                    {
                        response.Write(writer);
                    }

                    transport.Send(output.ToArray());
                }
            }
        }

        protected virtual Nfs3GetAttributesResult GetAttributes(Nfs3FileHandle handle)
        {
            throw new NotImplementedException();
        }

        protected virtual Nfs3ModifyResult SetAttributes(Nfs3FileHandle handle, Nfs3FileAttributes newAttributes)
        {
            throw new NotImplementedException();
        }

        protected virtual Nfs3LookupResult Lookup(Nfs3FileHandle dir, string name)
        {
            throw new NotImplementedException();
        }

        protected Nfs3AccessResult Access(Nfs3FileHandle handle, Nfs3AccessPermissions requested)
        {
            throw new NotImplementedException();
        }

        protected virtual Nfs3ReadResult Read(Nfs3FileHandle handle, long position, int count)
        {
            throw new NotImplementedException();
        }
        protected virtual Nfs3WriteResult Write(Nfs3FileHandle handle, long position, byte[] buffer, int count)
        {
            throw new NotImplementedException();
        }

        protected virtual Nfs3CreateResult Create(Nfs3FileHandle dirHandle, string name, bool createNew, Nfs3SetAttributes attributes)
        {
            throw new NotImplementedException();
        }

        protected virtual Nfs3CreateResult MakeDirectory(Nfs3FileHandle dirHandle, string name, Nfs3SetAttributes attributes)
        {
            throw new NotImplementedException();
        }

        protected virtual Nfs3ModifyResult Remove(Nfs3FileHandle dirHandle, string name)
        {
            throw new NotImplementedException();
        }

        protected virtual Nfs3ModifyResult RemoveDirectory(Nfs3FileHandle dirHandle, string name)
        {
            throw new NotImplementedException();
        }

        protected virtual Nfs3RenameResult Rename(Nfs3FileHandle fromDirHandle, string fromName, Nfs3FileHandle toDirHandle, string toName)
        {
            throw new NotImplementedException();
        }

        protected virtual Nfs3ReadDirPlusResult ReadDirPlus(Nfs3FileHandle dir, ulong cookie, ulong cookieVerifier, uint dirCount, uint maxCount)
        {
            throw new NotImplementedException();
        }

        protected virtual Nfs3FileSystemInfoResult FileSystemInfo(Nfs3FileHandle fileHandle)
        {
            var _10mb = 10 * 1024 * 1024u;
            var _1kb = 1024u;

            return new Nfs3FileSystemInfoResult()
            {
                FileSystemInfo = new Nfs3FileSystemInfo()
                {
                    DirectoryPreferredBytes = _10mb,
                    MaxFileSize = int.MaxValue,
                    ReadMaxBytes = _10mb,
                    ReadMultipleSize = _1kb,
                    ReadPreferredBytes = _10mb,
                    WriteMaxBytes = _10mb,
                    WriteMultipleSize = _1kb,
                    WritePreferredBytes = _10mb,
                    TimePrecision = Nfs3FileTime.Precision
                }
            };
        }

        protected virtual Nfs3FileSystemStatResult FileSystemStat(Nfs3FileHandle fileHandle)
        {
            throw new NotImplementedException();
        }

        protected virtual Nfs3MountResult Mount(string dirPath)
        {
            throw new NotImplementedException();
        }

        protected virtual Nfs3ExportResult Exports()
        {
            throw new NotImplementedException();
        }
    }
}
