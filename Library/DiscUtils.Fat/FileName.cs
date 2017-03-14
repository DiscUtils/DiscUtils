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

using System;
using System.Text;
using DiscUtils.Internal;

namespace DiscUtils.Fat
{
    internal sealed class FileName : IEquatable<FileName>
    {
        private const byte SpaceByte = 0x20;

        public static readonly FileName SelfEntryName =
            new FileName(new byte[] { 0x2E, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20 }, 0);

        public static readonly FileName ParentEntryName =
            new FileName(new byte[] { 0x2E, 0x2E, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20 }, 0);

        public static readonly FileName Null =
            new FileName(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 0);

        private static readonly byte[] InvalidBytes = { 0x22, 0x2A, 0x2B, 0x2C, 0x2E, 0x2F, 0x3A, 0x3B, 0x3C, 0x3D, 0x3E, 0x3F, 0x5B, 0x5C, 0x5D, 0x7C };

        private readonly byte[] _raw;

        public FileName(byte[] data, int offset)
        {
            _raw = new byte[11];
            Array.Copy(data, offset, _raw, 0, 11);
        }

        public FileName(string name, Encoding encoding)
        {
            _raw = new byte[11];
            byte[] bytes = encoding.GetBytes(name.ToUpperInvariant());

            int nameIdx = 0;
            int rawIdx = 0;
            while (nameIdx < bytes.Length && bytes[nameIdx] != '.' && rawIdx < _raw.Length)
            {
                byte b = bytes[nameIdx++];
                if (b < 0x20 || Contains(InvalidBytes, b))
                {
                    throw new ArgumentException("Invalid character in file name '" + (char)b + "'", nameof(name));
                }

                _raw[rawIdx++] = b;
            }

            if (rawIdx > 8)
            {
                throw new ArgumentException("File name too long '" + name + "'", nameof(name));
            }
            if (rawIdx == 0)
            {
                throw new ArgumentException("File name too short '" + name + "'", nameof(name));
            }

            while (rawIdx < 8)
            {
                _raw[rawIdx++] = SpaceByte;
            }

            if (nameIdx < bytes.Length && bytes[nameIdx] == '.')
            {
                ++nameIdx;
            }

            while (nameIdx < bytes.Length && rawIdx < _raw.Length)
            {
                byte b = bytes[nameIdx++];
                if (b < 0x20 || Contains(InvalidBytes, b))
                {
                    throw new ArgumentException("Invalid character in file extension '" + (char)b + "'", nameof(name));
                }

                _raw[rawIdx++] = b;
            }

            while (rawIdx < 11)
            {
                _raw[rawIdx++] = SpaceByte;
            }

            if (nameIdx != bytes.Length)
            {
                throw new ArgumentException("File extension too long '" + name + "'", nameof(name));
            }
        }

        public bool Equals(FileName other)
        {
            if (other == null)
            {
                return false;
            }

            return CompareRawNames(this, other) == 0;
        }

        public static FileName FromPath(string path, Encoding encoding)
        {
            return new FileName(Utilities.GetFileFromPath(path), encoding);
        }

        public static bool operator ==(FileName a, FileName b)
        {
            return CompareRawNames(a, b) == 0;
        }

        public static bool operator !=(FileName a, FileName b)
        {
            return CompareRawNames(a, b) != 0;
        }

        public string GetDisplayName(Encoding encoding)
        {
            return GetSearchName(encoding).TrimEnd('.');
        }

        public string GetSearchName(Encoding encoding)
        {
            return encoding.GetString(_raw, 0, 8).TrimEnd() + "." + encoding.GetString(_raw, 8, 3).TrimEnd();
        }

        public string GetRawName(Encoding encoding)
        {
            return encoding.GetString(_raw, 0, 11).TrimEnd();
        }

        public FileName Deleted()
        {
            byte[] data = new byte[11];
            Array.Copy(_raw, data, 11);
            data[0] = 0xE5;

            return new FileName(data, 0);
        }

        public bool IsDeleted()
        {
            return _raw[0] == 0xE5;
        }

        public bool IsEndMarker()
        {
            return _raw[0] == 0x00;
        }

        public void GetBytes(byte[] data, int offset)
        {
            Array.Copy(_raw, 0, data, offset, 11);
        }

        public override bool Equals(object other)
        {
            return Equals(other as FileName);
        }

        public override int GetHashCode()
        {
            int val = 0x1A8D3C4E;

            for (int i = 0; i < 11; ++i)
            {
                val = (val << 2) ^ _raw[i];
            }

            return val;
        }

        private static int CompareRawNames(FileName a, FileName b)
        {
            for (int i = 0; i < 11; ++i)
            {
                if (a._raw[i] != b._raw[i])
                {
                    return a._raw[i] - b._raw[i];
                }
            }

            return 0;
        }

        private static bool Contains(byte[] array, byte val)
        {
            foreach (byte b in array)
            {
                if (b == val)
                {
                    return true;
                }
            }

            return false;
        }
    }
}