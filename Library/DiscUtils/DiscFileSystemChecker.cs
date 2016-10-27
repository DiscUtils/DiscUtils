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

namespace DiscUtils
{
    using System;
    using System.IO;

    /// <summary>
    /// Flags for the amount of detail to include in a report.
    /// </summary>
    [Flags]
    public enum ReportLevels
    {
        /// <summary>
        /// Report no information.
        /// </summary>
        None = 0x00,

        /// <summary>
        /// Report informational level items.
        /// </summary>
        Information = 0x01,

        /// <summary>
        /// Report warning level items.
        /// </summary>
        Warnings = 0x02,

        /// <summary>
        /// Report error level items.
        /// </summary>
        Errors = 0x04,

        /// <summary>
        /// Report all items.
        /// </summary>
        All = 0x07
    }

    /// <summary>
    /// Base class for objects that validate file system integrity.
    /// </summary>
    /// <remarks>Instances of this class do not offer the ability to fix/correct
    /// file system issues, just to perform a limited number of checks on
    /// integrity of the file system.</remarks>
    public abstract class DiscFileSystemChecker
    {
        /// <summary>
        /// Checks the integrity of a file system held in a stream.
        /// </summary>
        /// <param name="reportOutput">A report on issues found.</param>
        /// <param name="levels">The amount of detail to report.</param>
        /// <returns><c>true</c> if the file system appears valid, else <c>false</c>.</returns>
        public abstract bool Check(TextWriter reportOutput, ReportLevels levels);
    }
}
