//
// Copyright (c) 2017, Bianco Veigel
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

namespace DiscUtils.Setup
{
    internal delegate Stream FileOpenDelegate(string fileName, FileMode mode, FileAccess access, FileShare share);

    /// <summary>
    /// Event arguments for opening a file
    /// </summary>
    public class FileOpenEventArgs:EventArgs
    {
        private FileOpenDelegate _opener;

        internal FileOpenEventArgs(string fileName, FileMode mode, FileAccess access, FileShare share, FileOpenDelegate opener)
        {
            FileName = fileName;
            FileMode = mode;
            FileAccess = access;
            FileShare = share;
            _opener = opener;
        }

        /// <summary>
        /// Gets or sets the filename to open
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="FileMode"/>
        /// </summary>
        public FileMode FileMode { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="FileAccess"/>
        /// </summary>
        public FileAccess FileAccess { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="FileShare"/>
        /// </summary>
        public FileShare FileShare { get; set; }

        /// <summary>
        /// The resulting stream.
        /// </summary>
        /// <remarks>
        /// If this is set to a non null value, this stream is used instead of opening the supplied <see cref="FileName"/>
        /// </remarks>
        public Stream Result { get; set; }

        /// <summary>
        /// returns the result from the builtin FileLocator
        /// </summary>
        /// <returns></returns>
        public Stream GetFileStream()
        {
            return _opener(FileName, FileMode, FileAccess, FileShare);
        }
    }
}