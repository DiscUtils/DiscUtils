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

namespace DiscUtils.Iso9660
{
    /// <summary>
    /// Enumeration of boot device emulation modes.
    /// </summary>
    public enum BootDeviceEmulation : byte
    {
        /// <summary>
        /// No emulation, the boot image is just loaded and executed.
        /// </summary>
        NoEmulation = 0x0,

        /// <summary>
        /// Emulates 1.2MB diskette image as drive A.
        /// </summary>
        Diskette1200KiB = 0x1,

        /// <summary>
        /// Emulates 1.44MB diskette image as drive A.
        /// </summary>
        Diskette1440KiB = 0x2,

        /// <summary>
        /// Emulates 2.88MB diskette image as drive A.
        /// </summary>
        Diskette2880KiB = 0x3,

        /// <summary>
        /// Emulates hard disk image as drive C.
        /// </summary>
        HardDisk = 0x4
    }
}