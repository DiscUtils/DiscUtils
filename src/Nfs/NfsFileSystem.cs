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

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DiscUtils.Nfs
{
    public class NfsFileSystem : DiscFileSystem
    {
        private Nfs3Client _client;

        public NfsFileSystem(string address, RpcCredentials credentials, string mountPoint)
            : base(new NfsFileSystemOptions())
        {
            _client = new Nfs3Client(address, credentials, mountPoint);
        }

        public NfsFileSystemOptions NfsOptions
        {
            get { return (NfsFileSystemOptions)Options; }
        }

        public override string FriendlyName
        {
            get { return "NFS"; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override void CopyFile(string sourceFile, string destinationFile, bool overwrite)
        {
            throw new NotImplementedException();
        }

        public override void CreateDirectory(string path)
        {
            throw new NotImplementedException();
        }

        public override void DeleteDirectory(string path)
        {
            Nfs3FileHandle handle = GetFile(path);
            if (handle != null && _client.GetAttributes(handle).Type != Nfs3FileType.Directory)
            {
                throw new DirectoryNotFoundException("No such directory: " + path);
            }

            string[] dirs = Utilities.GetDirectoryFromPath(path).Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            Nfs3FileHandle parent = GetDirectory(_client.RootHandle, dirs);

            if (handle != null)
            {
                _client.Remove(parent, Utilities.GetFileFromPath(path));
            }
        }

        public override void DeleteFile(string path)
        {
            Nfs3FileHandle handle = GetFile(path);
            if (handle != null && _client.GetAttributes(handle).Type == Nfs3FileType.Directory)
            {
                throw new FileNotFoundException("No such file", path);
            }

            string[] dirs = Utilities.GetDirectoryFromPath(path).Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            Nfs3FileHandle parent = GetDirectory(_client.RootHandle, dirs);

            if (handle != null)
            {
                _client.Remove(parent, Utilities.GetFileFromPath(path));
            }
        }

        public override bool DirectoryExists(string path)
        {
            return (GetAttributes(path) & FileAttributes.Directory) != 0;
        }

        public override bool FileExists(string path)
        {
            return (GetAttributes(path) & FileAttributes.Normal) != 0;
        }

        public override string[] GetDirectories(string path, string searchPattern, SearchOption searchOption)
        {
            Regex re = Utilities.ConvertWildcardsToRegEx(searchPattern);

            List<string> dirs = new List<string>();
            DoSearch(dirs, _client.RootHandle, path, re, searchOption == SearchOption.AllDirectories, true, false);
            return dirs.ToArray();
        }

        public override string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
        {
            Regex re = Utilities.ConvertWildcardsToRegEx(searchPattern);

            List<string> results = new List<string>();
            DoSearch(results, _client.RootHandle, path, re, searchOption == SearchOption.AllDirectories, false, true);
            return results.ToArray();
        }

        public override string[] GetFileSystemEntries(string path)
        {
            Regex re = Utilities.ConvertWildcardsToRegEx("*.*");

            List<string> results = new List<string>();
            DoSearch(results, _client.RootHandle, path, re, false, true, true);
            return results.ToArray();
        }

        public override string[] GetFileSystemEntries(string path, string searchPattern)
        {
            Regex re = Utilities.ConvertWildcardsToRegEx(searchPattern);

            List<string> results = new List<string>();
            DoSearch(results, _client.RootHandle, path, re, false, true, true);
            return results.ToArray();
        }

        public override void MoveDirectory(string sourceDirectoryName, string destinationDirectoryName)
        {
            throw new NotImplementedException();
        }

        public override void MoveFile(string sourceName, string destinationName, bool overwrite)
        {
            throw new NotImplementedException();
        }

        public override Stream OpenFile(string path, FileMode mode, FileAccess access)
        {
            Nfs3AccessPermissions requested;
            if (access == FileAccess.Read)
            {
                requested = Nfs3AccessPermissions.Read;
            }
            else if (access == FileAccess.ReadWrite)
            {
                requested = Nfs3AccessPermissions.Read | Nfs3AccessPermissions.Modify;
            }
            else
            {
                requested = Nfs3AccessPermissions.Modify;
            }

            if (mode == FileMode.Create || mode == FileMode.CreateNew || (mode == FileMode.OpenOrCreate && !FileExists(path)))
            {
                string[] dirs = Utilities.GetDirectoryFromPath(path).Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
                Nfs3FileHandle parent = GetDirectory(_client.RootHandle, dirs);

                Nfs3SetAttributes setAttrs = new Nfs3SetAttributes();
                setAttrs.Mode = NfsOptions.NewFilePermissions;
                setAttrs.SetMode = true;
                Nfs3FileHandle handle = _client.Create(parent, Utilities.GetFileFromPath(path), mode != FileMode.Create, setAttrs);

                return new Nfs3FileStream(_client, handle, access);
            }
            else
            {
                Nfs3FileHandle handle = GetFile(path);
                Nfs3AccessPermissions actualPerms = _client.Access(handle, requested);

                if (actualPerms != requested)
                {
                    throw new UnauthorizedAccessException();
                }

                Nfs3FileStream result = new Nfs3FileStream(_client, handle, access);
                if (mode == FileMode.Append)
                {
                    result.Seek(0, SeekOrigin.End);
                }
                else if (mode == FileMode.Truncate)
                {
                    result.SetLength(0);
                }
                return result;
            }
        }

        public override FileAttributes GetAttributes(string path)
        {
            Nfs3FileHandle handle = GetFile(path);
            Nfs3FileAttributes nfsAttrs = _client.GetAttributes(handle);

            FileAttributes result = (FileAttributes)0;
            if (nfsAttrs.Type == Nfs3FileType.Directory)
            {
                result |= FileAttributes.Directory;
            }
            else if (nfsAttrs.Type == Nfs3FileType.BlockDevice || nfsAttrs.Type == Nfs3FileType.CharacterDevice)
            {
                result |= FileAttributes.Device;
            }
            else
            {
                result |= FileAttributes.Normal;
            }

            if (Utilities.GetFileFromPath(path).StartsWith(".", StringComparison.Ordinal))
            {
                result |= FileAttributes.Hidden;
            }

            return result;
        }

        public override void SetAttributes(string path, FileAttributes newValue)
        {
            if (newValue != GetAttributes(path))
            {
                throw new NotSupportedException("Unable to change file attributes over NFS");
            }
        }

        public override DateTime GetCreationTimeUtc(string path)
        {
            // Note creation time is not available, so simulating from last modification time
            Nfs3FileHandle handle = GetFile(path);
            Nfs3FileAttributes attrs = _client.GetAttributes(handle);
            return attrs.ModifyTime.ToDateTime();
        }

        public override void SetCreationTimeUtc(string path, DateTime newTime)
        {
            // No action - creation time is not accessible over NFS
        }

        public override DateTime GetLastAccessTimeUtc(string path)
        {
            Nfs3FileHandle handle = GetFile(path);
            Nfs3FileAttributes attrs = _client.GetAttributes(handle);
            return attrs.AccessTime.ToDateTime();
        }

        public override void SetLastAccessTimeUtc(string path, DateTime newTime)
        {
            Nfs3FileHandle handle = GetFile(path);
            _client.SetAttributes(handle, new Nfs3SetAttributes() { SetAccessTime = Nfs3SetTimeMethod.ClientTime, AccessTime = new Nfs3FileTime(newTime) });
        }

        public override DateTime GetLastWriteTimeUtc(string path)
        {
            Nfs3FileHandle handle = GetFile(path);
            Nfs3FileAttributes attrs = _client.GetAttributes(handle);
            return attrs.ModifyTime.ToDateTime();
        }

        public override void SetLastWriteTimeUtc(string path, DateTime newTime)
        {
            Nfs3FileHandle handle = GetFile(path);
            _client.SetAttributes(handle, new Nfs3SetAttributes() { SetModifyTime = Nfs3SetTimeMethod.ClientTime, ModifyTime = new Nfs3FileTime(newTime) });
        }

        public override long GetFileLength(string path)
        {
            Nfs3FileHandle handle = GetFile(path);
            Nfs3FileAttributes attrs = _client.GetAttributes(handle);
            return attrs.Size;
        }

        private void DoSearch(List<string> results, Nfs3FileHandle dir, string path, Regex regex, bool subFolders, bool dirs, bool files)
        {
            foreach (Nfs3DirectoryEntry de in _client.ReadDirectory(dir, true))
            {
                if (de.Name == "." || de.Name == "..")
                {
                    continue;
                }

                bool isDir = de.FileAttributes.Type == Nfs3FileType.Directory;

                if ((isDir && dirs) || (!isDir && files))
                {
                    string searchName = (de.Name.IndexOf('.') == -1) ? de.Name + "." : de.Name;

                    if (regex.IsMatch(searchName))
                    {
                        results.Add(Path.Combine(path, de.Name));
                    }
                }

                if (subFolders && isDir)
                {
                    DoSearch(results, de.FileHandle, Path.Combine(path, de.Name), regex, subFolders, dirs, files);
                }
            }
        }

        private Nfs3FileHandle GetFile(string path)
        {
            string[] dirs = Utilities.GetDirectoryFromPath(path).Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            string file = Utilities.GetFileFromPath(path);

            Nfs3FileHandle parent = GetDirectory(_client.RootHandle, dirs);

            Nfs3FileHandle handle = _client.Lookup(parent, file);
            if (handle == null)
            {
                throw new FileNotFoundException("No such file or directory", path);
            }
            return handle;
        }

        private Nfs3FileHandle GetDirectory(Nfs3FileHandle parent, string[] dirs)
        {
            if (dirs == null)
            {
                return parent;
            }

            Nfs3FileHandle handle = parent;
            for (int i = 0; i < dirs.Length; ++i)
            {
                handle = _client.Lookup(parent, dirs[i]);

                if (handle == null || _client.GetAttributes(handle).Type != Nfs3FileType.Directory)
                {
                    throw new DirectoryNotFoundException();
                }
            }

            return handle;
        }

        private static string PathToUnix(string path)
        {
            return path.Replace('\\', '/');
        }

        private static string PathFromUnix(string path)
        {
            return path.Replace('/', '\\');
        }
    }
}
