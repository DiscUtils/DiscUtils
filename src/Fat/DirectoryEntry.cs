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

namespace DiscUtils.Fat
{
    internal class DirectoryEntry
    {
        private string _name;
        private byte _attr;
        private byte _creationTimeTenth;
        private ushort _creationTime;
        private ushort _creationDate;
        private ushort _lastAccessDate;
        private ushort _firstClusterHi;
        private ushort _lastWriteTime;
        private ushort _lastWriteDate;
        private ushort _firstClusterLo;
        private uint _fileSize;

        internal DirectoryEntry(Stream stream)
        {
            byte[] buffer = Utilities.ReadFully(stream, 32);
            Load(buffer, 0);
        }

        internal DirectoryEntry(string name, FatAttributes attrs)
        {
            _name = name;
            _attr = (byte)attrs;

        }

        internal DirectoryEntry(DirectoryEntry toCopy)
        {
            _name = toCopy._name;
            _attr = toCopy._attr;
            _creationTimeTenth = toCopy._creationTimeTenth;
            _creationTime = toCopy._creationTime;
            _creationDate = toCopy._creationDate;
            _lastAccessDate = toCopy._lastAccessDate;
            _firstClusterHi = toCopy._firstClusterHi;
            _lastWriteTime = toCopy._lastWriteTime;
            _firstClusterLo = toCopy._firstClusterLo;
            _fileSize = toCopy._fileSize;
        }

        private void Load(byte[] data, int offset)
        {
            _name = Utilities.BytesToString(data, offset, 11);
            _attr = data[offset + 11];
            _creationTimeTenth = data[offset + 13];
            _creationTime = Utilities.ToUInt16LittleEndian(data, offset + 14);
            _creationDate = Utilities.ToUInt16LittleEndian(data, offset + 16);
            _lastAccessDate = Utilities.ToUInt16LittleEndian(data, offset + 18);
            _firstClusterHi = Utilities.ToUInt16LittleEndian(data, offset + 20);
            _lastWriteTime = Utilities.ToUInt16LittleEndian(data, offset + 22);
            _lastWriteDate = Utilities.ToUInt16LittleEndian(data, offset + 24);
            _firstClusterLo = Utilities.ToUInt16LittleEndian(data, offset + 26);
            _fileSize = Utilities.ToUInt32LittleEndian(data, offset + 28);
        }

        internal void WriteTo(Stream stream)
        {
            byte[] buffer = new byte[32];

            Utilities.StringToBytes(_name, buffer, 0, 11);
            buffer[11] = _attr;
            buffer[13] = _creationTimeTenth;
            Utilities.WriteBytesLittleEndian((ushort)_creationTime, buffer, 14);
            Utilities.WriteBytesLittleEndian((ushort)_creationDate, buffer, 16);
            Utilities.WriteBytesLittleEndian((ushort)_lastAccessDate, buffer, 18);
            Utilities.WriteBytesLittleEndian((ushort)_firstClusterHi, buffer, 20);
            Utilities.WriteBytesLittleEndian((ushort)_lastWriteTime, buffer, 22);
            Utilities.WriteBytesLittleEndian((ushort)_lastWriteDate, buffer, 24);
            Utilities.WriteBytesLittleEndian((ushort)_firstClusterLo, buffer, 26);
            Utilities.WriteBytesLittleEndian((uint)_fileSize, buffer, 28);

            stream.Write(buffer, 0, buffer.Length);
        }

        public string Name
        {
            get
            {
                return (_name.Substring(0, 8).TrimEnd(' ') + "." + _name.Substring(8).TrimEnd(' ')).TrimEnd('.');
            }
            set
            {
                NormalizedName = FatUtilities.NormalizeFileName(value);
            }
        }

        internal string SearchName
        {
            get
            {
                return (_name.Substring(0, 8).TrimEnd(' ') + "." + _name.Substring(8).TrimEnd(' '));
            }
        }

        public string NormalizedName
        {
            get
            {
                return _name;
            }
            set
            {
                if (value.Length == 11)
                {
                    _name = value;
                }
                else
                {
                    throw new ArgumentException("Invalid normalized name");
                }
            }
        }

        public FatAttributes Attributes
        {
            get { return (FatAttributes)_attr; }
            set { _attr = (byte)value; }
        }

        public DateTime CreationTime
        {
            get { return FileTimeToDateTime(_creationDate, _creationTime, _creationTimeTenth); }
            set { DateTimeToFileTime(value, out _creationDate, out _creationTime, out _creationTimeTenth); }
        }

        public DateTime LastAccessTime
        {
            get { return FileTimeToDateTime(_lastAccessDate, 0, 0); }
            set { DateTimeToFileTime(value, out _lastAccessDate); }
        }

        public DateTime LastWriteTime
        {
            get { return FileTimeToDateTime(_lastWriteDate, _lastWriteTime, 0); }
            set { DateTimeToFileTime(value, out _lastWriteDate, out _lastWriteTime); }
        }

        public int FileSize
        {
            get { return (int)_fileSize; }
            set { _fileSize = (uint)value; }
        }

        public uint FirstCluster
        {
            get { return (uint)(_firstClusterHi << 16) | _firstClusterLo; }
            set
            {
                _firstClusterHi = (ushort)((value >> 16) & 0xFFFF);
                _firstClusterLo = (ushort)(value & 0xFFFF);
            }
        }

        private static DateTime FileTimeToDateTime(ushort date, ushort time, byte tenths)
        {
            if (date == 0 || date == 0xFFFF)
            {
                // Return Epoch - this is an invalid date
                return FatFileSystem.Epoch;
            }

            int year = 1980 + ((date & 0xFE00) >> 9);
            int month = (date & 0x01E0) >> 5;
            int day = date & 0x001F;
            int hour = (time & 0xF800) >> 11;
            int minute = (time & 0x07E0) >> 5;
            int second = ((time & 0x001F) * 2) + (tenths / 100);
            int millis = (tenths % 100) * 10;

            return new DateTime(year, month, day, hour, minute, second, millis);
        }

        private static void DateTimeToFileTime(DateTime value, out ushort date)
        {
            byte tenths;
            ushort time;
            DateTimeToFileTime(value, out date, out time, out tenths);
        }

        private static void DateTimeToFileTime(DateTime value, out ushort date, out ushort time)
        {
            byte tenths;
            DateTimeToFileTime(value, out date, out time, out tenths);
        }

        private static void DateTimeToFileTime(DateTime value, out ushort date, out ushort time, out byte tenths)
        {
            if (value.Year < 1980)
            {
                value = FatFileSystem.Epoch;
            }

            date = (ushort)(((value.Year - 1980 << 9) & 0xFE00) | ((value.Month << 5) & 0x01E0) | (value.Day & 0x001F));
            time = (ushort)(((value.Hour << 11) & 0xF800) | ((value.Minute << 5) & 0x07E0) | ((value.Second / 2) & 0x001F));
            tenths = (byte)(((value.Second % 2) * 100) + (value.Millisecond / 10));
        }

    }
}
