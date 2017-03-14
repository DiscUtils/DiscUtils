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

namespace DiscUtils.Ntfs
{
    /// <summary>
    /// Enumeration of NTFS file attribute types.
    /// </summary>
    /// <remarks>Normally applications only create Data attributes.</remarks>
    public enum AttributeType
    {
        /// <summary>
        /// No type specified.
        /// </summary>
        None = 0x00,

        /// <summary>
        /// NTFS Standard Information.
        /// </summary>
        StandardInformation = 0x10,

        /// <summary>
        /// Attribute list, that holds a list of attribute locations for files with a large attribute set.
        /// </summary>
        AttributeList = 0x20,

        /// <summary>
        /// FileName information, one per hard link.
        /// </summary>
        FileName = 0x30,

        /// <summary>
        /// Distributed Link Tracking object identity.
        /// </summary>
        ObjectId = 0x40,

        /// <summary>
        /// Legacy Security Descriptor attribute.
        /// </summary>
        SecurityDescriptor = 0x50,

        /// <summary>
        /// The name of the NTFS volume.
        /// </summary>
        VolumeName = 0x60,

        /// <summary>
        /// Information about the NTFS volume.
        /// </summary>
        VolumeInformation = 0x70,

        /// <summary>
        /// File contents, a file may have multiple data attributes (default is unnamed).
        /// </summary>
        Data = 0x80,

        /// <summary>
        /// Root information for directories and other NTFS index's.
        /// </summary>
        IndexRoot = 0x90,

        /// <summary>
        /// For 'large' directories and other NTFS index's, the index contents.
        /// </summary>
        IndexAllocation = 0xA0,

        /// <summary>
        /// Bitmask of allocated clusters, records, etc - typically used in indexes.
        /// </summary>
        Bitmap = 0xB0,

        /// <summary>
        /// ReparsePoint information.
        /// </summary>
        ReparsePoint = 0xC0,

        /// <summary>
        /// Extended Attributes meta-information.
        /// </summary>
        ExtendedAttributesInformation = 0xD0,

        /// <summary>
        /// Extended Attributes data.
        /// </summary>
        ExtendedAttributes = 0xE0,

        /// <summary>
        /// Legacy attribute type from NT (not used).
        /// </summary>
        PropertySet = 0xF0,

        /// <summary>
        /// Encrypted File System (EFS) data.
        /// </summary>
        LoggedUtilityStream = 0x100
    }
}