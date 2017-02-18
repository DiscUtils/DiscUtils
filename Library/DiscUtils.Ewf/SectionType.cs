//
// Copyright (c) 2013, Adam Bridge
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

namespace DiscUtils.Ewf
{
    /// <summary>
    /// The different sections that may be found in an EWF file.
    /// </summary>
    public enum SectionType
    {
        /// <summary>
        /// Contains data about the overall acquisition.
        /// </summary>
        Header,

        /// <summary>
        /// Contains data about the overall acquisition.
        /// </summary>
        Header2,

        /// <summary>
        /// Unknown. LEF related.
        /// </summary>
        Ltypes,

        /// <summary>
        /// Unknown. LEF related.
        /// </summary>
        Ltree,

        /// <summary>
        /// Represents a session on optical media.
        /// </summary>
        Session,

        /// <summary>
        /// Contains table of sectors where a read error occurred.
        /// </summary>
        Error2,

        /// <summary>
        /// Unsure. Probably represents an incomplete image. (Remotely imaged?)
        /// </summary>
        Incomplete,

        /// <summary>
        /// Contains data about the acquired device.
        /// </summary>
        Disk,

        /// <summary>
        /// Contains info about the acquired volume.
        /// </summary>
        Volume,

        /// <summary>
        /// The actual acquired data.
        /// </summary>
        Sectors,

        /// <summary>
        /// Provides offsets in segment file for chunks.
        /// </summary>
        Table,

        /// <summary>
        /// Backup of table section.
        /// </summary>
        Table2,

        /// <summary>
        /// Informs that the next segment file is required.
        /// </summary>
        Next,

        /// <summary>
        /// 
        /// </summary>
        Data,

        /// <summary>
        /// Contains MD5/SHA1 of acquired data.
        /// </summary>
        Digest,

        /// <summary>
        /// Contains MD5 of acquired data.
        /// </summary>
        Hash,

        /// <summary>
        /// Informs that this is the last segment file.
        /// </summary>
        Done
    }
}
