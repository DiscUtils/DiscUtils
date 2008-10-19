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

        internal DirectoryEntry(byte[] data, int offset)
        {
            Load(data, offset);
        }

        private void Load(byte[] data, int offset)
        {
            _name = Encoding.ASCII.GetString(data, offset, 11);
            _attr = data[offset + 11];
            _creationTimeTenth = data[offset + 13];
            _creationTime = BitConverter.ToUInt16(data, offset + 14);
            _creationDate = BitConverter.ToUInt16(data, offset + 16);
            _lastAccessDate = BitConverter.ToUInt16(data, offset + 18);
            _firstClusterHi = BitConverter.ToUInt16(data, offset + 20);
            _lastWriteTime = BitConverter.ToUInt16(data, offset + 22);
            _lastWriteDate = BitConverter.ToUInt16(data, offset + 24);
            _firstClusterLo = BitConverter.ToUInt16(data, offset + 26);
            _fileSize = BitConverter.ToUInt32(data, offset + 28);
        }

        internal void WriteTo(Stream stream)
        {
            byte[] buffer = new byte[32];

            Array.Copy(Encoding.ASCII.GetBytes(_name), 0, buffer, 0, 11);
            buffer[11] = _attr;
            buffer[13] = _creationTimeTenth;
            Array.Copy(BitConverter.GetBytes((ushort)_creationTime), 0, buffer, 14, 2);
            Array.Copy(BitConverter.GetBytes((ushort)_creationDate), 0, buffer, 16, 2);
            Array.Copy(BitConverter.GetBytes((ushort)_lastAccessDate), 0, buffer, 18, 2);
            Array.Copy(BitConverter.GetBytes((ushort)_firstClusterHi), 0, buffer, 20, 2);
            Array.Copy(BitConverter.GetBytes((ushort)_lastWriteTime), 0, buffer, 22, 2);
            Array.Copy(BitConverter.GetBytes((ushort)_lastWriteDate), 0, buffer, 24, 2);
            Array.Copy(BitConverter.GetBytes((ushort)_firstClusterLo), 0, buffer, 26, 2);
            Array.Copy(BitConverter.GetBytes((uint)_fileSize), 0, buffer, 28, 4);

            stream.Write(buffer, 0, buffer.Length);
        }

        public string Name
        {
            get
            {
                return (_name.Substring(0, 8).TrimEnd(' ') + "." + _name.Substring(8).TrimEnd(' ')).TrimEnd('.');
            }
        }

        public string NormalizedName
        {
            get
            {
                return _name;
            }
        }

        public FatAttributes Attributes
        {
            get { return (FatAttributes)_attr; }
        }

        public DateTime CreationTime
        {
            get { return FileTimeToDateTime(_creationDate, _creationTime, _creationTimeTenth); }
        }

        public DateTime LastAccessTime
        {
            get { return FileTimeToDateTime(_lastAccessDate, 0, 0); }
        }

        public DateTime LastWriteTime
        {
            get { return FileTimeToDateTime(_lastWriteDate, _lastWriteTime, 0); }
            set { DateTimeToFileTime(value, ref _lastWriteDate, ref _lastWriteTime); }
        }

        public int FileSize
        {
            get { return (int)_fileSize; }
            set { _fileSize = (uint)value; }
        }

        public uint FirstCluster
        {
            get { return (uint)(_firstClusterHi << 16) | _firstClusterLo; }
        }

        private DateTime FileTimeToDateTime(ushort date, ushort time, byte tenths)
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

        private void DateTimeToFileTime(DateTime value, ref ushort date, ref ushort time)
        {
            if (value.Year < 1980)
            {
                value = FatFileSystem.Epoch;
            }

            date = (ushort)(((value.Year - 1980 << 9) & 0xFE00) | ((value.Month << 5) & 0x01E0) | (value.Day & 0x001F));
            time = (ushort)(((value.Hour << 11) & 0xF800) | ((value.Minute << 5) & 0x07E0) | ((value.Second / 2) & 0x001F));
        }

    }
}
