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

namespace DiscUtils.Registry
{
    /// <summary>
    /// The per-key flags present on registry keys.
    /// </summary>
    [Flags]
    public enum RegistryKeyFlags
    {
        /// <summary>
        /// Unknown purpose.
        /// </summary>
        Unknown0001 = 0x0001,

        /// <summary>
        /// Unknown purpose.
        /// </summary>
        Unknown0002 = 0x0002,

        /// <summary>
        /// The key is the root key in the registry hive.
        /// </summary>
        Root = 0x0004,

        /// <summary>
        /// Unknown purpose.
        /// </summary>
        Unknown0008 = 0x0008,

        /// <summary>
        /// The key is a link to another key.
        /// </summary>
        Link = 0x0010,

        /// <summary>
        /// This is a normal key.
        /// </summary>
        Normal = 0x0020,

        /// <summary>
        /// Unknown purpose.
        /// </summary>
        Unknown0040 = 0x0040,

        /// <summary>
        /// Unknown purpose.
        /// </summary>
        Unknown0080 = 0x0080,

        /// <summary>
        /// Unknown purpose.
        /// </summary>
        Unknown0100 = 0x0100,

        /// <summary>
        /// Unknown purpose.
        /// </summary>
        Unknown0200 = 0x0200,

        /// <summary>
        /// Unknown purpose.
        /// </summary>
        Unknown0400 = 0x0400,

        /// <summary>
        /// Unknown purpose.
        /// </summary>
        Unknown0800 = 0x0800,

        /// <summary>
        /// Unknown purpose.
        /// </summary>
        Unknown1000 = 0x1000,

        /// <summary>
        /// Unknown purpose.
        /// </summary>
        Unknown2000 = 0x2000,

        /// <summary>
        /// Unknown purpose.
        /// </summary>
        Unknown4000 = 0x4000,

        /// <summary>
        /// Unknown purpose.
        /// </summary>
        Unknown8000 = 0x8000
    }
}