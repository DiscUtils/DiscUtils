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

using System.Security.Principal;

namespace DiscUtils.Ntfs
{
    /// <summary>
    /// Class representing NTFS formatting options.
    /// </summary>
    public sealed class NtfsFormatOptions
    {
        /// <summary>
        /// Gets or sets the NTFS bootloader code to put in the formatted file system.
        /// </summary>
        public byte[] BootCode { get; set; }

        /// <summary>
        /// Gets or sets the SID of the computer account that notionally formatted the file system.
        /// </summary>
        /// <remarks>
        /// Certain ACLs in the file system will refer to the 'local' administrator of the indicated
        /// computer account.
        /// </remarks>
        public SecurityIdentifier ComputerAccount { get; set; }
    }
}