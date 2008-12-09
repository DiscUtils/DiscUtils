//
// Copyright (c) 2008, Kenneth Bell
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
    internal enum AttributeType : uint
    {
        None = 0x00,
        StandardInformation = 0x10,
        AttributeList = 0x20,
        FileName = 0x30,
        ObjectId = 0x40,
        SecurityDescriptor = 0x50,
        VolumeName = 0x60,
        VolumeInformation = 0x70,
        Data = 0x80,
        IndexRoot = 0x90,
        IndexAllocation = 0xA0,
        Bitmap = 0xB0,
        ReparsePoint = 0xC0,
        ExtendedAttributesInformation = 0xD0,
        ExtendedAttributes = 0xE0,
        PropertySet = 0xF0,
        LoggedUtilityStream = 0x100
    }
}
