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

namespace DiscUtils.LogicalDiskManager
{
    internal abstract class DatabaseRecord
    {
        public string Signature; // VBLK
        public uint Label;
        public uint Counter;
        public uint Valid;
        public uint Flags;
        public RecordType RecordType;
        public uint DataLength;

        public ulong Id;
        public string Name;

        protected DatabaseRecord()
        {
        }

        public static DatabaseRecord ReadFrom(byte[] buffer, int offset)
        {
            DatabaseRecord result = null;

            if (Utilities.ToInt32BigEndian(buffer, offset + 0xC) != 0)
            {
                switch ((RecordType)(buffer[offset + 0x13] & 0xF))
                {
                    case RecordType.Volume:
                        result = new VolumeRecord();
                        break;

                    case RecordType.Component:
                        result = new ComponentRecord();
                        break;

                    case RecordType.Extent:
                        result = new ExtentRecord();
                        break;

                    case RecordType.Disk:
                        result = new DiskRecord();
                        break;

                    case RecordType.DiskGroup:
                        result = new DiskGroupRecord();
                        break;

                    default:
                        throw new NotImplementedException("Unrecognized record type: " + buffer[offset + 0x13]);
                }

                result.DoReadFrom(buffer, offset);
            }

            return result;
        }

        protected virtual void DoReadFrom(byte[] buffer, int offset)
        {
            Signature = Utilities.BytesToString(buffer, offset + 0x00, 4);
            Label = Utilities.ToUInt32BigEndian(buffer, offset + 0x04);
            Counter = Utilities.ToUInt32BigEndian(buffer, offset + 0x08);
            Valid = Utilities.ToUInt32BigEndian(buffer, offset + 0x0C);
            Flags = Utilities.ToUInt32BigEndian(buffer, offset + 0x10);
            RecordType = (RecordType)(Flags & 0xF);
            DataLength = Utilities.ToUInt32BigEndian(buffer, 0x14);
        }

        protected static ulong ReadVarULong(byte[] buffer, ref int offset)
        {
            int length = buffer[offset];


            ulong result = 0;
            for (int i = 0; i < length; ++i)
            {
                result = (result << 8) | buffer[offset + i + 1];
            }

            offset += length + 1;

            return result;
        }

        protected static long ReadVarLong(byte[] buffer, ref int offset)
        {
            return (long)ReadVarULong(buffer, ref offset);
        }

        protected static string ReadVarString(byte[] buffer, ref int offset)
        {
            int length = buffer[offset];

            string result = Utilities.BytesToString(buffer, offset + 1, length);
            offset += length + 1;
            return result;
        }

        protected static byte ReadByte(byte[] buffer, ref int offset)
        {
            return buffer[offset++];
        }

        protected static uint ReadUInt(byte[] buffer, ref int offset)
        {
            offset += 4;
            return Utilities.ToUInt32BigEndian(buffer, offset - 4);
        }

        protected static long ReadLong(byte[] buffer, ref int offset)
        {
            offset += 8;
            return Utilities.ToInt64BigEndian(buffer, offset - 8);
        }

        protected static ulong ReadULong(byte[] buffer, ref int offset)
        {
            offset += 8;
            return Utilities.ToUInt64BigEndian(buffer, offset - 8);
        }

        protected static string ReadString(byte[] buffer, int len, ref int offset)
        {
            offset += len;
            return Utilities.BytesToString(buffer, offset - len, len);
        }
    }
}
