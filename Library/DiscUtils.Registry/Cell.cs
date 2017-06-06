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

using DiscUtils.Streams;

namespace DiscUtils.Registry
{
    /// <summary>
    /// Base class for the different kinds of cell present in a hive.
    /// </summary>
    internal abstract class Cell : IByteArraySerializable
    {
        public Cell(int index)
        {
            Index = index;
        }

        public int Index { get; set; }

        public abstract int Size { get; }

        public abstract int ReadFrom(byte[] buffer, int offset);

        public abstract void WriteTo(byte[] buffer, int offset);

        internal static Cell Parse(RegistryHive hive, int index, byte[] buffer, int pos)
        {
            string type = EndianUtilities.BytesToString(buffer, pos, 2);

            Cell result = null;

            switch (type)
            {
                case "nk":
                    result = new KeyNodeCell(index);
                    break;

                case "sk":
                    result = new SecurityCell(index);
                    break;

                case "vk":
                    result = new ValueCell(index);
                    break;

                case "lh":
                case "lf":
                    result = new SubKeyHashedListCell(hive, index);
                    break;

                case "li":
                case "ri":
                    result = new SubKeyIndirectListCell(hive, index);
                    break;

                default:
                    throw new RegistryCorruptException("Unknown cell type '" + type + "'");
            }

            result.ReadFrom(buffer, pos);
            return result;
        }
    }
}