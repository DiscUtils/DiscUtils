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

namespace DiscUtils.BootConfig
{
    /// <summary>
    /// The known formats used to store BCD values.
    /// </summary>
    public enum ElementFormat
    {
        /// <summary>
        /// Unknown format.
        /// </summary>
        None = 0,

        /// <summary>
        /// A block device, or partition.
        /// </summary>
        Device = 1,

        /// <summary>
        /// A unicode string.
        /// </summary>
        String = 2,

        /// <summary>
        /// A Globally Unique Identifier (GUID).
        /// </summary>
        Guid = 3,

        /// <summary>
        /// A GUID list.
        /// </summary>
        GuidList = 4,

        /// <summary>
        /// An integer.
        /// </summary>
        Integer = 5,

        /// <summary>
        /// A boolean.
        /// </summary>
        Boolean = 6,

        /// <summary>
        /// An integer list.
        /// </summary>
        IntegerList = 7
    }
}