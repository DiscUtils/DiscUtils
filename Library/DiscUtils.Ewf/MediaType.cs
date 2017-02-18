//
// Copyright (c) 2013, Adam Bridge
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

namespace DiscUtils.Ewf
{
    /// <summary>
    /// Specifies the source media of the acquisition.
    /// </summary>
    public enum MediaType
    {
        /// <summary>
        /// Is removable media.
        /// </summary>
        Removable = 0x00,

        /// <summary>
        /// Is fixed media.
        /// </summary>
        Fixed = 0x01,

        /// <summary>
        /// Is an optical disc.
        /// </summary>
        Disc = 0x03,

        /// <summary>
        /// Is a Logical Evidence File.
        /// </summary>
        LEF = 0x0E,

        /// <summary>
        /// Is a RAM acquisition.
        /// </summary>
        RAM = 0x10
    }
}
