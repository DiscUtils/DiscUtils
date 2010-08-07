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


namespace DiscUtils.Ntfs
{
    /// <summary>
    /// Class whose instances hold options controlling how <see cref="NtfsFileSystem"/> works.
    /// </summary>
    public sealed class NtfsOptions : DiscFileSystemOptions
    {
        private bool _hideMetaFiles;
        private bool _hideHiddenFiles;
        private bool _hideSystemFiles;
        private bool _hideDosFileNames;
        private ShortFileNameOption _shortNameCreation;

        internal NtfsOptions()
        {
            _hideMetaFiles = true;
            _hideHiddenFiles = true;
            _hideSystemFiles = true;
            _hideDosFileNames = true;
        }

        /// <summary>
        /// Gets and sets whether to include file system meta-files when enumerating directories.
        /// </summary>
        /// <remarks>Meta-files are those with an MFT (Master File Table) index less than 24.</remarks>
        public bool HideMetafiles
        {
            get { return _hideMetaFiles; }
            set { _hideMetaFiles = value; }
        }

        /// <summary>
        /// Get and sets whether to include hidden files when enumerating directories.
        /// </summary>
        public bool HideHiddenFiles
        {
            get { return _hideHiddenFiles; }
            set { _hideHiddenFiles = value; }
        }

        /// <summary>
        /// Gets and sets whether to include system files when enumerating directories.
        /// </summary>
        public bool HideSystemFiles
        {
            get { return _hideSystemFiles; }
            set { _hideSystemFiles = value; }
        }

        /// <summary>
        /// Gets and sets whether to hide DOS (8.3-style) file names when enumerating directories.
        /// </summary>
        public bool HideDosFileNames
        {
            get { return _hideDosFileNames; }
            set { _hideDosFileNames = value; }
        }

        /// <summary>
        /// Gets and sets whether short (8.3) file names are created automatically.
        /// </summary>
        public ShortFileNameOption ShortNameCreation
        {
            get { return _shortNameCreation; }
            set { _shortNameCreation = value; }
        }

        /// <summary>
        /// Returns a string representation of the file system options.
        /// </summary>
        /// <returns>A string</returns>
        public override string ToString()
        {
            return "Show: Normal " + (HideMetafiles ? "" : "Meta ") + (HideHiddenFiles ? "" : "Hidden ") + (HideSystemFiles ? "" : "System ") + (HideDosFileNames ? "" : "ShortNames ");
        }
    }
}
