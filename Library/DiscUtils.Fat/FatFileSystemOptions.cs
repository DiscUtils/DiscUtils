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

using System;
using System.Text;
using DiscUtils.CoreCompat;

namespace DiscUtils.Fat
{
    /// <summary>
    /// FAT file system options.
    /// </summary>
    public sealed class FatFileSystemOptions : DiscFileSystemOptions
    {
        private Encoding _encoding;

        static FatFileSystemOptions()
        {
            EncodingHelper.RegisterEncodings();
        }

        internal FatFileSystemOptions()
        {
            FileNameEncoding = Encoding.GetEncoding(437);
        }

        internal FatFileSystemOptions(FileSystemParameters parameters)
        {
            if (parameters != null && parameters.FileNameEncoding != null)
            {
                FileNameEncoding = parameters.FileNameEncoding;
            }
            else
            {
                FileNameEncoding = Encoding.GetEncoding(437);
            }
        }

        /// <summary>
        /// Gets or sets the character encoding used for file names.
        /// </summary>
        public Encoding FileNameEncoding
        {
            get { return _encoding; }

            set
            {
                if (!value.IsSingleByte)
                {
                    throw new ArgumentException(value.EncodingName + " is not a single byte encoding");
                }

                _encoding = value;
            }
        }
    }
}