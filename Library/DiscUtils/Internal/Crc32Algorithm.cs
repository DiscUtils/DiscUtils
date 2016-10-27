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

namespace DiscUtils.Internal
{
    internal enum Crc32Algorithm
    {
        /// <summary>
        /// Used in Ethernet, PKZIP, BZIP2, Gzip, PNG, etc. (aka CRC32).
        /// </summary>
        Common = 0,

        /// <summary>
        /// Used in iSCSI, SCTP, Btrfs, Vhdx. (aka CRC32C).
        /// </summary>
        Castagnoli = 1,

        /// <summary>
        /// Unknown usage. (aka CRC32K).
        /// </summary>
        Koopman = 2,

        /// <summary>
        /// Used in AIXM.  (aka CRC32Q).
        /// </summary>
        Aeronautical = 3
    }
}