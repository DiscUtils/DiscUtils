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

        public string Name
        {
            get
            {
                return _name.Substring(0, 8).TrimEnd(' ') + "." + _name.Substring(8).TrimEnd(' ');
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
        }

        public int FileSize
        {
            get { return (int)_fileSize; }
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
                return new DateTime(1980, 1, 1);
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
    }
}
