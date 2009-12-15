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
    /// <summary>
    /// A file system backed by an NFS server.
    /// </summary>
    /// <remarks>NFS is a common storage protocol for Virtual Machines.  Currently, only NFS v3 is supported</remarks>
    public class NfsFileSystem : DiscFileSystem
    {
        private Nfs3Client _client;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="address">The address of the NFS server (IP or DNS address)</param>
        /// <param name="credentials">The credentials to use when accessing the NFS server</param>
        /// <param name="mountPoint">The mount point on the server to root the file system</param>
        public NfsFileSystem(string address, RpcCredentials credentials, string mountPoint)
            : base(new NfsFileSystemOptions())
        {
            _client = new Nfs3Client(address, credentials, mountPoint);
        }

        /// <summary>
        /// Disposes of this instance, freeing up any resources used.
        /// </summary>
        /// <param name="disposing"><c>true</c> if called from Dispose, else <c>false</c></param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_client != null)
                {
                    _client.Dispose();
                    _client = null;
                }
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// The options controlling this instance.
        /// </summary>
        public NfsFileSystemOptions NfsOptions
        {
            get { return (NfsFileSystemOptions)Options; }
        }

        /// <summary>
        /// The friendly name for this file system (NFS).
        /// </summary>
        public override string FriendlyName
        {
            get { return "NFS"; }
        }

        /// <summary>
        /// Gets whether this file system supports modification (true for NFS).
        /// </summary>
        public override bool CanWrite
        {
            get { return true; }
        }

        /// <summary>
        /// Copies a file from one location to another.
        /// </summary>
        /// <param name="sourceFile">The source file to copy</param>
        /// <param name="destinationFile">The destination path</param>
        /// <param name="overwrite">Whether to overwrite any existing file (true), or fail if such a file exists</param>
        public override void CopyFile(string sourceFile, string destinationFile, bool overwrite)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a directory at the specified path.
        /// </summary>
        /// <param name="path">The path of the directory to create</param>
        public override void CreateDirectory(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Deletes a directory at the specified path.
        /// </summary>
        /// <param name="path">The directory to delete.</param>
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

        /// <summary>
        /// Deletes a file at the specified path.
        /// </summary>
        /// <param name="path">The path of the file to delete.</param>
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

        /// <summary>
        /// Indicates whether a specified path exists, and refers to a directory.
        /// </summary>
        /// <param name="path">The path to inspect</param>
        /// <returns><c>true</c> if the path is a directory, else <c>false</c>.</returns>
        public override bool DirectoryExists(string path)
        {
            return (GetAttributes(path) & FileAttributes.Directory) != 0;
        }

        /// <summary>
        /// Indicates whether a specified path exists, and refers to a directory.
        /// </summary>
        /// <param name="path">The path to inspect</param>
        /// <returns><c>true</c> if the path is a file, else <c>false</c>.</returns>
        public override bool FileExists(string path)
        {
            return (GetAttributes(path) & FileAttributes.Normal) != 0;
        }

        /// <summary>
        /// Gets the names of subdirectories in a specified directory matching a specified
        /// search pattern, using a value to determine whether to search subdirectories.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <param name="searchPattern">The search string to match against.</param>
        /// <param name="searchOption">Indicates whether to search subdirectories.</param>
        /// <returns>Array of directories matching the search pattern.</returns>
        public override string[] GetDirectories(string path, string searchPattern, SearchOption searchOption)
        {
            Regex re = Utilities.ConvertWildcardsToRegEx(searchPattern);

            List<string> dirs = new List<string>();
            DoSearch(dirs, _client.RootHandle, path, re, searchOption == SearchOption.AllDirectories, true, false);
            return dirs.ToArray();
        }

        /// <summary>
        /// Gets the names of files in a specified directory matching a specified
        /// search pattern, using a value to determine whether to search subdirectories.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <param name="searchPattern">The search string to match against.</param>
        /// <param name="searchOption">Indicates whether to search subdirectories.</param>
        /// <returns>Array of files matching the search pattern.</returns>
        public override string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
        {
            Regex re = Utilities.ConvertWildcardsToRegEx(searchPattern);

            List<string> results = new List<string>();
            DoSearch(results, _client.RootHandle, path, re, searchOption == SearchOption.AllDirectories, false, true);
            return results.ToArray();
        }

        /// <summary>
        /// Gets the names of all files and subdirectories in a specified directory.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <returns>Array of files and subdirectories matching the search pattern.</returns>
        public override string[] GetFileSystemEntries(string path)
        {
            Regex re = Utilities.ConvertWildcardsToRegEx("*.*");

            List<string> results = new List<string>();
            DoSearch(results, _client.RootHandle, path, re, false, true, true);
            return results.ToArray();
        }

        /// <summary>
        /// Gets the names of files and subdirectories in a specified directory matching a specified
        /// search pattern.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <param name="searchPattern">The search string to match against.</param>
        /// <returns>Array of files and subdirectories matching the search pattern.</returns>
        public override string[] GetFileSystemEntries(string path, string searchPattern)
        {
            Regex re = Utilities.ConvertWildcardsToRegEx(searchPattern);

            List<string> results = new List<string>();
            DoSearch(results, _client.RootHandle, path, re, false, true, true);
            return results.ToArray();
        }

        /// <summary>
        /// Moves a directory.
        /// </summary>
        /// <param name="sourceDirectoryName">The directory to move.</param>
        /// <param name="destinationDirectoryName">The target directory name.</param>
        public override void MoveDirectory(string sourceDirectoryName, string destinationDirectoryName)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Moves a file, allowing an existing file to be overwritten.
        /// </summary>
        /// <param name="sourceName">The file to move.</param>
        /// <param name="destinationName">The target file name.</param>
        /// <param name="overwrite">Whether to permit a destination file to be overwritten</param>
        public override void MoveFile(string sourceName, string destinationName, bool overwrite)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Opens the specified file.
        /// </summary>
        /// <param name="path">The full path of the file to open.</param>
        /// <param name="mode">The file mode for the created stream.</param>
        /// <param name="access">The access permissions for the created stream.</param>
        /// <returns>The new stream.</returns>
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
                setAttrs.Size = 0;
                setAttrs.SetSize = true;
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

        /// <summary>
        /// Gets the attributes of a file or directory.
        /// </summary>
        /// <param name="path">The file or directory to inspect</param>
        /// <returns>The attributes of the file or directory</returns>
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

        /// <summary>
        /// Sets the attributes of a file or directory.
        /// </summary>
        /// <param name="path">The file or directory to change</param>
        /// <param name="newValue">The new attributes of the file or directory</param>
        public override void SetAttributes(string path, FileAttributes newValue)
        {
            if (newValue != GetAttributes(path))
            {
                throw new NotSupportedException("Unable to change file attributes over NFS");
            }
        }

        /// <summary>
        /// Gets the creation time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <returns>The creation time.</returns>
        public override DateTime GetCreationTimeUtc(string path)
        {
            // Note creation time is not available, so simulating from last modification time
            Nfs3FileHandle handle = GetFile(path);
            Nfs3FileAttributes attrs = _client.GetAttributes(handle);
            return attrs.ModifyTime.ToDateTime();
        }

        /// <summary>
        /// Sets the creation time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <param name="newTime">The new time to set.</param>
        public override void SetCreationTimeUtc(string path, DateTime newTime)
        {
            // No action - creation time is not accessible over NFS
        }

        /// <summary>
        /// Gets the last access time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory</param>
        /// <returns>The last access time</returns>
        public override DateTime GetLastAccessTimeUtc(string path)
        {
            Nfs3FileHandle handle = GetFile(path);
            Nfs3FileAttributes attrs = _client.GetAttributes(handle);
            return attrs.AccessTime.ToDateTime();
        }

        /// <summary>
        /// Sets the last access time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <param name="newTime">The new time to set.</param>
        public override void SetLastAccessTimeUtc(string path, DateTime newTime)
        {
            Nfs3FileHandle handle = GetFile(path);
            _client.SetAttributes(handle, new Nfs3SetAttributes() { SetAccessTime = Nfs3SetTimeMethod.ClientTime, AccessTime = new Nfs3FileTime(newTime) });
        }

        /// <summary>
        /// Gets the last modification time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory</param>
        /// <returns>The last write time</returns>
        public override DateTime GetLastWriteTimeUtc(string path)
        {
            Nfs3FileHandle handle = GetFile(path);
            Nfs3FileAttributes attrs = _client.GetAttributes(handle);
            return attrs.ModifyTime.ToDateTime();
        }

        /// <summary>
        /// Sets the last modification time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <param name="newTime">The new time to set.</param>
        public override void SetLastWriteTimeUtc(string path, DateTime newTime)
        {
            Nfs3FileHandle handle = GetFile(path);
            _client.SetAttributes(handle, new Nfs3SetAttributes() { SetModifyTime = Nfs3SetTimeMethod.ClientTime, ModifyTime = new Nfs3FileTime(newTime) });
        }

        /// <summary>
        /// Gets the length of a file.
        /// </summary>
        /// <param name="path">The path to the file</param>
        /// <returns>The length in bytes</returns>
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
    }
}
