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

namespace DiscUtils.Vmdk
{
    /// <summary>
    /// Enumeration of known virtual disk controller (aka adapter) types.
    /// </summary>
    public enum DiskAdapterType
    {
        /// <summary>
        /// Unknown or unspecified.
        /// </summary>
        None = 0,

        /// <summary>
        /// IDE controller.
        /// </summary>
        Ide = 1,

        /// <summary>
        /// Bus Logic SCSI controller.
        /// </summary>
        BusLogicScsi = 2,

        /// <summary>
        /// LSI Logic SCSI controller.
        /// </summary>
        LsiLogicScsi = 3,

        /// <summary>
        /// Legacy ESX controller.
        /// </summary>
        LegacyEsx = 4
    }
}