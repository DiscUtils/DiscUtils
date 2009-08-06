//
// Copyright (c) 2008-2009, Kenneth Bell
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

namespace DiscUtils.Registry
{
    /// <summary>
    /// A registry hive.
    /// </summary>
    public sealed class RegistryHive
    {
        private const long BinStart = 4 * Sizes.OneKiB;

        private Stream _fileStream;

        private HiveHeader _header;

        private List<BinHeader> _bins;

        /// <summary>
        /// Creates a new instance from the contents of an existing stream.
        /// </summary>
        /// <param name="hive">The stream containing the registry hive</param>
        public RegistryHive(Stream hive)
        {
            _fileStream = hive;

            byte[] buffer = Utilities.ReadFully(_fileStream, HiveHeader.HeaderSize);

            _header = new HiveHeader();
            _header.ReadFrom(buffer, 0);

            _bins = new List<BinHeader>();
            int pos = 0;
            while (pos < _header.Length)
            {
                _fileStream.Position = BinStart + pos;
                byte[] headerBuffer = Utilities.ReadFully(_fileStream, BinHeader.HeaderSize);
                BinHeader header = new BinHeader();
                header.ReadFrom(headerBuffer, 0);
                _bins.Add(header);

                pos += header.BinSize;
            }
        }

        /// <summary>
        /// Gets the root key in the registry hive.
        /// </summary>
        public RegistryKey Root
        {
            get { return new RegistryKey(this, _header.RootCell, GetCell<KeyNodeCell>(_header.RootCell)); }
        }

        internal K GetCell<K>(int index)
            where K : Cell
        {
            Bin bin = GetBin(index);

            if (bin != null)
            {
                return (K)bin[index - bin.Header.FileOffset];
            }
            else
            {
                return null;
            }
        }

        internal void FreeCell(int index)
        {
            Bin bin = GetBin(index);

            if (bin != null)
            {
                bin.FreeCell(index - bin.Header.FileOffset);
            }
        }

        internal int UpdateCell(int index, Cell cell)
        {
            Bin bin = GetBin(index);

            if (bin != null)
            {
                if (bin.UpdateCell(index - bin.Header.FileOffset, cell))
                {
                    return index;
                }
                else
                {
                    throw new NotImplementedException("Migrating cell to new location");
                }
            }
            else
            {
                throw new IndexOutOfRangeException("No bin found containing index");
            }
        }

        internal byte[] RawCellData(int index, int maxBytes)
        {
            Bin bin = GetBin(index);

            if (bin != null)
            {
                return bin.RawCellData(index - bin.Header.FileOffset, maxBytes);
            }
            else
            {
                return null;
            }
        }

        internal void WriteRawCellData(int index, byte[] data, int offset, int count)
        {
            Bin bin = GetBin(index);

            if (bin != null)
            {
                bin.WriteRawCellData(index - bin.Header.FileOffset, data, offset, count);
            }
            else
            {
                throw new IndexOutOfRangeException("No bin found containing index");
            }
        }

        private BinHeader FindBin(int index)
        {
            int binsIdx = _bins.BinarySearch(null, new BinFinder(index));
            if (binsIdx >= 0)
            {
                return _bins[binsIdx];
            }

            return null;
        }

        private Bin GetBin(int cellIndex)
        {
            BinHeader binHeader = FindBin(cellIndex);

            if (binHeader != null)
            {
                _fileStream.Position = BinStart + binHeader.FileOffset;
                return new Bin(_fileStream);
            }

            return null;
        }

        private class BinFinder : IComparer<BinHeader>
        {
            private int _index;

            public BinFinder(int index)
            {
                _index = index;
            }

            #region IComparer<BinHeader> Members

            public int Compare(BinHeader x, BinHeader y)
            {
                if (x.FileOffset + x.BinSize < _index)
                {
                    return -1;
                }
                else if (x.FileOffset > _index)
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }

            #endregion
        }
    }
}
