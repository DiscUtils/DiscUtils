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
    /// Enumeration of known application types.
    /// </summary>
    public enum ApplicationType
    {
        /// <summary>
        /// Unknown type.
        /// </summary>
        None = 0,

        /// <summary>
        /// Firmware boot manager (e.g. UEFI).
        /// </summary>
        FirmwareBootManager = 1,

        /// <summary>
        /// Windows boot manager.
        /// </summary>
        BootManager = 2,

        /// <summary>
        /// Operating System Loader.
        /// </summary>
        OsLoader = 3,

        /// <summary>
        /// Resume loader.
        /// </summary>
        Resume = 4,

        /// <summary>
        /// Memory diagnostic application.
        /// </summary>
        MemoryDiagnostics = 5,

        /// <summary>
        /// Legacy NT loader application.
        /// </summary>
        NtLoader = 6,

        /// <summary>
        /// Windows setup application.
        /// </summary>
        SetupLoader = 7,

        /// <summary>
        /// Boot sector application.
        /// </summary>
        BootSector = 8,

        /// <summary>
        /// Startup application.
        /// </summary>
        Startup = 9
    }
}