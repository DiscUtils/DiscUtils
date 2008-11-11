//
// Copyright (c) 2008, Kenneth Bell
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
using System.Linq;
using System.Text;
using System.IO;

namespace DiscUtils
{
    /// <summary>
    /// Provides information about a directory on a disc.
    /// </summary>
    /// <remarks>
    /// This class allows navigation of the disc directory/file hierarchy.
    /// </remarks>
    public abstract class DiscDirectoryInfo : DiscFileSystemInfo
    {
        /// <summary>
        /// Construction limited to sub classes.
        /// </summary>
        protected DiscDirectoryInfo()
        {
        }

        /// <summary>
        /// Creates a directory.
        /// </summary>
        public abstract void Create();

        /// <summary>
        /// Deletes a directory, even if it's not empty.
        /// </summary>
        /// <param name="recursive">Controls whether to recursively delete contents</param>
        public abstract void Delete(bool recursive);

        /// <summary>
        /// Moves a directory and it's contents to a new path.
        /// </summary>
        /// <param name="destinationDirName">The</param>
        public abstract void MoveTo(string destinationDirName);

        /// <summary>
        /// Gets all child directories.
        /// </summary>
        /// <returns>An array of child directories</returns>
        public abstract DiscDirectoryInfo[] GetDirectories();

        /// <summary>
        /// Gets all child directories matching a search pattern.
        /// </summary>
        /// <param name="pattern">The search pattern</param>
        /// <returns>An array of child directories, or empty if none match</returns>
        /// <remarks>The search pattern can include the wildcards * (matching 0 or more characters)
        /// and ? (matching 1 character).</remarks>
        public abstract DiscDirectoryInfo[] GetDirectories(string pattern);

        /// <summary>
        /// Gets all descendant directories matching a search pattern.
        /// </summary>
        /// <param name="pattern">The search pattern</param>
        /// <param name="searchOption">Whether to search just this directory, or all children</param>
        /// <returns>An array of descendant directories, or empty if none match</returns>
        /// <remarks>The search pattern can include the wildcards * (matching 0 or more characters)
        /// and ? (matching 1 character).  The option parameter determines whether only immediate
        /// children, or all children are returned.</remarks>
        public abstract DiscDirectoryInfo[] GetDirectories(string pattern, SearchOption searchOption);

        /// <summary>
        /// Gets all files.
        /// </summary>
        /// <returns>An array of files.</returns>
        public abstract DiscFileInfo[] GetFiles();

        /// <summary>
        /// Gets all files matching a search pattern.
        /// </summary>
        /// <param name="pattern">The search pattern</param>
        /// <returns>An array of files, or empty if none match</returns>
        /// <remarks>The search pattern can include the wildcards * (matching 0 or more characters)
        /// and ? (matching 1 character).</remarks>
        public abstract DiscFileInfo[] GetFiles(string pattern);

        /// <summary>
        /// Gets all descendant files matching a search pattern.
        /// </summary>
        /// <param name="pattern">The search pattern</param>
        /// <param name="searchOption">Whether to search just this directory, or all children</param>
        /// <returns>An array of descendant files, or empty if none match</returns>
        /// <remarks>The search pattern can include the wildcards * (matching 0 or more characters)
        /// and ? (matching 1 character).  The option parameter determines whether only immediate
        /// children, or all children are returned.</remarks>
        public abstract DiscFileInfo[] GetFiles(string pattern, SearchOption searchOption);

        /// <summary>
        /// Gets all files and directories in this directory.
        /// </summary>
        /// <returns>An array of files and directories.</returns>
        public abstract DiscFileSystemInfo[] GetFileSystemInfos();

        /// <summary>
        /// Gets all files and directories in this directory.
        /// </summary>
        /// <param name="pattern">The search pattern</param>
        /// <returns>An array of files and directories.</returns>
        /// <remarks>The search pattern can include the wildcards * (matching 0 or more characters)
        /// and ? (matching 1 character).</remarks>
        public abstract DiscFileSystemInfo[] GetFileSystemInfos(string pattern);

    }
}
