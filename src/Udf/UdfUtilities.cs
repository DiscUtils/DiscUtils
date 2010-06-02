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
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace DiscUtils.Udf
{
    internal static class UdfUtilities
    {
        public static DateTime ParseTimestamp(byte[] buffer, int offset)
        {
            bool allZero = true;
            for (int i = 0; i < 12; ++i)
            {
                if (buffer[i] != 0)
                {
                    allZero = false;
                    break;
                }
            }

            if (allZero)
            {
                return DateTime.MinValue;
            }


            ushort typeAndZone = Utilities.ToUInt16LittleEndian(buffer, offset);

            int type = (typeAndZone >> 12) & 0x0F;
            int minutesWest = typeAndZone & 0xFFF;

            if ((minutesWest & 0x800) != 0)
            {
                minutesWest = (-1 & ~0xFFF) | minutesWest;
            }


            int year = ForceRange(1, 9999, Utilities.ToInt16LittleEndian(buffer, offset + 2));
            int month = ForceRange(1, 12, buffer[offset + 4]);
            int day = ForceRange(1, 31, buffer[offset + 5]);
            int hour = ForceRange(0, 23, buffer[offset + 6]);
            int min = ForceRange(0, 59, buffer[offset + 7]);
            int sec = ForceRange(0, 59, buffer[offset + 8]);
            int csec = ForceRange(0, 99, buffer[offset + 9]);
            int hmsec = ForceRange(0, 99, buffer[offset + 10]);
            int msec = ForceRange(0, 99, buffer[offset + 11]);

            try
            {
                DateTime baseTime = new DateTime(year, month, day, hour, min, sec, (10 * csec) + (hmsec / 10), DateTimeKind.Utc);
                return baseTime - TimeSpan.FromMinutes(minutesWest);
            }
            catch (ArgumentOutOfRangeException)
            {
                return DateTime.MinValue;
            }
        }

        public static string ReadDString(byte[] buffer, int offset, int count)
        {
            int byteLen = buffer[offset + count - 1];
            return ReadDCharacters(buffer, offset, byteLen);
        }

        public static string ReadDCharacters(byte[] buffer, int offset, int count)
        {
            if (count == 0)
            {
                return "";
            }

            byte alg = buffer[offset];

            if (alg != 8 && alg != 16)
            {
                throw new InvalidDataException("Corrupt compressed unicode string");
            }

            StringBuilder result = new StringBuilder(count);

            int pos = 1;
            while (pos < count)
            {
                char ch = '\0';

                if (alg == 16)
                {
                    ch = (char)(buffer[offset + pos] << 8);
                    pos++;
                }

                if (pos < count)
                {
                    ch |= (char)buffer[offset + pos];
                    pos++;
                }

                result.Append(ch);
            }

            return result.ToString();
        }

        public static byte[] ReadExtent(UdfContext context, LongAllocationDescriptor extent)
        {
            LogicalPartition partition = context.LogicalPartitions[extent.ExtentLocation.Partition];
            long pos = extent.ExtentLocation.LogicalBlock * partition.LogicalBlockSize;
            return Utilities.ReadFully(partition.Content, pos, (int)extent.ExtentLength);
        }

        private static short ForceRange(short min, short max, short val)
        {
            if (val < min)
            {
                return min;
            }
            else if (val > max)
            {
                return max;
            }
            return val;
        }

        private static byte ForceRange(byte min, byte max, byte val)
        {
            if (val < min)
            {
                return min;
            }
            else if (val > max)
            {
                return max;
            }
            return val;
        }

    }
}
