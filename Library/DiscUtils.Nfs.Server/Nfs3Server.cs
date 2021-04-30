﻿//
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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace DiscUtils.Nfs.Server
{
    public class Nfs3Server : IRpcProgram
    {
        public int ProgramIdentifier => RpcIdentifiers.Nfs3ProgramIdentifier;

        public int ProgramVersion => RpcIdentifiers.Nfs3ProgramVersion;

        public IEnumerable<int> Procedures
        { get; } = new int[]
        {
            (int)NfsProc3.Null,
            (int)NfsProc3.GetAttr,
            (int)NfsProc3.SetAttr,
            (int)NfsProc3.Lookup,
            (int)NfsProc3.Access,
            // Readlink = 5,
            (int)NfsProc3.Read,
            (int)NfsProc3.Write,
            (int)NfsProc3.Create,
            (int)NfsProc3.Mkdir,
            // Symlink = 10,
            // Mknod = 11,
            (int)NfsProc3.Remove,
            (int)NfsProc3.Rmdir,
            (int)NfsProc3.Rename,
            // Link = 15,
            (int)NfsProc3.ReadDir,
            (int)NfsProc3.ReadDirPlus,
            (int)NfsProc3.Fsstat,
            (int)NfsProc3.Fsinfo,
            (int)NfsProc3.Pathconf,
            (int)NfsProc3.Commit
        };

        public IRpcObject Invoke(RpcCallHeader header, XdrDataReader reader)
        {
            switch ((NfsProc3)header.Proc)
            {
                case NfsProc3.Null:
                    {
                        // Nothing to do here.
                        return null;
                    }

                case NfsProc3.GetAttr:
                    {
                        var handle = new Nfs3FileHandle(reader);

                        return GetAttributes(handle);
                    }

                case NfsProc3.SetAttr:
                    {
                        var handle = new Nfs3FileHandle(reader);
                        var newAttributes = new Nfs3SetAttributes(reader);
                        reader.ReadBool();

                        return SetAttributes(handle, newAttributes);
                    }

                case NfsProc3.Lookup:
                    {
                        var handle = new Nfs3FileHandle(reader);
                        var name = reader.ReadString();

                        return Lookup(handle, name);
                    }

                case NfsProc3.Access:
                    {
                        var handle = new Nfs3FileHandle(reader);
                        var requested = (Nfs3AccessPermissions)reader.ReadInt32();

                        return Access(handle, requested);
                    }

                // NfsProc3.Readlink
                case NfsProc3.Read:
                    {
                        var handle = new Nfs3FileHandle(reader);
                        var position = reader.ReadInt64();
                        var count = reader.ReadInt32();

                        return Read(handle, position, count);
                    }

                case NfsProc3.Write:
                    {
                        var handle = new Nfs3FileHandle(reader);
                        var position = reader.ReadInt64();
                        var count = reader.ReadInt32();
                        var howRead = (Nfs3StableHow)reader.ReadInt32();
                        var buffer = reader.ReadBuffer();

                        return Write(handle, position, buffer, count);
                    }

                case NfsProc3.Create:
                    {
                        // diropargs3 = dir handle + name
                        var dirHandle = new Nfs3FileHandle(reader);
                        var name = reader.ReadString();

                        // createhow3
                        var mode = (Nfs3CreateMode)reader.ReadInt32();

                        Nfs3SetAttributes attributes = null;
                        ulong verifier = 0;

                        switch (mode)
                        {
                            case Nfs3CreateMode.Unchecked:
                            case Nfs3CreateMode.Guarded:
                                attributes = new Nfs3SetAttributes(reader);
                                break;

                            case Nfs3CreateMode.Exclusive:
                                verifier = reader.ReadUInt64();
                                break;

                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        return Create(dirHandle, name, mode, attributes, verifier);
                    }

                case NfsProc3.Mkdir:
                    {
                        var dirHandle = new Nfs3FileHandle(reader);
                        var name = reader.ReadString();
                        var attributes = new Nfs3SetAttributes(reader);

                        return MakeDirectory(dirHandle, name, attributes);
                    }

                // NfsProc3.Symlink = 10,
                // NfsProc3.Mknod = 11,
                case NfsProc3.Remove:
                    {
                        var dirHandle = new Nfs3FileHandle(reader);
                        var name = reader.ReadString();

                        return Remove(dirHandle, name);
                    }

                case NfsProc3.Rmdir:
                    {
                        var dirHandle = new Nfs3FileHandle(reader);
                        var name = reader.ReadString();

                        return RemoveDirectory(dirHandle, name);
                    }

                case NfsProc3.Rename:
                    {
                        var fromDirHandle = new Nfs3FileHandle(reader);
                        var fromName = reader.ReadString();
                        var toDirHandle = new Nfs3FileHandle(reader);
                        var toName = reader.ReadString();

                        return Rename(fromDirHandle, fromName, toDirHandle, toName);
                    }

                // NfsProc3.Link = 15
                case NfsProc3.ReadDir:
                    {
                        var dir = new Nfs3FileHandle(reader);
                        var cookie = reader.ReadUInt64();
                        var cookieVerifier = reader.ReadUInt64();
                        var count = reader.ReadUInt32();

                        return ReadDir(dir, cookie, cookieVerifier, count);
                    }

                case NfsProc3.ReadDirPlus:
                    {
                        var dir = new Nfs3FileHandle(reader);
                        var cookie = reader.ReadUInt64();
                        var cookieVerifier = reader.ReadUInt64();
                        var dirCount = reader.ReadUInt32();
                        var maxCount = reader.ReadUInt32();

                        Console.WriteLine($"ReadDirPlus: dir {dir}, cookie: {cookie}, cookieVerifier: {cookieVerifier}, dirCount: {dirCount}, maxCount: {maxCount}");
                        return ReadDirPlus(dir, cookie, cookieVerifier, dirCount, maxCount);
                    }

                case NfsProc3.Fsstat:
                    {
                        var fileHandle = new Nfs3FileHandle(reader);

                        return FileSystemStat(fileHandle);
                    }

                case NfsProc3.Fsinfo:
                    {
                        var fileHandle = new Nfs3FileHandle(reader);

                        return FileSystemInfo(fileHandle);
                    }

                case NfsProc3.Pathconf:
                    {
                        var fileHandle = new Nfs3FileHandle(reader);

                        return PathConf(fileHandle);
                    }

                case NfsProc3.Commit:
                    {
                        var handle = new Nfs3FileHandle(reader);
                        var offset = reader.ReadInt64();
                        var count = reader.ReadInt32();

                        return Commit(handle, offset, count);
                    }

                default:
                    throw new ArgumentOutOfRangeException(nameof(header));
            }
        }

        protected virtual Nfs3GetAttributesResult GetAttributes(Nfs3FileHandle handle)
        {
            throw new NotImplementedException();
        }

        protected virtual Nfs3ModifyResult SetAttributes(Nfs3FileHandle handle, Nfs3SetAttributes newAttributes)
        {
            throw new NotImplementedException();
        }

        protected virtual Nfs3LookupResult Lookup(Nfs3FileHandle dir, string name)
        {
            throw new NotImplementedException();
        }

        protected virtual Nfs3AccessResult Access(Nfs3FileHandle handle, Nfs3AccessPermissions requested)
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

        protected virtual Nfs3CreateResult Create(Nfs3FileHandle dirHandle, string name, Nfs3CreateMode mode, Nfs3SetAttributes attributes, ulong verifier)
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

        protected virtual Nfs3ReadDirResult ReadDir(Nfs3FileHandle dir, ulong cookie, ulong cookieVerifier, uint count)
        {
            throw new NotImplementedException();
        }

        protected virtual Nfs3ReadDirPlusResult ReadDirPlus(Nfs3FileHandle dir, ulong cookie, ulong cookieVerifier, uint dirCount, uint maxCount)
        {
            throw new NotImplementedException();
        }

        protected virtual Nfs3FileSystemInfoResult FileSystemInfo(Nfs3FileHandle fileHandle)
        {
            throw new NotImplementedException();
        }

        protected virtual Nfs3FileSystemStatResult FileSystemStat(Nfs3FileHandle fileHandle)
        {
            throw new NotImplementedException();
        }

        protected virtual Nfs3PathConfResult PathConf(Nfs3FileHandle handle)
        {
            throw new NotImplementedException();
        }

        protected virtual Nfs3CommitResult Commit(Nfs3FileHandle handle, long offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}