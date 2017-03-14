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

using System.Text;

namespace DiscUtils
{
    /// <summary>
    /// Class with generic file system parameters.
    /// </summary>
    /// <remarks>Note - not all parameters apply to all types of file system.</remarks>
    public sealed class FileSystemParameters
    {
        /// <summary>
        /// Gets or sets the character encoding for file names, or <c>null</c> for default.
        /// </summary>
        /// <remarks>Some file systems, such as FAT, don't specify a particular character set for
        /// file names.  This parameter determines the character set that will be used for such
        /// file systems.</remarks>
        public Encoding FileNameEncoding { get; set; }

        /// <summary>
        /// Gets or sets the algorithm to convert file system time to UTC.
        /// </summary>
        /// <remarks>Some file system, such as FAT, don't have a defined way to convert from file system
        /// time (local time where the file system is authored) to UTC time.  This parameter determines
        /// the algorithm to use.</remarks>
        public TimeConverter TimeConverter { get; set; }
    }
}