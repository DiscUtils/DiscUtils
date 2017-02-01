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

using System.Collections.Generic;

namespace DiscUtils.Vfs
{
    /// <summary>
    /// Interface implemented by classes representing a directory.
    /// </summary>
    /// <typeparam name="TDirEntry">Concrete type representing directory entries.</typeparam>
    /// <typeparam name="TFile">Concrete type representing files.</typeparam>
    public interface IVfsDirectory<TDirEntry, TFile> : IVfsFile
        where TDirEntry : VfsDirEntry
        where TFile : IVfsFile
    {
        /// <summary>
        /// Gets all of the directory entries.
        /// </summary>
        ICollection<TDirEntry> AllEntries { get; }

        /// <summary>
        /// Gets a self-reference, if available.
        /// </summary>
        TDirEntry Self { get; }

        /// <summary>
        /// Gets a specific directory entry, by name.
        /// </summary>
        /// <param name="name">The name of the directory entry.</param>
        /// <returns>The directory entry, or <c>null</c> if not found.</returns>
        TDirEntry GetEntryByName(string name);

        /// <summary>
        /// Creates a new file.
        /// </summary>
        /// <param name="name">The name of the file (relative to this directory).</param>
        /// <returns>The newly created file.</returns>
        TDirEntry CreateNewFile(string name);
    }
}