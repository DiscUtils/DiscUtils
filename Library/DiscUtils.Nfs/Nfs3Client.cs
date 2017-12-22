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

using System;
using System.Collections.Generic;
using System.IO;

namespace DiscUtils.Nfs
{
    internal sealed class Nfs3Client : IDisposable
    {
        private readonly Dictionary<Nfs3FileHandle, Nfs3FileAttributes> _cachedAttributes;
        private readonly Dictionary<Nfs3FileHandle, Nfs3FileSystemStat> _cachedStats;
        private readonly Nfs3Mount _mountClient;
        private readonly Nfs3 _nfsClient;

        private IRpcClient _rpcClient;

        public Nfs3Client(string address, RpcCredentials credentials, string mountPoint)
            : this(new RpcClient(address, credentials), mountPoint)
        {
        }

        public Nfs3Client(IRpcClient rpcClient, string mountPoint)
        {
            _rpcClient = rpcClient;
            _mountClient = new Nfs3Mount(_rpcClient);
            RootHandle = _mountClient.Mount(mountPoint).FileHandle;

            _nfsClient = new Nfs3(_rpcClient);

            Nfs3FileSystemInfoResult fsiResult = _nfsClient.FileSystemInfo(RootHandle);
            FileSystemInfo = fsiResult.FileSystemInfo;
            _cachedAttributes = new Dictionary<Nfs3FileHandle, Nfs3FileAttributes>();
            _cachedAttributes[RootHandle] = fsiResult.PostOpAttributes;
            _cachedStats = new Dictionary<Nfs3FileHandle, Nfs3FileSystemStat>();
        }

        public Nfs3FileSystemInfo FileSystemInfo { get; }

        public Nfs3FileHandle RootHandle { get; }

        public void Dispose()
        {
            if (_rpcClient != null)
            {
                _rpcClient.Dispose();
                _rpcClient = null;
            }
        }

        public Nfs3FileAttributes GetAttributes(Nfs3FileHandle handle)
        {
            Nfs3FileAttributes result;
            if (_cachedAttributes.TryGetValue(handle, out result))
            {
                return result;
            }

            Nfs3GetAttributesResult getResult = _nfsClient.GetAttributes(handle);

            if (getResult.Status == Nfs3Status.Ok)
            {
                _cachedAttributes[handle] = getResult.Attributes;
                return getResult.Attributes;
            }
            throw new Nfs3Exception(getResult.Status);
        }

        public void SetAttributes(Nfs3FileHandle handle, Nfs3SetAttributes newAttributes)
        {
            Nfs3ModifyResult result = _nfsClient.SetAttributes(handle, newAttributes);

            _cachedAttributes[handle] = result.CacheConsistency.After;

            if (result.Status != Nfs3Status.Ok)
            {
                throw new Nfs3Exception(result.Status);
            }
        }

        public Nfs3FileHandle Lookup(Nfs3FileHandle dirHandle, string name)
        {
            Nfs3LookupResult result = _nfsClient.Lookup(dirHandle, name);

            if (result.ObjectAttributes != null && result.ObjectHandle != null)
            {
                _cachedAttributes[result.ObjectHandle] = result.ObjectAttributes;
            }

            if (result.DirAttributes != null)
            {
                _cachedAttributes[dirHandle] = result.DirAttributes;
            }

            if (result.Status == Nfs3Status.Ok)
            {
                return result.ObjectHandle;
            }
            if (result.Status == Nfs3Status.NoSuchEntity)
            {
                return null;
            }
            throw new Nfs3Exception(result.Status);
        }

        public Nfs3AccessPermissions Access(Nfs3FileHandle handle, Nfs3AccessPermissions requested)
        {
            Nfs3AccessResult result = _nfsClient.Access(handle, requested);

            if (result.ObjectAttributes != null)
            {
                _cachedAttributes[handle] = result.ObjectAttributes;
            }

            if (result.Status == Nfs3Status.Ok)
            {
                return result.Access;
            }
            throw new Nfs3Exception(result.Status);
        }

        public Nfs3ReadResult Read(Nfs3FileHandle fileHandle, long position, int count)
        {
            Nfs3ReadResult result = _nfsClient.Read(fileHandle, position, count);

            if (result.FileAttributes != null)
            {
                _cachedAttributes[fileHandle] = result.FileAttributes;
            }

            if (result.Status == Nfs3Status.Ok)
            {
                return result;
            }
            throw new Nfs3Exception(result.Status);
        }

        public int Write(Nfs3FileHandle fileHandle, long position, byte[] buffer, int offset, int count)
        {
            Nfs3WriteResult result = _nfsClient.Write(fileHandle, position, buffer, offset, count);

            _cachedAttributes[fileHandle] = result.CacheConsistency.After;

            if (result.Status == Nfs3Status.Ok)
            {
                return result.Count;
            }
            throw new Nfs3Exception(result.Status);
        }

        public Nfs3FileHandle Create(Nfs3FileHandle dirHandle, string name, bool createNew, Nfs3SetAttributes attributes)
        {
            Nfs3CreateResult result = _nfsClient.Create(dirHandle, name, createNew, attributes);

            if (result.Status == Nfs3Status.Ok)
            {
                _cachedAttributes[result.FileHandle] = result.FileAttributes;
                return result.FileHandle;
            }
            throw new Nfs3Exception(result.Status);
        }

        public Nfs3FileHandle MakeDirectory(Nfs3FileHandle dirHandle, string name, Nfs3SetAttributes attributes)
        {
            Nfs3CreateResult result = _nfsClient.MakeDirectory(dirHandle, name, attributes);

            if (result.Status == Nfs3Status.Ok)
            {
                _cachedAttributes[result.FileHandle] = result.FileAttributes;
                return result.FileHandle;
            }
            throw new Nfs3Exception(result.Status);
        }

        public void Remove(Nfs3FileHandle dirHandle, string name)
        {
            Nfs3ModifyResult result = _nfsClient.Remove(dirHandle, name);

            _cachedAttributes[dirHandle] = result.CacheConsistency.After;
            if (result.Status != Nfs3Status.Ok)
            {
                throw new Nfs3Exception(result.Status);
            }
        }

        public void RemoveDirectory(Nfs3FileHandle dirHandle, string name)
        {
            Nfs3ModifyResult result = _nfsClient.RemoveDirectory(dirHandle, name);

            _cachedAttributes[dirHandle] = result.CacheConsistency.After;
            if (result.Status != Nfs3Status.Ok)
            {
                throw new Nfs3Exception(result.Status);
            }
        }

        public void Rename(Nfs3FileHandle fromDirHandle, string fromName, Nfs3FileHandle toDirHandle, string toName)
        {
            Nfs3RenameResult result = _nfsClient.Rename(fromDirHandle, fromName, toDirHandle, toName);

            _cachedAttributes[fromDirHandle] = result.FromDirCacheConsistency.After;
            _cachedAttributes[toDirHandle] = result.ToDirCacheConsistency.After;
            if (result.Status != Nfs3Status.Ok)
            {
                throw new Nfs3Exception(result.Status);
            }
        }

        public Nfs3FileSystemStat FsStat(Nfs3FileHandle handle)
        {
            Nfs3FileSystemStat result;
            if (_cachedStats.TryGetValue(handle, out result))
            {
                //increase caching to at least one second to prevent multiple RPC calls for single Size calculation
                if (result.InvariantUntil > DateTime.Now.AddSeconds(-1))
                    return result;
            }
            Nfs3FileSystemStatResult getResult = _nfsClient.FileSystemStat(handle);
            if (getResult.Status == Nfs3Status.Ok)
            {
                _cachedStats[handle] = getResult.FileSystemStat;
                return getResult.FileSystemStat;
            }
            else
            {
                throw new Nfs3Exception(getResult.Status);
            }
        }

        internal IEnumerable<Nfs3DirectoryEntry> ReadDirectory(Nfs3FileHandle parent, bool silentFail)
        {
            ulong cookie = 0;
            ulong cookieVerifier = 0;

            Nfs3ReadDirPlusResult result;
            do
            {
                result = _nfsClient.ReadDirPlus(parent, cookie, cookieVerifier, FileSystemInfo.DirectoryPreferredBytes,
                    FileSystemInfo.ReadMaxBytes);

                if (result.Status == Nfs3Status.AccessDenied && silentFail)
                {
                    break;
                }
                if (result.Status != Nfs3Status.Ok)
                {
                    throw new Nfs3Exception(result.Status);
                }

                foreach (Nfs3DirectoryEntry entry in result.DirEntries)
                {
                    _cachedAttributes[entry.FileHandle] = entry.FileAttributes;
                    yield return entry;
                    cookie = entry.Cookie;
                }

                cookieVerifier = result.CookieVerifier;
            } while (!result.Eof);
        }
    }
}
