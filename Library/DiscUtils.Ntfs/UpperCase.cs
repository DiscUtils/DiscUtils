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
using System.Collections.Generic;
using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.Ntfs
{
    internal sealed class UpperCase : IComparer<string>
    {
        private readonly char[] _table;

        public UpperCase(File file)
        {
            using (Stream s = file.OpenStream(AttributeType.Data, null, FileAccess.Read))
            {
                _table = new char[s.Length / 2];

                byte[] buffer = StreamUtilities.ReadExact(s, (int)s.Length);

                for (int i = 0; i < _table.Length; ++i)
                {
                    _table[i] = (char)EndianUtilities.ToUInt16LittleEndian(buffer, i * 2);
                }
            }
        }

        public int Compare(string x, string y)
        {
            int compLen = Math.Min(x.Length, y.Length);
            for (int i = 0; i < compLen; ++i)
            {
                int result = _table[x[i]] - _table[y[i]];
                if (result != 0)
                {
                    return result;
                }
            }

            // Identical out to the shortest string, so length is now the
            // determining factor.
            return x.Length - y.Length;
        }

        public int Compare(byte[] x, int xOffset, int xLength, byte[] y, int yOffset, int yLength)
        {
            int compLen = Math.Min(xLength, yLength) / 2;
            for (int i = 0; i < compLen; ++i)
            {
                char xCh = (char)(x[xOffset + i * 2] | (x[xOffset + i * 2 + 1] << 8));
                char yCh = (char)(y[yOffset + i * 2] | (y[yOffset + i * 2 + 1] << 8));

                int result = _table[xCh] - _table[yCh];
                if (result != 0)
                {
                    return result;
                }
            }

            // Identical out to the shortest string, so length is now the
            // determining factor.
            return xLength - yLength;
        }

        internal static UpperCase Initialize(File file)
        {
            byte[] buffer = new byte[(char.MaxValue + 1) * 2];
            for (int i = char.MinValue; i <= char.MaxValue; ++i)
            {
                EndianUtilities.WriteBytesLittleEndian(char.ToUpperInvariant((char)i), buffer, i * 2);
            }

            using (Stream s = file.OpenStream(AttributeType.Data, null, FileAccess.ReadWrite))
            {
                s.Write(buffer, 0, buffer.Length);
            }

            return new UpperCase(file);
        }
    }
}