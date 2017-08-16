//
// DiscUtils Copyright (c) 2008-2011, Kenneth Bell
//
// Original NativeFileSystem contributed by bsobel:
//    http://discutils.codeplex.com/workitem/5190
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
using DiscUtils.Internal;
using DiscUtils.Streams;

namespace DiscUtils
{
    /// <summary>
    /// Provides an implementation for OS-mounted file systems.
    /// </summary>
    public class NativeFileSystem : DiscFileSystem
    {
        private readonly bool _readOnly;

        /// <summary>
        /// Initializes a new instance of the NativeFileSystem class.
        /// </summary>
        /// <param name="basePath">The 'root' directory of the new instance.</param>
        /// <param name="readOnly">Only permit 'read' activities.</param>
        public NativeFileSystem(string basePath, bool readOnly)
        {
            BasePath = basePath;
            if (!BasePath.EndsWith(@"\", StringComparison.OrdinalIgnoreCase))
            {
                BasePath += @"\";
            }

            _readOnly = readOnly;
        }

        /// <summary>
        /// Gets the base path used to create the file system.
        /// </summary>
        public string BasePath { get; }

        /// <summary>
        /// Indicates whether the file system is read-only or read-write.
        /// </summary>
        /// <returns>true if the file system is read-write.</returns>
        public override bool CanWrite
        {
            get { return !_readOnly; }
        }

        /// <summary>
        /// Provides a friendly description of the file system type.
        /// </summary>
        public override string FriendlyName
        {
            get { return "Native"; }
        }

        /// <summary>
        /// Gets a value indicating whether the file system is thread-safe.
        /// </summary>
        /// <remarks>The Native File System is thread safe.</remarks>
        public override bool IsThreadSafe
        {
            get { return true; }
        }

        /// <summary>
        /// Gets the root directory of the file system.
        /// </summary>
        public override DiscDirectoryInfo Root
        {
            get { return new DiscDirectoryInfo(this, string.Empty); }
        }

        /// <summary>
        /// Gets the volume label.
        /// </summary>
        public override string VolumeLabel
        {
            get { return string.Empty; }
        }

        /// <summary>
        /// Copies an existing file to a new file.
        /// </summary>
        /// <param name="sourceFile">The source file.</param>
        /// <param name="destinationFile">The destination file.</param>
        public override void CopyFile(string sourceFile, string destinationFile)
        {
            CopyFile(sourceFile, destinationFile, true);
        }

        /// <summary>
        /// Copies an existing file to a new file, allowing overwriting of an existing file.
        /// </summary>
        /// <param name="sourceFile">The source file.</param>
        /// <param name="destinationFile">The destination file.</param>
        /// <param name="overwrite">Whether to permit over-writing of an existing file.</param>
        public override void CopyFile(string sourceFile, string destinationFile, bool overwrite)
        {
            if (_readOnly)
            {
                throw new UnauthorizedAccessException();
            }

            if (sourceFile.StartsWith(@"\", StringComparison.OrdinalIgnoreCase))
            {
                sourceFile = sourceFile.Substring(1);
            }

            if (destinationFile.StartsWith(@"\", StringComparison.OrdinalIgnoreCase))
            {
                destinationFile = destinationFile.Substring(1);
            }

            File.Copy(Path.Combine(BasePath, sourceFile), Path.Combine(BasePath, destinationFile), true);
        }

        /// <summary>
        /// Creates a directory.
        /// </summary>
        /// <param name="path">The path of the new directory.</param>
        public override void CreateDirectory(string path)
        {
            if (_readOnly)
            {
                throw new UnauthorizedAccessException();
            }

            if (path.StartsWith(@"\", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(1);
            }

            Directory.CreateDirectory(Path.Combine(BasePath, path));
        }

        /// <summary>
        /// Deletes a directory.
        /// </summary>
        /// <param name="path">The path of the directory to delete.</param>
        public override void DeleteDirectory(string path)
        {
            if (_readOnly)
            {
                throw new UnauthorizedAccessException();
            }

            if (path.StartsWith(@"\", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(1);
            }

            Directory.Delete(Path.Combine(BasePath, path));
        }

        /// <summary>
        /// Deletes a directory, optionally with all descendants.
        /// </summary>
        /// <param name="path">The path of the directory to delete.</param>
        /// <param name="recursive">Determines if the all descendants should be deleted.</param>
        public override void DeleteDirectory(string path, bool recursive)
        {
            if (recursive)
            {
                foreach (string dir in GetDirectories(path))
                {
                    DeleteDirectory(dir, true);
                }

                foreach (string file in GetFiles(path))
                {
                    DeleteFile(file);
                }
            }

            DeleteDirectory(path);
        }

        /// <summary>
        /// Deletes a file.
        /// </summary>
        /// <param name="path">The path of the file to delete.</param>
        public override void DeleteFile(string path)
        {
            if (_readOnly)
            {
                throw new UnauthorizedAccessException();
            }

            if (path.StartsWith(@"\", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(1);
            }

            File.Delete(Path.Combine(BasePath, path));
        }

        /// <summary>
        /// Indicates if a directory exists.
        /// </summary>
        /// <param name="path">The path to test.</param>
        /// <returns>true if the directory exists.</returns>
        public override bool DirectoryExists(string path)
        {
            if (path.StartsWith(@"\", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(1);
            }

            return Directory.Exists(Path.Combine(BasePath, path));
        }

        /// <summary>
        /// Indicates if a file exists.
        /// </summary>
        /// <param name="path">The path to test.</param>
        /// <returns>true if the file exists.</returns>
        public override bool FileExists(string path)
        {
            if (path.StartsWith(@"\", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(1);
            }

            return File.Exists(Path.Combine(BasePath, path));
        }

        /// <summary>
        /// Indicates if a file or directory exists.
        /// </summary>
        /// <param name="path">The path to test.</param>
        /// <returns>true if the file or directory exists.</returns>
        public override bool Exists(string path)
        {
            return FileExists(path) || DirectoryExists(path);
        }

        /// <summary>
        /// Gets the names of subdirectories in a specified directory.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <returns>Array of directories.</returns>
        public override string[] GetDirectories(string path)
        {
            return GetDirectories(path, "*.*", SearchOption.TopDirectoryOnly);
        }

        /// <summary>
        /// Gets the names of subdirectories in a specified directory matching a specified
        /// search pattern.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <param name="searchPattern">The search string to match against.</param>
        /// <returns>Array of directories matching the search pattern.</returns>
        public override string[] GetDirectories(string path, string searchPattern)
        {
            return GetDirectories(path, searchPattern, SearchOption.TopDirectoryOnly);
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
            if (path.StartsWith(@"\", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(1);
            }

            try
            {
                return CleanItems(Directory.GetDirectories(Path.Combine(BasePath, path), searchPattern, searchOption));
            }
            catch (IOException)
            {
                return new string[0];
            }
            catch (UnauthorizedAccessException)
            {
                return new string[0];
            }
        }

        /// <summary>
        /// Gets the names of files in a specified directory.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <returns>Array of files.</returns>
        public override string[] GetFiles(string path)
        {
            return GetFiles(path, "*.*", SearchOption.TopDirectoryOnly);
        }

        /// <summary>
        /// Gets the names of files in a specified directory.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <param name="searchPattern">The search string to match against.</param>
        /// <returns>Array of files matching the search pattern.</returns>
        public override string[] GetFiles(string path, string searchPattern)
        {
            return GetFiles(path, searchPattern, SearchOption.TopDirectoryOnly);
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
            if (path.StartsWith(@"\", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(1);
            }

            try
            {
                return CleanItems(Directory.GetFiles(Path.Combine(BasePath, path), searchPattern, searchOption));
            }
            catch (IOException)
            {
                return new string[0];
            }
            catch (UnauthorizedAccessException)
            {
                return new string[0];
            }
        }

        /// <summary>
        /// Gets the names of all files and subdirectories in a specified directory.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <returns>Array of files and subdirectories matching the search pattern.</returns>
        public override string[] GetFileSystemEntries(string path)
        {
            return GetFileSystemEntries(path, "*.*");
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
            if (path.StartsWith(@"\", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(1);
            }

            try
            {
                return CleanItems(Directory.GetFileSystemEntries(Path.Combine(BasePath, path), searchPattern));
            }
            catch (IOException)
            {
                return new string[0];
            }
            catch (UnauthorizedAccessException)
            {
                return new string[0];
            }
        }

        /// <summary>
        /// Moves a directory.
        /// </summary>
        /// <param name="sourceDirectoryName">The directory to move.</param>
        /// <param name="destinationDirectoryName">The target directory name.</param>
        public override void MoveDirectory(string sourceDirectoryName, string destinationDirectoryName)
        {
            if (_readOnly)
            {
                throw new UnauthorizedAccessException();
            }

            if (sourceDirectoryName.StartsWith(@"\", StringComparison.OrdinalIgnoreCase))
            {
                sourceDirectoryName = sourceDirectoryName.Substring(1);
            }

            if (destinationDirectoryName.StartsWith(@"\", StringComparison.OrdinalIgnoreCase))
            {
                destinationDirectoryName = destinationDirectoryName.Substring(1);
            }

            Directory.Move(Path.Combine(BasePath, sourceDirectoryName),
                Path.Combine(BasePath, destinationDirectoryName));
        }

        /// <summary>
        /// Moves a file.
        /// </summary>
        /// <param name="sourceName">The file to move.</param>
        /// <param name="destinationName">The target file name.</param>
        public override void MoveFile(string sourceName, string destinationName)
        {
            MoveFile(sourceName, destinationName, false);
        }

        /// <summary>
        /// Moves a file, allowing an existing file to be overwritten.
        /// </summary>
        /// <param name="sourceName">The file to move.</param>
        /// <param name="destinationName">The target file name.</param>
        /// <param name="overwrite">Whether to permit a destination file to be overwritten.</param>
        public override void MoveFile(string sourceName, string destinationName, bool overwrite)
        {
            if (_readOnly)
            {
                throw new UnauthorizedAccessException();
            }

            if (destinationName.StartsWith(@"\", StringComparison.OrdinalIgnoreCase))
            {
                destinationName = destinationName.Substring(1);
            }

            if (FileExists(Path.Combine(BasePath, destinationName)))
            {
                if (overwrite)
                {
                    DeleteFile(Path.Combine(BasePath, destinationName));
                }
                else
                {
                    throw new IOException("File already exists");
                }
            }

            if (sourceName.StartsWith(@"\", StringComparison.OrdinalIgnoreCase))
            {
                sourceName = sourceName.Substring(1);
            }

            File.Move(Path.Combine(BasePath, sourceName), Path.Combine(BasePath, destinationName));
        }

        /// <summary>
        /// Opens the specified file.
        /// </summary>
        /// <param name="path">The full path of the file to open.</param>
        /// <param name="mode">The file mode for the created stream.</param>
        /// <returns>The new stream.</returns>
        public override SparseStream OpenFile(string path, FileMode mode)
        {
            return OpenFile(path, mode, FileAccess.ReadWrite);
        }

        /// <summary>
        /// Opens the specified file.
        /// </summary>
        /// <param name="path">The full path of the file to open.</param>
        /// <param name="mode">The file mode for the created stream.</param>
        /// <param name="access">The access permissions for the created stream.</param>
        /// <returns>The new stream.</returns>
        public override SparseStream OpenFile(string path, FileMode mode, FileAccess access)
        {
            if (_readOnly && access != FileAccess.Read)
            {
                throw new UnauthorizedAccessException();
            }

            if (path.StartsWith(@"\", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(1);
            }

            FileShare fileShare = FileShare.None;
            if (access == FileAccess.Read)
            {
                fileShare = FileShare.Read;
            }

            var locator = new LocalFileLocator(BasePath);
            return SparseStream.FromStream(locator.Open(path, mode, access, fileShare),
                Ownership.Dispose);
        }

        /// <summary>
        /// Gets the attributes of a file or directory.
        /// </summary>
        /// <param name="path">The file or directory to inspect.</param>
        /// <returns>The attributes of the file or directory.</returns>
        public override FileAttributes GetAttributes(string path)
        {
            if (path.StartsWith(@"\", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(1);
            }

            return File.GetAttributes(Path.Combine(BasePath, path));
        }

        /// <summary>
        /// Sets the attributes of a file or directory.
        /// </summary>
        /// <param name="path">The file or directory to change.</param>
        /// <param name="newValue">The new attributes of the file or directory.</param>
        public override void SetAttributes(string path, FileAttributes newValue)
        {
            if (_readOnly)
            {
                throw new UnauthorizedAccessException();
            }

            if (path.StartsWith(@"\", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(1);
            }

            File.SetAttributes(Path.Combine(BasePath, path), newValue);
        }

        /// <summary>
        /// Gets the creation time (in local time) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <returns>The creation time.</returns>
        public override DateTime GetCreationTime(string path)
        {
            return GetCreationTimeUtc(path).ToLocalTime();
        }

        /// <summary>
        /// Sets the creation time (in local time) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <param name="newTime">The new time to set.</param>
        public override void SetCreationTime(string path, DateTime newTime)
        {
            SetCreationTimeUtc(path, newTime.ToUniversalTime());
        }

        /// <summary>
        /// Gets the creation time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <returns>The creation time.</returns>
        public override DateTime GetCreationTimeUtc(string path)
        {
            if (path.StartsWith(@"\", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(1);
            }

            return File.GetCreationTimeUtc(Path.Combine(BasePath, path));
        }

        /// <summary>
        /// Sets the creation time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <param name="newTime">The new time to set.</param>
        public override void SetCreationTimeUtc(string path, DateTime newTime)
        {
            if (_readOnly)
            {
                throw new UnauthorizedAccessException();
            }

            if (path.StartsWith(@"\", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(1);
            }

            File.SetCreationTimeUtc(Path.Combine(BasePath, path), newTime);
        }

        /// <summary>
        /// Gets the last access time (in local time) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <returns>The last access time.</returns>
        public override DateTime GetLastAccessTime(string path)
        {
            return GetLastAccessTimeUtc(path).ToLocalTime();
        }

        /// <summary>
        /// Sets the last access time (in local time) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <param name="newTime">The new time to set.</param>
        public override void SetLastAccessTime(string path, DateTime newTime)
        {
            SetLastAccessTimeUtc(path, newTime.ToUniversalTime());
        }

        /// <summary>
        /// Gets the last access time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <returns>The last access time.</returns>
        public override DateTime GetLastAccessTimeUtc(string path)
        {
            if (path.StartsWith(@"\", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(1);
            }

            return File.GetLastAccessTimeUtc(Path.Combine(BasePath, path));
        }

        /// <summary>
        /// Sets the last access time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <param name="newTime">The new time to set.</param>
        public override void SetLastAccessTimeUtc(string path, DateTime newTime)
        {
            if (_readOnly)
            {
                throw new UnauthorizedAccessException();
            }

            if (path.StartsWith(@"\", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(1);
            }

            File.SetLastAccessTimeUtc(Path.Combine(BasePath, path), newTime);
        }

        /// <summary>
        /// Gets the last modification time (in local time) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <returns>The last write time.</returns>
        public override DateTime GetLastWriteTime(string path)
        {
            return GetLastWriteTimeUtc(path).ToLocalTime();
        }

        /// <summary>
        /// Sets the last modification time (in local time) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <param name="newTime">The new time to set.</param>
        public override void SetLastWriteTime(string path, DateTime newTime)
        {
            SetLastWriteTimeUtc(path, newTime.ToUniversalTime());
        }

        /// <summary>
        /// Gets the last modification time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <returns>The last write time.</returns>
        public override DateTime GetLastWriteTimeUtc(string path)
        {
            if (path.StartsWith(@"\", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(1);
            }

            return File.GetLastWriteTimeUtc(Path.Combine(BasePath, path));
        }

        /// <summary>
        /// Sets the last modification time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <param name="newTime">The new time to set.</param>
        public override void SetLastWriteTimeUtc(string path, DateTime newTime)
        {
            if (_readOnly)
            {
                throw new UnauthorizedAccessException();
            }

            if (path.StartsWith(@"\", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(1);
            }

            File.SetLastWriteTimeUtc(Path.Combine(BasePath, path), newTime);
        }

        /// <summary>
        /// Gets the length of a file.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <returns>The length in bytes.</returns>
        public override long GetFileLength(string path)
        {
            if (path.StartsWith(@"\", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(1);
            }

            return new FileInfo(Path.Combine(BasePath, path)).Length;
        }

        /// <summary>
        /// Gets an object representing a possible file.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns>The representing object.</returns>
        /// <remarks>The file does not need to exist.</remarks>
        public override DiscFileInfo GetFileInfo(string path)
        {
            return new DiscFileInfo(this, path);
        }

        /// <summary>
        /// Gets an object representing a possible directory.
        /// </summary>
        /// <param name="path">The directory path.</param>
        /// <returns>The representing object.</returns>
        /// <remarks>The directory does not need to exist.</remarks>
        public override DiscDirectoryInfo GetDirectoryInfo(string path)
        {
            return new DiscDirectoryInfo(this, path);
        }

        /// <summary>
        /// Gets an object representing a possible file system object (file or directory).
        /// </summary>
        /// <param name="path">The file system path.</param>
        /// <returns>The representing object.</returns>
        /// <remarks>The file system object does not need to exist.</remarks>
        public override DiscFileSystemInfo GetFileSystemInfo(string path)
        {
            return new DiscFileSystemInfo(this, path);
        }
         
        /// <summary>
        /// Size of the Filesystem in bytes
        /// </summary>
        public override long Size
        {
            get
            {
                DriveInfo info = new DriveInfo(BasePath);
                return info.TotalSize;
            }
        }
 
        /// <summary>
        /// Used space of the Filesystem in bytes
        /// </summary>
        public override long UsedSpace
        {
            get { return Size - AvailableSpace; }
        }
 
        /// <summary>
        /// Available space of the Filesystem in bytes
        /// </summary>
        public override long AvailableSpace
        {
            get
            {
                DriveInfo info = new DriveInfo(BasePath);
                return info.AvailableFreeSpace;
            }
        }

        private string[] CleanItems(string[] dirtyItems)
        {
            string[] cleanList = new string[dirtyItems.Length];
            for (int x = 0; x < dirtyItems.Length; x++)
            {
                cleanList[x] = dirtyItems[x].Substring(BasePath.Length - 1);
            }

            return cleanList;
        }
    }
}
