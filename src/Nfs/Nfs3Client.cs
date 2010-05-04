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

using System;
using System.Collections.Generic;

namespace DiscUtils.Nfs
{
    internal sealed class Nfs3Client : IDisposable
    {
        private RpcClient _rpcClient;
        private Nfs3Mount _mountClient;
        private Nfs3 _nfsClient;

        private Nfs3FileHandle _rootHandle;
        private Nfs3FileSystemInfo _fsInfo;
        private Dictionary<Nfs3FileHandle, Nfs3FileAttributes> _cachedAttributes;

        public Nfs3Client(string address, RpcCredentials credentials, string mountPoint)
        {
            _rpcClient = new RpcClient(address, credentials);
            _mountClient = new Nfs3Mount(_rpcClient);
            _rootHandle = _mountClient.Mount(mountPoint).FileHandle;

            _nfsClient = new Nfs3(_rpcClient);

            Nfs3FileSystemInfoResult fsiResult = _nfsClient.FileSystemInfo(_rootHandle);
            _fsInfo = fsiResult.FileSystemInfo;
            _cachedAttributes = new Dictionary<Nfs3FileHandle, Nfs3FileAttributes>();
            _cachedAttributes[_rootHandle] = fsiResult.PostOpAttributes;
        }

        public void Dispose()
        {
            if (_rpcClient != null)
            {
                _rpcClient.Dispose();
                _rpcClient = null;
            }
        }

        public Nfs3FileHandle RootHandle
        {
            get { return _rootHandle; }
        }

        public Nfs3FileSystemInfo FileSystemInfo
        {
            get { return _fsInfo; }
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
            else
            {
                throw new Nfs3Exception(getResult.Status);
            }
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
            else if (result.Status == Nfs3Status.NoSuchEntity)
            {
                return null;
            }
            else
            {
                throw new Nfs3Exception(result.Status);
            }
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
            else
            {
                throw new Nfs3Exception(result.Status);
            }
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
            else
            {
                throw new Nfs3Exception(result.Status);
            }
        }

        public int Write(Nfs3FileHandle fileHandle, long position, byte[] buffer, int offset, int count)
        {
            Nfs3WriteResult result = _nfsClient.Write(fileHandle, position, buffer, offset, count);

            _cachedAttributes[fileHandle] = result.CacheConsistency.After;

            if (result.Status == Nfs3Status.Ok)
            {
                return result.Count;
            }
            else
            {
                throw new Nfs3Exception(result.Status);
            }
        }

        public Nfs3FileHandle Create(Nfs3FileHandle dirHandle, string name, bool createNew, Nfs3SetAttributes attributes)
        {
            Nfs3CreateResult result = _nfsClient.Create(dirHandle, name, createNew, attributes);

            if (result.Status == Nfs3Status.Ok)
            {
                _cachedAttributes[result.FileHandle] = result.FileAttributes;
                return result.FileHandle;
            }
            else
            {
                throw new Nfs3Exception(result.Status);
            }
        }

        public Nfs3FileHandle MakeDirectory(Nfs3FileHandle dirHandle, string name, Nfs3SetAttributes attributes)
        {
            Nfs3CreateResult result = _nfsClient.MakeDirectory(dirHandle, name, attributes);

            if (result.Status == Nfs3Status.Ok)
            {
                _cachedAttributes[result.FileHandle] = result.FileAttributes;
                return result.FileHandle;
            }
            else
            {
                throw new Nfs3Exception(result.Status);
            }
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

        internal IEnumerable<Nfs3DirectoryEntry> ReadDirectory(Nfs3FileHandle parent, bool silentFail)
        {
            ulong cookie = 0;
            byte[] cookieVerifier = null;

            Nfs3ReadDirPlusResult result;
            do
            {
                result = _nfsClient.ReadDirPlus(parent, cookie, cookieVerifier, _fsInfo.DirectoryPreferredBytes, _fsInfo.ReadMaxBytes);

                if (result.Status == Nfs3Status.AccessDenied && silentFail)
                {
                    break;
                }
                else if (result.Status != Nfs3Status.Ok)
                {
                    throw new Nfs3Exception(result.Status);
                }

                foreach(var entry in result.DirEntries)
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
