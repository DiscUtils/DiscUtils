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

using System;

namespace DiscUtils.Ewf
{
    /// <summary>
    /// The different sections that may be found in an EWF file.
    /// </summary>
    public enum SECTION_TYPE
    {
        /// <summary>
        /// Contains data about the overall acquisition.
        /// </summary>
        header,

        /// <summary>
        /// Contains data about the overall acquisition.
        /// </summary>
        header2,

        /// <summary>
        /// Unknown. LEF related.
        /// </summary>
        ltypes,

        /// <summary>
        /// Unknown. LEF related.
        /// </summary>
        ltree,
        
        /// <summary>
        /// Represents a session on optical media.
        /// </summary>
        session,

        /// <summary>
        /// Contains table of sectors where a read error occurred.
        /// </summary>
        error2,

        /// <summary>
        /// Unsure. Probably represents an incomplete image. (Remotely imaged?)
        /// </summary>
        incomplete,

        /// <summary>
        /// Contains data about the acquired device.
        /// </summary>
        disk,

        /// <summary>
        /// Contains info about the acquired volume.
        /// </summary>
        volume,

        /// <summary>
        /// The actual acquired data.
        /// </summary>
        sectors,

        /// <summary>
        /// Provides offsets in segment file for chunks.
        /// </summary>
        table,

        /// <summary>
        /// Backup of table section.
        /// </summary>
        table2,

        /// <summary>
        /// Informs that the next segment file is required.
        /// </summary>
        next,

        /// <summary>
        /// 
        /// </summary>
        data,

        /// <summary>
        /// Contains MD5/SHA1 of acquired data.
        /// </summary>
        digest,

        /// <summary>
        /// Contains MD5 of acquired data.
        /// </summary>
        hash,

        /// <summary>
        /// Informs that this is the last segment file.
        /// </summary>
        done
    }

    /// <summary>
    /// Specifies the source media of the acquisition.
    /// </summary>
    public enum MEDIA_TYPE
    {
        /// <summary>
        /// Is removable media.
        /// </summary>
        Removable = 0x00,

        /// <summary>
        /// Is fixed media.
        /// </summary>
        Fixed = 0x01,

        /// <summary>
        /// Is an optical disc.
        /// </summary>
        Disc = 0x03,

        /// <summary>
        /// Is a Logical Evidence File.
        /// </summary>
        LEF = 0x0E,

        /// <summary>
        /// Is a RAM acquisition.
        /// </summary>
        RAM = 0x10
    }

    /// <summary>
    /// Info about how the acquisition was achieved.
    /// </summary>
    [Flags]
    public enum MEDIA_FLAG
    {
        /// <summary>
        /// File is an Image File
        /// </summary>
        ImageFile = 0x01,

        /// <summary>
        /// File represents a physical device.
        /// </summary>
        Device = 0x02,

        /// <summary>
        /// Fastbloc write-blocker was used during acquisition.
        /// </summary>
        Fastbloc = 0x04,

        /// <summary>
        /// Tableau write-blocker was used during acquisition.
        /// </summary>
        Tableau = 0x08
    }

    /// <summary>
    /// Level of compression in use.
    /// </summary>
    public enum COMPRESSION
    {
        /// <summary>
        /// No compression used.
        /// </summary>
        None = 0x00,

        /// <summary>
        /// Average compression used. (Why do this??)
        /// </summary>
        Good = 0x01,

        /// <summary>
        /// Maximum compression used.
        /// </summary>
        Best = 0x02
    }
}
