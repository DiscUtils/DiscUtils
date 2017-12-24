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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace DiscUtils.Nfs
{
    public class Nfs3Server : IRpcProgram
    {
        public int ProgramIdentifier => Nfs3.ProgramIdentifier;

        public int ProgramVersion => Nfs3.ProgramVersion;

        public IEnumerable<int> Procedures
        { get; } = new int[]
        {
            (int)NfsProc3.Null,
            (int)NfsProc3.GetAttr,
            (int)NfsProc3.SetAttr,
            (int)NfsProc3.Lookup,
            (int)NfsProc3.Access,
            (int)NfsProc3.Read,
            (int)NfsProc3.Write,
            (int)NfsProc3.Create,
            (int)NfsProc3.Mkdir,
            (int)NfsProc3.Remove,
            (int)NfsProc3.Rmdir,
            (int)NfsProc3.Rename,
            // (int)NfsProc3.ReadDir,
            (int)NfsProc3.ReadDirPlus,
            (int)NfsProc3.Fsinfo,
            (int)NfsProc3.Fsstat,
            (int)NfsProc3.Pathconf,
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
                        var newAttributes = new Nfs3FileAttributes(reader);
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
                        var dirHandle = new Nfs3FileHandle(reader);
                        var name = reader.ReadString();
                        var createNew = reader.ReadInt32() == 1 ? true : false;
                        var attributes = new Nfs3SetAttributes(reader);

                        return Create(dirHandle, name, createNew, attributes);
                    }

                case NfsProc3.Mkdir:
                    {
                        var dirHandle = new Nfs3FileHandle(reader);
                        var name = reader.ReadString();
                        var attributes = new Nfs3SetAttributes(reader);

                        return MakeDirectory(dirHandle, name, attributes);
                    }

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

                        return ReadDirPlus(dir, cookie, cookieVerifier, dirCount, maxCount);
                    }

                case NfsProc3.Fsinfo:
                    {
                        var fileHandle = new Nfs3FileHandle(reader);

                        return FileSystemInfo(fileHandle);
                    }

                case NfsProc3.Fsstat:
                    {
                        var fileHandle = new Nfs3FileHandle(reader);

                        return FileSystemStat(fileHandle);
                    }

                case NfsProc3.Pathconf:
                    {
                        var fileHandle = new Nfs3FileHandle(reader);

                        return PathConf(fileHandle);
                    }

                default:
                    throw new ArgumentOutOfRangeException(nameof(header));
            }
        }

        protected virtual Nfs3ReadDirResult ReadDir(Nfs3FileHandle dir, ulong cookie, ulong cookieVerifier, uint count)
        {
            throw new NotImplementedException();
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

        protected virtual Nfs3PathConfResult PathConf(Nfs3FileHandle handle)
        {
            throw new NotImplementedException();
        }
    }
}
