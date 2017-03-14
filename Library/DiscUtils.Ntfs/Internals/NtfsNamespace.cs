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

namespace DiscUtils.Ntfs.Internals
{
    /// <summary>
    /// The known NTFS namespaces.
    /// </summary>
    /// <remarks>
    /// NTFS has multiple namespaces, indicating whether a name is the
    /// long name for a file, the short name for a file, both, or none.
    /// </remarks>
    public enum NtfsNamespace
    {
        /// <summary>
        /// Posix namespace (i.e. long name).
        /// </summary>
        Posix = 0,

        /// <summary>
        /// Windows long file name.
        /// </summary>
        Win32 = 1,

        /// <summary>
        /// DOS (8.3) file name.
        /// </summary>
        Dos = 2,

        /// <summary>
        /// File name that is both the long name and the DOS (8.3) name.
        /// </summary>
        Win32AndDos = 3
    }
}