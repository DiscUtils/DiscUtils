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

using System;
using System.Text;

namespace DiscUtils.Iso9660
{
    [Flags()]
    internal enum FileFlags : byte
    {
        None = 0x00,
        Hidden = 0x01,
        Directory = 0x02,
        AssociatedFile = 0x04,
        Record = 0x08,
        Protection = 0x10,
        MultiExtent = 0x80
    }

    internal struct DirectoryRecord
    {
        public byte ExtendedAttributeRecordLength;
        public uint LocationOfExtent;
        public uint DataLength;
        public DateTime RecordingDateAndTime;
        public FileFlags Flags;
        public byte FileUnitSize;
        public byte InterleaveGapSize;
        public ushort VolumeSequenceNumber;
        public String FileIdentifier;

        public DirectoryRecord(string name, FileFlags flags, uint extentLocation, uint dataLength)
        {
            ExtendedAttributeRecordLength = 0;
            LocationOfExtent = extentLocation;
            DataLength = dataLength;
            RecordingDateAndTime = DateTime.Now;
            Flags = flags;
            FileUnitSize = 0;
            InterleaveGapSize = 0;
            VolumeSequenceNumber = 1;
            FileIdentifier = name;
        }

        public static int ReadFrom(byte[] src, int offset, Encoding enc, out DirectoryRecord record)
        {
            int length = src[offset + 0];
            record.ExtendedAttributeRecordLength = src[offset + 1];
            record.LocationOfExtent = Utilities.ToUInt32FromBoth(src, offset + 2);
            record.DataLength = Utilities.ToUInt32FromBoth(src, offset + 10);
            record.RecordingDateAndTime = Utilities.ToUTCDateTimeFromDirectoryTime(src, offset + 18);
            record.Flags = (FileFlags)src[offset + 25];
            record.FileUnitSize = src[offset + 26];
            record.InterleaveGapSize = src[offset + 27];
            record.VolumeSequenceNumber = Utilities.ToUInt16FromBoth(src, offset + 28);
            byte lengthOfFileIdentifier = src[offset + 32];
            record.FileIdentifier = Utilities.ReadChars(src, offset + 33, lengthOfFileIdentifier, enc);

            return length;
        }


        internal int WriteTo(byte[] buffer, int offset, Encoding enc)
        {
            uint length = CalcLength(FileIdentifier, enc);
            buffer[offset] = (byte)length;
            buffer[offset + 1] = ExtendedAttributeRecordLength;
            Utilities.ToBothFromUInt32(buffer, offset + 2, LocationOfExtent);
            Utilities.ToBothFromUInt32(buffer, offset + 10, DataLength);
            Utilities.ToDirectoryTimeFromUTC(buffer, offset + 18, RecordingDateAndTime);
            buffer[offset + 25] = (byte)Flags;
            buffer[offset + 26] = FileUnitSize;
            buffer[offset + 27] = InterleaveGapSize;
            Utilities.ToBothFromUInt16(buffer, offset + 28, VolumeSequenceNumber);
            byte lengthOfFileIdentifier;

            lengthOfFileIdentifier = (byte)Utilities.WriteString(buffer, offset + 33, (int)(length - 33), false, FileIdentifier, enc);
#if false
            if ((Flags & FileFlags.Directory) != 0)
            {
                lengthOfFileIdentifier = Utilities.WriteDirectoryName(buffer, offset + 33, 255, FileIdentifier, enc);
            }
            else
            {
                lengthOfFileIdentifier = Utilities.WriteFileName(buffer, offset + 33, 255, FileIdentifier, enc);
            }
#endif
            buffer[offset + 32] = lengthOfFileIdentifier;
            return (int)length;
        }

        public static uint CalcLength(string name, Encoding enc)
        {
            int nameBytes = enc.GetByteCount(name);
            return (uint)(33 + nameBytes + (((nameBytes & 0x1) == 0) ? 1 : 0));
        }
    }

}
