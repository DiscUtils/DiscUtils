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

namespace DiscUtils.Registry
{
    /// <summary>
    /// The types of registry values.
    /// </summary>
    public enum RegistryValueType
    {
        /// <summary>
        /// Unknown type.
        /// </summary>
        None = 0x00,

        /// <summary>
        /// A unicode string.
        /// </summary>
        String = 0x01,

        /// <summary>
        /// A string containing environment variables.
        /// </summary>
        ExpandString = 0x02,

        /// <summary>
        /// Binary data.
        /// </summary>
        Binary = 0x03,

        /// <summary>
        /// A 32-bit integer.
        /// </summary>
        Dword = 0x04,

        /// <summary>
        /// A 32-bit integer.
        /// </summary>
        DwordBigEndian = 0x05,

        /// <summary>
        /// A registry link.
        /// </summary>
        Link = 0x06,

        /// <summary>
        /// A multistring.
        /// </summary>
        MultiString = 0x07,

        /// <summary>
        /// An unknown binary format.
        /// </summary>
        ResourceList = 0x08,

        /// <summary>
        /// An unknown binary format.
        /// </summary>
        FullResourceDescriptor = 0x09,

        /// <summary>
        /// An unknown binary format.
        /// </summary>
        ResourceRequirementsList = 0x0A,

        /// <summary>
        /// A 64-bit integer.
        /// </summary>
        QWord = 0x0B
    }
}