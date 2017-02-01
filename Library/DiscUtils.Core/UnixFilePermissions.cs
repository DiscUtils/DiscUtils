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

namespace DiscUtils
{
    /// <summary>
    /// Standard Unix-style file system permissions.
    /// </summary>
    [Flags]
    public enum UnixFilePermissions
    {
        /// <summary>
        /// No permissions.
        /// </summary>
        None = 0,

        /// <summary>
        /// Any user execute permission.
        /// </summary>
        OthersExecute = 0x001,

        /// <summary>
        /// Any user write permission.
        /// </summary>
        OthersWrite = 0x002,

        /// <summary>
        /// Any user read permission.
        /// </summary>
        OthersRead = 0x004,

        /// <summary>
        /// Any user all permissions.
        /// </summary>
        OthersAll = OthersExecute | OthersWrite | OthersRead,

        /// <summary>
        /// Group execute permission.
        /// </summary>
        GroupExecute = 0x008,

        /// <summary>
        /// Group write permission.
        /// </summary>
        GroupWrite = 0x010,

        /// <summary>
        /// Group read permission.
        /// </summary>
        GroupRead = 0x020,

        /// <summary>
        /// Group all permissions.
        /// </summary>
        GroupAll = GroupExecute | GroupWrite | GroupRead,

        /// <summary>
        /// Owner execute permission.
        /// </summary>
        OwnerExecute = 0x040,

        /// <summary>
        /// Owner write permission.
        /// </summary>
        OwnerWrite = 0x080,

        /// <summary>
        /// Owner read permission.
        /// </summary>
        OwnerRead = 0x100,

        /// <summary>
        /// Owner all permissions.
        /// </summary>
        OwnerAll = OwnerExecute | OwnerWrite | OwnerRead,

        /// <summary>
        /// Sticky bit (meaning ill-defined).
        /// </summary>
        Sticky = 0x200,

        /// <summary>
        /// Set GUID on execute.
        /// </summary>
        SetGroupId = 0x400,

        /// <summary>
        /// Set UID on execute.
        /// </summary>
        SetUserId = 0x800
    }
}