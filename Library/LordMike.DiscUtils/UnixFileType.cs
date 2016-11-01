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

namespace DiscUtils
{
    /// <summary>
    /// Standard Unix-style file type.
    /// </summary>
    public enum UnixFileType
    {
        /// <summary>
        /// No type specified.
        /// </summary>
        None = 0,

        /// <summary>
        /// A FIFO / Named Pipe.
        /// </summary>
        Fifo = 0x1,

        /// <summary>
        /// A character device.
        /// </summary>
        Character = 0x2,

        /// <summary>
        /// A normal directory.
        /// </summary>
        Directory = 0x4,

        /// <summary>
        /// A block device.
        /// </summary>
        Block = 0x6,

        /// <summary>
        /// A regular file.
        /// </summary>
        Regular = 0x8,

        /// <summary>
        /// A soft link.
        /// </summary>
        Link = 0xA,

        /// <summary>
        /// A unix socket.
        /// </summary>
        Socket = 0xC
    }
}