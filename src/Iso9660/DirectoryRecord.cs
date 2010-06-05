//
// Copyright (c) 2008-2010, Kenneth Bell
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
using System.IO;
using System.Text;
using DiscUtils.Vfs;

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

    internal class DirectoryRecord : VfsDirEntry
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

        public static int ReadFrom(byte[] src, int offset, Encoding enc, out DirectoryRecord record)
        {
            int length = src[offset + 0];

            record = new DirectoryRecord();
            record.ExtendedAttributeRecordLength = src[offset + 1];
            record.LocationOfExtent = IsoUtilities.ToUInt32FromBoth(src, offset + 2);
            record.DataLength = IsoUtilities.ToUInt32FromBoth(src, offset + 10);
            record.RecordingDateAndTime = IsoUtilities.ToUTCDateTimeFromDirectoryTime(src, offset + 18);
            record.Flags = (FileFlags)src[offset + 25];
            record.FileUnitSize = src[offset + 26];
            record.InterleaveGapSize = src[offset + 27];
            record.VolumeSequenceNumber = IsoUtilities.ToUInt16FromBoth(src, offset + 28);
            byte lengthOfFileIdentifier = src[offset + 32];
            record.FileIdentifier = IsoUtilities.ReadChars(src, offset + 33, lengthOfFileIdentifier, enc);

            return length;
        }


        internal int WriteTo(byte[] buffer, int offset, Encoding enc)
        {
            uint length = CalcLength(FileIdentifier, enc);
            buffer[offset] = (byte)length;
            buffer[offset + 1] = ExtendedAttributeRecordLength;
            IsoUtilities.ToBothFromUInt32(buffer, offset + 2, LocationOfExtent);
            IsoUtilities.ToBothFromUInt32(buffer, offset + 10, DataLength);
            IsoUtilities.ToDirectoryTimeFromUTC(buffer, offset + 18, RecordingDateAndTime);
            buffer[offset + 25] = (byte)Flags;
            buffer[offset + 26] = FileUnitSize;
            buffer[offset + 27] = InterleaveGapSize;
            IsoUtilities.ToBothFromUInt16(buffer, offset + 28, VolumeSequenceNumber);
            byte lengthOfFileIdentifier;


            if (FileIdentifier.Length == 1 && FileIdentifier[0] <= 1)
            {
                buffer[offset + 33] = (byte)FileIdentifier[0];
                lengthOfFileIdentifier = 1;
            }
            else
            {
                lengthOfFileIdentifier = (byte)IsoUtilities.WriteString(buffer, offset + 33, (int)(length - 33), false, FileIdentifier, enc);
            }

            buffer[offset + 32] = lengthOfFileIdentifier;
            return (int)length;
        }

        public static uint CalcLength(string name, Encoding enc)
        {
            int nameBytes;
            if (name.Length == 1 && name[0] <= 1)
            {
                nameBytes = 1;
            }
            else
            {
                nameBytes = enc.GetByteCount(name);
            }

            return (uint)(33 + nameBytes + (((nameBytes & 0x1) == 0) ? 1 : 0));
        }

        public override bool IsDirectory
        {
            get { return (Flags & FileFlags.Directory) != 0; }
        }

        public override string FileName
        {
            get { return FileIdentifier; }
        }

        public override bool HasVfsTimeInfo
        {
            get { return true; }
        }

        public override DateTime LastAccessTimeUtc
        {
            get { return RecordingDateAndTime; }
        }

        public override DateTime LastWriteTimeUtc
        {
            get { return RecordingDateAndTime; }
        }

        public override DateTime CreationTimeUtc
        {
            get { return RecordingDateAndTime; }
        }

        public override bool HasVfsFileAttributes
        {
            get { return true; }
        }

        public override FileAttributes FileAttributes
        {
            get
            {
                FileAttributes attrs = FileAttributes.ReadOnly;

                if ((Flags & FileFlags.Directory) != 0)
                {
                    attrs |= FileAttributes.Directory;
                }

                if ((Flags & FileFlags.Hidden) != 0)
                {
                    attrs |= FileAttributes.Hidden;
                }

                return attrs;
            }
        }

        public override long UniqueCacheId
        {
            get { return (((long)LocationOfExtent) << 32) | DataLength; }
        }
    }

}
