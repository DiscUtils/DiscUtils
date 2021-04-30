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
using System.Linq;

namespace DiscUtils.Nfs.Server
{
    public class Nfs3FileServer : Nfs3Server
    {
        private readonly Dictionary<string, IFileSystem> _mounts;
        private readonly Dictionary<Nfs3FileHandle, FileHandleMapping> _handles = new Dictionary<Nfs3FileHandle, FileHandleMapping>();
        private ulong handleIndex = 0;

        public Dictionary<string, IFileSystem> Mounts { get { return _mounts; } }

        public Nfs3FileServer(Dictionary<string, IFileSystem> mounts)
        {
            _mounts = mounts;
        }

        protected override Nfs3GetAttributesResult GetAttributes(Nfs3FileHandle handle)
        {
            var handleStatus = ValidateFileHandle(handle);

            if (handleStatus != Nfs3Status.Ok)
            {
                return new Nfs3GetAttributesResult()
                {
                    Status = handleStatus
                };
            }

            var path = _handles[handle];
            var fileSystem = path.FileSystem;

            return new Nfs3GetAttributesResult()
            {
                Attributes = GetAttributes(path),
                Status = Nfs3Status.Ok
            };
        }

        protected override Nfs3ModifyResult SetAttributes(Nfs3FileHandle handle, Nfs3SetAttributes newAttributes)
        {
            var handleStatus = ValidateFileHandle(handle);

            if (handleStatus != Nfs3Status.Ok)
            {
                return new Nfs3ModifyResult()
                {
                    Status = handleStatus
                };
            }

            var path = _handles[handle];
            var fileSystem = path.FileSystem;

            // Don't do anything
            return new Nfs3ModifyResult()
            {
                CacheConsistency = new Nfs3WeakCacheConsistency()
                {
                    Before = GetWeakConsistencyAttributes(path),
                    After = GetAttributes(path)
                },
                Status = Nfs3Status.Ok
            };
        }

        protected override Nfs3LookupResult Lookup(Nfs3FileHandle dir, string name)
        {
            var handleStatus = ValidateFileHandle(dir);

            if (handleStatus != Nfs3Status.Ok)
            {
                return new Nfs3LookupResult()
                {
                    Status = handleStatus
                };
            }

            var parentHandle = _handles[dir];
            var fileSystem = parentHandle.FileSystem;
            var childPath = Path.Combine(parentHandle.Path, name);

            var childHandle = GetHandle(fileSystem, childPath);
            var child = _handles[childHandle];

            if (fileSystem.FileExists(childPath) || fileSystem.DirectoryExists(childPath))
            {
                return new Nfs3LookupResult()
                {
                    DirAttributes = GetAttributes(parentHandle),
                    ObjectAttributes = GetAttributes(child),
                    ObjectHandle = childHandle,
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

        protected override Nfs3AccessResult Access(Nfs3FileHandle handle, Nfs3AccessPermissions requested)
        {
            var handleStatus = ValidateFileHandle(handle);

            if (handleStatus != Nfs3Status.Ok)
            {
                return new Nfs3AccessResult()
                {
                    Status = handleStatus
                };
            }

            var path = _handles[handle];
            var fileSystem = path.FileSystem;

            var possiblePermissions = Nfs3AccessPermissions.None;

            if (fileSystem.DirectoryExists(path.Path))
            {
                // The execute permission doesn't exist for files.
                possiblePermissions = Nfs3AccessPermissions.All & ~Nfs3AccessPermissions.Execute;
            }
            else if (fileSystem.FileExists(path.Path))
            {
                // The lookup permission doesn't exist for files.
                possiblePermissions = Nfs3AccessPermissions.All & ~Nfs3AccessPermissions.Lookup;
            }
            else
            {
                return new Nfs3AccessResult()
                {
                    Status = Nfs3Status.NoSuchEntity
                };
            }

            var permissions = requested & possiblePermissions;

            return new Nfs3AccessResult()
            {
                Status = Nfs3Status.Ok,
                ObjectAttributes = GetAttributes(path),
                Access = permissions
            };
        }

        protected override Nfs3ReadResult Read(Nfs3FileHandle handle, long position, int count)
        {
            var handleStatus = ValidateFileHandle(handle);

            if (handleStatus != Nfs3Status.Ok)
            {
                return new Nfs3ReadResult()
                {
                    Status = handleStatus
                };
            }

            var path = _handles[handle];
            var fileSystem = path.FileSystem;
            byte[] buffer = new byte[count];
            int read = 0;

            using (Stream stream = fileSystem.OpenFile(path.Path, FileMode.Open, FileAccess.Read))
            {
                stream.Seek(position, SeekOrigin.Begin);
                read = stream.Read(buffer, 0, count);
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
            var handleStatus = ValidateFileHandle(handle);

            if (handleStatus != Nfs3Status.Ok)
            {
                return new Nfs3WriteResult()
                {
                    Status = handleStatus
                };
            }

            var path = _handles[handle];
            var fileSystem = path.FileSystem;

            var before = GetWeakConsistencyAttributes(path);

            using (Stream stream = fileSystem.OpenFile(path.Path, FileMode.Open, FileAccess.Write))
            {
                stream.Position = position;
                stream.Write(buffer, 0, count);
            }

            return new Nfs3WriteResult()
            {
                Count = count,
                CacheConsistency = new Nfs3WeakCacheConsistency()
                {
                    Before = before,
                    After = GetAttributes(path)
                },
                HowCommitted = Nfs3StableHow.DataSync,
                WriteVerifier = 0,
                Status = Nfs3Status.Ok
            };
        }

        protected override Nfs3CreateResult Create(Nfs3FileHandle dirHandle, string name, Nfs3CreateMode mode, Nfs3SetAttributes attributes, ulong verifier)
        {
            if (mode == Nfs3CreateMode.Exclusive)
            {
                return new Nfs3CreateResult()
                {
                    Status = Nfs3Status.NotSupported,
                    CacheConsistency = new Nfs3WeakCacheConsistency()
                };
            }

            var handleStatus = ValidateFileHandle(dirHandle);

            if (handleStatus != Nfs3Status.Ok)
            {
                return new Nfs3CreateResult()
                {
                    Status = handleStatus
                };
            }

            var parentPath = _handles[dirHandle];
            var fileSystem = parentPath.FileSystem;

            if (!fileSystem.DirectoryExists(parentPath.Path))
            {
                return new Nfs3CreateResult()
                {
                    Status = Nfs3Status.NotDirectory,
                    CacheConsistency = new Nfs3WeakCacheConsistency()
                };
            }

            var childPath = Path.Combine(parentPath.Path, name);
            var childHandle = GetHandle(fileSystem, childPath);
            var child = _handles[childHandle];
            var before = GetWeakConsistencyAttributes(parentPath);

            if (fileSystem.FileExists(childPath) || fileSystem.DirectoryExists(childPath))
            {
                return new Nfs3CreateResult()
                {
                    Status = Nfs3Status.FileExists,
                    CacheConsistency = new Nfs3WeakCacheConsistency()
                    {
                        Before = before,
                        After = GetAttributes(parentPath)
                    }
                };
            }

            using (fileSystem.OpenFile(childPath, FileMode.CreateNew, FileAccess.ReadWrite))
            {
            }

            return new Nfs3CreateResult()
            {
                Status = Nfs3Status.Ok,
                FileAttributes = GetAttributes(child),
                FileHandle = childHandle,
                CacheConsistency = new Nfs3WeakCacheConsistency()
                {
                    Before = before,
                    After = GetAttributes(parentPath)
                }
            };
        }

        protected override Nfs3CreateResult MakeDirectory(Nfs3FileHandle dirHandle, string name, Nfs3SetAttributes attributes)
        {
            var handleStatus = ValidateFileHandle(dirHandle);

            if (handleStatus != Nfs3Status.Ok)
            {
                return new Nfs3CreateResult()
                {
                    Status = handleStatus
                };
            }

            var parentPath = _handles[dirHandle];
            var fileSystem = parentPath.FileSystem;
            var newPath = Path.Combine(parentPath.Path, name);

            var before = GetWeakConsistencyAttributes(parentPath);
            fileSystem.CreateDirectory(newPath);
            var childHandle = GetHandle(fileSystem, newPath);
            var child = _handles[childHandle];

            return new Nfs3CreateResult()
            {
                CacheConsistency = new Nfs3WeakCacheConsistency()
                {
                    Before = before,
                    After = GetAttributes(parentPath)
                },
                FileAttributes = GetAttributes(child),
                FileHandle = childHandle,
                Status = Nfs3Status.Ok
            };
        }

        protected override Nfs3ModifyResult Remove(Nfs3FileHandle dirHandle, string name)
        {
            var handleStatus = ValidateFileHandle(dirHandle);

            if (handleStatus != Nfs3Status.Ok)
            {
                return new Nfs3ModifyResult()
                {
                    Status = handleStatus
                };
            }

            var parentPath = _handles[dirHandle];
            var fileSystem = parentPath.FileSystem;
            var childPath = Path.Combine(parentPath.Path, name);
            var before = GetWeakConsistencyAttributes(parentPath);

            if (!fileSystem.FileExists(childPath))
            {
                return new Nfs3ModifyResult()
                {
                    Status = Nfs3Status.NoSuchEntity,
                    CacheConsistency = new Nfs3WeakCacheConsistency()
                    {
                        Before = before,
                        After = GetAttributes(parentPath)
                    }
                };
            }

            fileSystem.DeleteFile(childPath);

            return new Nfs3ModifyResult()
            {
                CacheConsistency = new Nfs3WeakCacheConsistency()
                {
                    Before = before,
                    After = GetAttributes(parentPath)
                },
                Status = Nfs3Status.Ok
            };
        }

        protected override Nfs3ModifyResult RemoveDirectory(Nfs3FileHandle dirHandle, string name)
        {
            var handleStatus = ValidateFileHandle(dirHandle);

            if (handleStatus != Nfs3Status.Ok)
            {
                return new Nfs3ModifyResult()
                {
                    CacheConsistency = new Nfs3WeakCacheConsistency(),
                    Status = handleStatus
                };
            }

            var parentPath = _handles[dirHandle];
            var fileSystem = parentPath.FileSystem;
            var childPath = Path.Combine(parentPath.Path, name);
            var before = GetWeakConsistencyAttributes(parentPath);

            if (!fileSystem.DirectoryExists(childPath))
            {
                return new Nfs3ModifyResult()
                {
                    CacheConsistency = new Nfs3WeakCacheConsistency()
                    {
                        Before = before,
                        After = GetAttributes(parentPath)
                    },
                    Status = Nfs3Status.NoSuchEntity
                };
            }

            fileSystem.DeleteDirectory(childPath, recursive: true);

            return new Nfs3ModifyResult()
            {
                CacheConsistency = new Nfs3WeakCacheConsistency()
                {
                    Before = before,
                    After = GetAttributes(parentPath)
                },
                Status = Nfs3Status.Ok
            };
        }

        protected override Nfs3RenameResult Rename(Nfs3FileHandle fromDirHandle, string fromName, Nfs3FileHandle toDirHandle, string toName)
        {
            if (!_handles.ContainsKey(fromDirHandle) || !_handles.ContainsKey(toDirHandle))
            {
                return new Nfs3RenameResult()
                {
                    FromDirCacheConsistency = new Nfs3WeakCacheConsistency(),
                    ToDirCacheConsistency = new Nfs3WeakCacheConsistency(),
                    Status = Nfs3Status.BadFileHandle
                };
            }

            var fromDir = _handles[fromDirHandle];
            var toDir = _handles[toDirHandle];

            if (fromDir.FileSystem != toDir.FileSystem)
            {
                return new Nfs3RenameResult()
                {
                    FromDirCacheConsistency = new Nfs3WeakCacheConsistency(),
                    ToDirCacheConsistency = new Nfs3WeakCacheConsistency(),
                    Status = Nfs3Status.AttemptedCrossDeviceHardLink
                };
            }

            var fileSystem = toDir.FileSystem;
            var fromPath = Path.Combine(fromDir.Path, fromName);
            var toPath = Path.Combine(toDir.Path, toName);

            if (!fileSystem.DirectoryExists(toDir.Path) || !fileSystem.DirectoryExists(fromDir.Path))
            {
                return new Nfs3RenameResult()
                {
                    FromDirCacheConsistency = new Nfs3WeakCacheConsistency(),
                    ToDirCacheConsistency = new Nfs3WeakCacheConsistency(),
                    Status = Nfs3Status.StaleFileHandle
                };
            }

            var fromBefore = GetWeakConsistencyAttributes(fromDir);
            var toBefore = GetWeakConsistencyAttributes(toDir);

            if (fileSystem.Exists(toPath))
            {
                return new Nfs3RenameResult()
                {
                    FromDirCacheConsistency = new Nfs3WeakCacheConsistency()
                    {
                        Before = fromBefore,
                        After = GetAttributes(fromDir),
                    },
                    ToDirCacheConsistency = new Nfs3WeakCacheConsistency()
                    {
                        Before = toBefore,
                        After = GetAttributes(toDir)
                    },
                    Status = Nfs3Status.FileExists
                };
            }

            if (fileSystem.FileExists(fromPath))
            {
                fileSystem.MoveFile(fromPath, toPath);

                return new Nfs3RenameResult()
                {
                    FromDirCacheConsistency = new Nfs3WeakCacheConsistency()
                    {
                        Before = fromBefore,
                        After = GetAttributes(fromDir),
                    },
                    ToDirCacheConsistency = new Nfs3WeakCacheConsistency()
                    {
                        Before = toBefore,
                        After = GetAttributes(toDir)
                    },
                    Status = Nfs3Status.Ok
                };
            }
            else if (fileSystem.DirectoryExists(fromPath))
            {
                fileSystem.MoveDirectory(fromPath, toPath);

                return new Nfs3RenameResult()
                {
                    FromDirCacheConsistency = new Nfs3WeakCacheConsistency()
                    {
                        Before = fromBefore,
                        After = GetAttributes(fromDir),
                    },
                    ToDirCacheConsistency = new Nfs3WeakCacheConsistency()
                    {
                        Before = toBefore,
                        After = GetAttributes(toDir)
                    },
                    Status = Nfs3Status.Ok
                };
            }
            else
            {
                return new Nfs3RenameResult()
                {
                    FromDirCacheConsistency = new Nfs3WeakCacheConsistency()
                    {
                        Before = fromBefore,
                        After = GetAttributes(fromDir),
                    },
                    ToDirCacheConsistency = new Nfs3WeakCacheConsistency()
                    {
                        Before = toBefore,
                        After = GetAttributes(toDir)
                    },
                    Status = Nfs3Status.NoSuchEntity
                };
            }
        }

        protected override Nfs3ReadDirResult ReadDir(Nfs3FileHandle dir, ulong cookie, ulong cookieVerifier, uint count)
        {
            var handleStatus = ValidateFileHandle(dir);

            if (handleStatus != Nfs3Status.Ok)
            {
                return new Nfs3ReadDirResult()
                {
                    Status = handleStatus
                };
            }

            var directory = _handles[dir];
            var fileSystem = directory.FileSystem;

            List<Nfs3DirectoryEntry> dirEntries = new List<Nfs3DirectoryEntry>();

            var entries = fileSystem.GetFiles(directory.Path);

            var result = new Nfs3ReadDirResult()
            {
                CookieVerifier = (ulong)entries.Length,
                DirAttributes = GetAttributes(directory),
                DirEntries = dirEntries,
                Status = Nfs3Status.Ok,
                Eof = true
            };

            var index = (int)cookie;
            var size = result.GetSize();

            for (; index < entries.Length; index++)
            {
                var file = entries[index];
                var fileHandle = GetHandle(fileSystem, file);
                var fileMapping = _handles[fileHandle];

                var entry = new Nfs3DirectoryEntry()
                {
                    FileId = BitConverter.ToUInt64(fileHandle.Value, 0),
                    Name = Path.GetFileName(file),
                    Cookie = (ulong)index,
                    FileAttributes = GetAttributes(fileMapping),
                    FileHandle = fileHandle,
                };

                var entrySize = (int)entry.GetSize();

                if (size + entrySize > count)
                {
                    break;
                }
                else
                {
                    size += entrySize;
                    dirEntries.Add(entry);
                }
            }

            result.Eof = index == entries.Length;
            return result;
        }

        protected override Nfs3ReadDirPlusResult ReadDirPlus(Nfs3FileHandle dir, ulong cookie, ulong cookieVerifier, uint dirCount, uint maxCount)
        {
            var handleStatus = ValidateFileHandle(dir);

            if (handleStatus != Nfs3Status.Ok)
            {
                return new Nfs3ReadDirPlusResult()
                {
                    Status = handleStatus
                };
            }

            var directory = _handles[dir];
            var fileSystem = directory.FileSystem;
            List<Nfs3DirectoryEntry> dirEntries = new List<Nfs3DirectoryEntry>();

            var entries = fileSystem.GetFileSystemEntries(directory.Path);

            var index = (int)cookie;

            var result = new Nfs3ReadDirPlusResult()
            {
                CookieVerifier = (ulong)entries.Length,
                Eof = true,
                DirAttributes = GetAttributes(directory),
                DirEntries = dirEntries,
                Status = Nfs3Status.Ok
            };

            var dirSize = 0;
            var maxSize = result.GetSize();

            for (; index < entries.Length; index++)
            {
                var file = entries[index];
                var fileHandle = GetHandle(fileSystem, file);
                var fileMapping = _handles[fileHandle];

                var entry = new Nfs3DirectoryEntry()
                {
                    Cookie = (ulong)index,
                    FileAttributes = GetAttributes(fileMapping),
                    FileHandle = fileHandle,
                    FileId = BitConverter.ToUInt64(fileHandle.Value, 0),
                    Name = Path.GetFileName(file)
                };

                var entrySize = (int)entry.GetSize();

                if (dirSize + entrySize > dirCount || maxSize + entrySize > maxCount)
                {
                    break;
                }
                else
                {
                    dirSize += entrySize;
                    maxSize += entrySize;
                    dirEntries.Add(entry);
                }
            }

            result.Eof = index == entries.Length;
            return result;
        }

        protected override Nfs3FileSystemInfoResult FileSystemInfo(Nfs3FileHandle fileHandle)
        {
            var handleStatus = ValidateFileHandle(fileHandle);

            if (handleStatus != Nfs3Status.Ok)
            {
                return new Nfs3FileSystemInfoResult()
                {
                    Status = handleStatus
                };
            }

            var path = _handles[fileHandle];

            var _10mb = 10 * 1024 * 1024u;
            var _100kb = 100 * 1024u;

            return new Nfs3FileSystemInfoResult()
            {
                FileSystemInfo = new Nfs3FileSystemInfo()
                {
                    DirectoryPreferredBytes = _10mb,
                    MaxFileSize = int.MaxValue,
                    ReadMaxBytes = _10mb,
                    ReadMultipleSize = _100kb,
                    ReadPreferredBytes = _10mb,
                    WriteMaxBytes = _10mb,
                    WriteMultipleSize = _100kb,
                    WritePreferredBytes = _10mb,
                    TimePrecision = Nfs3FileTime.Precision
                },
                PostOpAttributes = GetAttributes(path),
                Status = Nfs3Status.Ok
            };
        }

        protected override Nfs3FileSystemStatResult FileSystemStat(Nfs3FileHandle fileHandle)
        {
            var handleStatus = ValidateFileHandle(fileHandle);

            if (handleStatus != Nfs3Status.Ok)
            {
                return new Nfs3FileSystemStatResult()
                {
                    Status = handleStatus
                };
            }

            var path = _handles[fileHandle];
            var fileSystem = path.FileSystem;

            return new Nfs3FileSystemStatResult()
            {
                FileSystemStat = new Nfs3FileSystemStat()
                {
                    AvailableFreeFileSlotCount = (ulong)(int.MaxValue - _handles.Count),
                    AvailableFreeSpaceBytes = (ulong)fileSystem.AvailableSpace,
                    FileSlotCount = int.MaxValue,
                    FreeFileSlotCount = (ulong)(int.MaxValue - _handles.Count),
                    FreeSpaceBytes = (ulong)fileSystem.AvailableSpace,
                    Invariant = TimeSpan.FromMilliseconds(1),
                    TotalSizeBytes = (ulong)fileSystem.Size
                },
                PostOpAttributes = GetAttributes(path),
                Status = Nfs3Status.Ok
            };
        }

        protected override Nfs3PathConfResult PathConf(Nfs3FileHandle handle)
        {
            var handleStatus = ValidateFileHandle(handle);

            if (handleStatus != Nfs3Status.Ok)
            {
                return new Nfs3PathConfResult()
                {
                    Status = handleStatus
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

        protected override Nfs3CommitResult Commit(Nfs3FileHandle handle, long offset, int count)
        {
            var handleStatus = ValidateFileHandle(handle);

            if (handleStatus != Nfs3Status.Ok)
            {
                return new Nfs3CommitResult()
                {
                    CacheConsistency = new Nfs3WeakCacheConsistency(),
                    Status = handleStatus
                };
            }

            var path = _handles[handle];
            var fileSystem = path.FileSystem;

            if (!fileSystem.FileExists(path.Path))
            {
                return new Nfs3CommitResult()
                {
                    Status = Nfs3Status.NoSuchEntity,
                    CacheConsistency = new Nfs3WeakCacheConsistency()
                };
            }

            return new Nfs3CommitResult()
            {
                Status = Nfs3Status.Ok,
                CacheConsistency = new Nfs3WeakCacheConsistency()
                {
                    Before = GetWeakConsistencyAttributes(path),
                    After = GetAttributes(path)
                },
                WriteVerifier = 0
            };
        }

        public Nfs3FileHandle GetHandle(IFileSystem fileSystem, string path)
        {
            if (!_handles.Values.Any(v => v.FileSystem == fileSystem && v.Path == path))
            {
                _handles.Add(NextHandle(), new FileHandleMapping()
                {
                    FileSystem = fileSystem,
                    Path = path
                });
            }

            return _handles.Single(v => v.Value.FileSystem == fileSystem && v.Value.Path == path).Key;
        }

        private Nfs3FileHandle NextHandle()
        {
            handleIndex++;
            return new Nfs3FileHandle()
            {
                Value = BitConverter.GetBytes(handleIndex)
            };
        }

        private Nfs3FileAttributes GetAttributes(FileHandleMapping path)
        {
            Nfs3FileAttributes attributes = new Nfs3FileAttributes();
            var fileSystem = path.FileSystem;

            DiscFileSystemInfo fsi = null;

            if (fileSystem.FileExists(path.Path))
            {
                var info = fileSystem.GetFileInfo(path.Path);

                attributes.BytesUsed = info.Length;
                attributes.Size = info.Length;
                attributes.Type = Nfs3FileType.File;

                fsi = info;
            }
            else if (fileSystem.DirectoryExists(path.Path))
            {
                var info = fileSystem.GetDirectoryInfo(path.Path);

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
            attributes.FileId = BitConverter.ToUInt64(GetHandle(path.FileSystem, fsi.FullName).Value, 0);
            attributes.FileSystemId = 0;
            attributes.Gid = 0;
            attributes.Uid = 0;
            attributes.LinkCount = 1;
            attributes.Mode = UnixFilePermissions.OwnerRead | UnixFilePermissions.OwnerWrite;
            attributes.ModifyTime = new Nfs3FileTime(fsi.LastWriteTime);
            attributes.RdevMajor = 0;
            attributes.RdevMinor = 0;

            return attributes;
        }

        private Nfs3Status ValidateFileHandle(Nfs3FileHandle handle)
        {
            if (!_handles.ContainsKey(handle))
            {
                return Nfs3Status.BadFileHandle;
            }

            var path = _handles[handle];
            var fileSystem = path.FileSystem;

            if (!fileSystem.Exists(path.Path))
            {
                return Nfs3Status.StaleFileHandle;
            }

            return Nfs3Status.Ok;
        }

        private Nfs3WeakCacheConsistencyAttr GetWeakConsistencyAttributes(FileHandleMapping path)
        {
            var fileSystem = path.FileSystem;

            if (!fileSystem.Exists(path.Path))
            {
                return null;
            }

            var info = fileSystem.GetFileSystemInfo(path.Path);
            var attr = new Nfs3WeakCacheConsistencyAttr()
            {
                ChangeTime = new Nfs3FileTime(info.LastWriteTime),
                ModifyTime = new Nfs3FileTime(info.LastWriteTime),
            };

            if (fileSystem.FileExists(path.Path))
            {
                attr.Size = fileSystem.GetFileLength(path.Path);
            }

            return attr;
        }
    }
}