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

#if !NET20
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DiscUtils.Nfs
{
    public class Nfs3FileServer : Nfs3Server
    {
        private readonly Dictionary<string, string> _mounts;
        private readonly Dictionary<Nfs3FileHandle, string> _handles = new Dictionary<Nfs3FileHandle, string>();
        private ulong handleIndex = 0;

        public Dictionary<string, string> Mounts { get { return _mounts; } }

        public Nfs3FileServer(Dictionary<string, string> mounts)
        {
            _mounts = mounts;
        }

        protected override Nfs3ReadDirResult ReadDir(Nfs3FileHandle dir, ulong cookie, ulong cookieVerifier, uint count)
        {
            if (!_handles.ContainsKey(dir))
            {
                return new Nfs3ReadDirResult()
                {
                    Status = Nfs3Status.NoSuchEntity
                };
            }

            var directory = _handles[dir];
            List<Nfs3DirectoryEntry> dirEntries = new List<Nfs3DirectoryEntry>();

            foreach (var file in Directory.GetFileSystemEntries(directory))
            {
                dirEntries.Add(new Nfs3DirectoryEntry()
                {
                    FileId = BitConverter.ToUInt64(GetHandle(file).Value, 0),
                    Name = Path.GetFileName(file),
                    Cookie = 0,
                    FileAttributes = null,
                    FileHandle = null,
                });
            }

            return new Nfs3ReadDirResult()
            {
                CookieVerifier = 0,
                DirAttributes = GetAttributes(directory),
                DirEntries = dirEntries,
                Status = Nfs3Status.Ok,
                Eof = true
            };
        }

        protected override Nfs3ReadDirPlusResult ReadDirPlus(Nfs3FileHandle dir, ulong cookie, ulong cookieVerifier, uint dirCount, uint maxCount)
        {
            if (!_handles.ContainsKey(dir))
            {
                return new Nfs3ReadDirPlusResult()
                {
                    Status = Nfs3Status.NoSuchEntity
                };
            }

            var directory = _handles[dir];
            List<Nfs3DirectoryEntry> dirEntries = new List<Nfs3DirectoryEntry>();

            foreach (var file in Directory.GetFileSystemEntries(directory))
            {
                dirEntries.Add(new Nfs3DirectoryEntry()
                {
                    Cookie = 0,
                    FileAttributes = GetAttributes(file),
                    FileHandle = GetHandle(file),
                    FileId = BitConverter.ToUInt64(GetHandle(file).Value, 0),
                    Name = Path.GetFileName(file)
                });
            }

            return new Nfs3ReadDirPlusResult()
            {
                CookieVerifier = 0,
                Eof = true,
                DirAttributes = GetAttributes(directory),
                DirEntries = dirEntries,
                Status = Nfs3Status.Ok
            };
        }

        protected override Nfs3ReadResult Read(Nfs3FileHandle handle, long position, int count)
        {
            if (!_handles.ContainsKey(handle))
            {
                return new Nfs3ReadResult()
                {
                    Status = Nfs3Status.NoSuchEntity
                };
            }

            var path = _handles[handle];
            byte[] buffer = new byte[count];
            int read = 0;

            using (Stream stream = File.OpenRead(path))
            {
                read = stream.Read(buffer, (int)position, count);
            }

            return new Nfs3ReadResult()
            {
                Count = read,
                Data = buffer,
                Eof = true,
                FileAttributes = GetAttributes(path),
                Status = Nfs3Status.Ok
            };
        }

        protected override Nfs3WriteResult Write(Nfs3FileHandle handle, long position, byte[] buffer, int count)
        {
            if (!_handles.ContainsKey(handle))
            {
                return new Nfs3WriteResult()
                {
                    Status = Nfs3Status.NoSuchEntity
                };
            }

            var path = _handles[handle];

            using (Stream stream = File.OpenWrite(path))
            {
                stream.Position = position;
                stream.Write(buffer, 0, count);
            }

            return new Nfs3WriteResult()
            {
                Count = count,
                CacheConsistency = new Nfs3WeakCacheConsistency(),
                HowCommitted = Nfs3StableHow.DataSync,
                WriteVerifier = 0,
                Status = Nfs3Status.Ok
            };
        }

        protected override Nfs3CreateResult MakeDirectory(Nfs3FileHandle dirHandle, string name, Nfs3SetAttributes attributes)
        {
            if (!_handles.ContainsKey(dirHandle))
            {
                return new Nfs3CreateResult()
                {
                    Status = Nfs3Status.NoSuchEntity
                };
            }

            var parentPath = _handles[dirHandle];
            var newPath = Path.Combine(parentPath, name);

            Directory.CreateDirectory(newPath);

            return new Nfs3CreateResult()
            {
                CacheConsistency = new Nfs3WeakCacheConsistency(),
                FileAttributes = GetAttributes(newPath),
                FileHandle = GetHandle(newPath),
                Status = Nfs3Status.Ok
            };
        }

        protected override Nfs3ModifyResult RemoveDirectory(Nfs3FileHandle dirHandle, string name)
        {
            if (!_handles.ContainsKey(dirHandle))
            {
                return new Nfs3ModifyResult()
                {
                    Status = Nfs3Status.NoSuchEntity
                };
            }

            var parentPath = _handles[dirHandle];
            var childPath = Path.Combine(parentPath, name);

            if (!Directory.Exists(childPath))
            {
                return new Nfs3ModifyResult()
                {
                    Status = Nfs3Status.NoSuchEntity
                };
            }

            Directory.Delete(childPath, recursive: true);

            return new Nfs3ModifyResult()
            {
                CacheConsistency = new Nfs3WeakCacheConsistency(),
                Status = Nfs3Status.Ok
            };
        }

        protected override Nfs3CreateResult Create(Nfs3FileHandle dirHandle, string name, bool createNew, Nfs3SetAttributes attributes)
        {
            if (!_handles.ContainsKey(dirHandle))
            {
                return new Nfs3CreateResult()
                {
                    Status = Nfs3Status.NoSuchEntity
                };
            }

            var parentPath = _handles[dirHandle];
            var childPath = Path.Combine(parentPath, name);

            using (File.Create(childPath))
            {
            }

            return new Nfs3CreateResult()
            {
                Status = Nfs3Status.Ok,
                FileAttributes = GetAttributes(childPath),
                FileHandle = GetHandle(childPath),
                CacheConsistency = new Nfs3WeakCacheConsistency()
            };
        }

        protected override Nfs3ModifyResult Remove(Nfs3FileHandle dirHandle, string name)
        {
            if (!_handles.ContainsKey(dirHandle))
            {
                return new Nfs3ModifyResult()
                {
                    Status = Nfs3Status.NoSuchEntity
                };
            }

            var parentPath = _handles[dirHandle];
            var childPath = Path.Combine(parentPath, name);

            if (!File.Exists(childPath))
            {
                return new Nfs3ModifyResult()
                {
                    Status = Nfs3Status.NoSuchEntity
                };
            }

            File.Delete(childPath);

            return new Nfs3ModifyResult()
            {
                CacheConsistency = new Nfs3WeakCacheConsistency(),
                Status = Nfs3Status.Ok
            };
        }

        protected override Nfs3LookupResult Lookup(Nfs3FileHandle dir, string name)
        {
            if (!_handles.ContainsKey(dir))
            {
                return new Nfs3LookupResult()
                {
                    Status = Nfs3Status.NoSuchEntity
                };
            }

            var parentPath = _handles[dir];
            var childPath = Path.Combine(parentPath, name);

            if (File.Exists(childPath) || Directory.Exists(childPath))
            {
                return new Nfs3LookupResult()
                {
                    DirAttributes = GetAttributes(parentPath),
                    ObjectAttributes = GetAttributes(childPath),
                    ObjectHandle = GetHandle(childPath),
                    Status = Nfs3Status.Ok
                };
            }
            else
            {
                return new Nfs3LookupResult()
                {
                    Status = Nfs3Status.NoSuchEntity
                };
            }
        }

        protected override Nfs3GetAttributesResult GetAttributes(Nfs3FileHandle handle)
        {
            if (!_handles.ContainsKey(handle))
            {
                return new Nfs3GetAttributesResult()
                {
                    Status = Nfs3Status.NoSuchEntity
                };
            }

            var path = _handles[handle];

            return new Nfs3GetAttributesResult()
            {
                Attributes = GetAttributes(path),
                Status = Nfs3Status.Ok
            };
        }

        protected override Nfs3ModifyResult SetAttributes(Nfs3FileHandle handle, Nfs3FileAttributes newAttributes)
        {
            if (!_handles.ContainsKey(handle))
            {
                return new Nfs3ModifyResult()
                {
                    Status = Nfs3Status.NoSuchEntity
                };
            }

            // Don't do anything
            return new Nfs3ModifyResult()
            {
                CacheConsistency = new Nfs3WeakCacheConsistency(),
                Status = Nfs3Status.Ok
            };
        }

        protected override Nfs3FileSystemStatResult FileSystemStat(Nfs3FileHandle fileHandle)
        {
            if (!_handles.ContainsKey(fileHandle))
            {
                return new Nfs3FileSystemStatResult()
                {
                    Status = Nfs3Status.NoSuchEntity
                };
            }

            var path = _handles[fileHandle];

            DriveInfo info = new DriveInfo(Path.GetPathRoot(path));

            return new Nfs3FileSystemStatResult()
            {
                FileSystemStat = new Nfs3FileSystemStat()
                {
                    AvailableFreeFileSlotCount = (ulong)(int.MaxValue - _handles.Count),
                    AvailableFreeSpaceBytes = (ulong)info.AvailableFreeSpace,
                    FileSlotCount = int.MaxValue,
                    FreeFileSlotCount = (ulong)(int.MaxValue - _handles.Count),
                    FreeSpaceBytes = (ulong)info.TotalFreeSpace,
                    Invariant = TimeSpan.FromMilliseconds(1),
                    TotalSizeBytes = (ulong)info.TotalSize
                },
                PostOpAttributes = GetAttributes(path),
                Status = Nfs3Status.Ok
            };
        }

        protected override Nfs3PathConfResult PathConf(Nfs3FileHandle handle)
        {
            if (!_handles.ContainsKey(handle))
            {
                return new Nfs3PathConfResult()
                {
                    Status = Nfs3Status.NoSuchEntity
                };
            }

            var path = _handles[handle];

            return new Nfs3PathConfResult()
            {
                Status = Nfs3Status.Ok,
                ObjectAttributes = GetAttributes(path),
                CaseInsensitive = true,
                CasePreserving = false,
                ChownRestricted = true,
                LinkMax = 128,
                NameMax = 128,
                NoTrunc = false
            };
        }

        protected override Nfs3AccessResult Access(Nfs3FileHandle handle, Nfs3AccessPermissions requested)
        {
            if (!_handles.ContainsKey(handle))
            {
                return new Nfs3AccessResult()
                {
                    Status = Nfs3Status.NoSuchEntity
                };
            }

            var path = _handles[handle];

            return new Nfs3AccessResult()
            {
                Status = Nfs3Status.Ok,
                ObjectAttributes = GetAttributes(path),
                Access = Nfs3AccessPermissions.Read | Nfs3AccessPermissions.Delete | Nfs3AccessPermissions.Modify
            };
        }

        public Nfs3FileHandle GetHandle(string path)
        {
            if (!_handles.Values.Any(v => v == path))
            {
                _handles.Add(NextHandle(), path);
            }

            return _handles.Single(h => h.Value == path).Key;
        }

        private Nfs3FileHandle NextHandle()
        {
            handleIndex++;
            return new Nfs3FileHandle()
            {
                Value = BitConverter.GetBytes(handleIndex)
            };
        }

        private Nfs3FileAttributes GetAttributes(string path)
        {
            Nfs3FileAttributes attributes = new Nfs3FileAttributes();

            System.IO.FileSystemInfo fsi = null;

            if (File.Exists(path))
            {
                var info = new FileInfo(path);

                attributes.BytesUsed = info.Length;
                attributes.Size = info.Length;
                attributes.Type = Nfs3FileType.File;

                fsi = info;
            }
            else if (Directory.Exists(path))
            {
                var info = new DirectoryInfo(path);

                attributes.BytesUsed = 0;
                attributes.Size = 0;
                attributes.Type = Nfs3FileType.Directory;

                fsi = info;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(path));
            }

            attributes.AccessTime = new Nfs3FileTime(fsi.LastAccessTime);
            attributes.ChangeTime = new Nfs3FileTime(fsi.LastWriteTime);
            attributes.FileId = BitConverter.ToUInt64(GetHandle(fsi.FullName).Value, 0);
            attributes.FileSystemId = 0;
            attributes.Gid = 0;
            attributes.LinkCount = 0;
            attributes.Mode = UnixFilePermissions.GroupAll | UnixFilePermissions.OthersAll | UnixFilePermissions.OwnerAll;
            attributes.ModifyTime = new Nfs3FileTime(fsi.LastWriteTime);
            attributes.RdevMajor = 0;
            attributes.RdevMinor = 0;
            attributes.Uid = 0;

            return attributes;
        }
    }
}
#endif