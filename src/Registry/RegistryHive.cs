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
using System.IO;
using System.Security.AccessControl;

namespace DiscUtils.Registry
{
    /// <summary>
    /// A registry hive.
    /// </summary>
    public sealed class RegistryHive : IDisposable
    {
        private const long BinStart = 4 * Sizes.OneKiB;

        private Stream _fileStream;
        private Ownership _ownsStream;
        private HiveHeader _header;
        private List<BinHeader> _bins;

        /// <summary>
        /// Creates a new instance from the contents of an existing stream.
        /// </summary>
        /// <param name="hive">The stream containing the registry hive</param>
        /// <remarks>
        /// The created object does not assume ownership of the stream.
        /// </remarks>
        public RegistryHive(Stream hive)
            : this(hive, Ownership.None)
        {
        }

        /// <summary>
        /// Creates a new instance from the contents of an existing stream.
        /// </summary>
        /// <param name="hive">The stream containing the registry hive</param>
        /// <param name="ownership">Whether the new object assumes object of the stream</param>
        public RegistryHive(Stream hive, Ownership ownership)
        {
            _fileStream = hive;
            _fileStream.Position = 0;
            _ownsStream = ownership;

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
        /// Disposes of this instance, freeing any underlying stream (if any).
        /// </summary>
        public void Dispose()
        {
            if (_fileStream != null && _ownsStream == Ownership.Dispose)
            {
                _fileStream.Dispose();
                _fileStream = null;
            }
        }

        /// <summary>
        /// Creates a new (empty) registry hive.
        /// </summary>
        /// <param name="stream">The stream to contain the new hive</param>
        /// <returns>The new hive</returns>
        /// <remarks>
        /// The returned object does not assume ownership of the stream.
        /// </remarks>
        public static RegistryHive Create(Stream stream)
        {
            return Create(stream, Ownership.None);
        }

        /// <summary>
        /// Creates a new (empty) registry hive.
        /// </summary>
        /// <param name="stream">The stream to contain the new hive</param>
        /// <param name="ownership">Whether the returned object owns the stream</param>
        /// <returns>The new hive</returns>
        public static RegistryHive Create(Stream stream, Ownership ownership)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream", "Attempt to create registry hive in null stream");
            }

            // Construct a file with minimal structure - hive header, plus one (empty) bin
            BinHeader binHeader = new BinHeader();
            binHeader.FileOffset = 0;
            binHeader.BinSize = (int)(4 * Sizes.OneKiB);

            HiveHeader hiveHeader = new HiveHeader();
            hiveHeader.Length = binHeader.BinSize;

            stream.Position = 0;

            byte[] buffer = new byte[hiveHeader.Size];
            hiveHeader.WriteTo(buffer, 0);
            stream.Write(buffer, 0, buffer.Length);

            buffer = new byte[binHeader.Size];
            binHeader.WriteTo(buffer, 0);
            stream.Position = BinStart;
            stream.Write(buffer, 0, buffer.Length);

            buffer = new byte[4];
            Utilities.WriteBytesLittleEndian(binHeader.BinSize - binHeader.Size, buffer, 0);
            stream.Write(buffer, 0, buffer.Length);

            // Make sure the file is initialized out to the end of the firs bin
            stream.Position = BinStart + binHeader.BinSize - 1;
            stream.WriteByte(0);

            // Temporary hive to perform construction of higher-level structures
            RegistryHive newHive = new RegistryHive(stream);
            KeyNodeCell rootCell = new KeyNodeCell("root", -1);
            rootCell.Flags = RegistryKeyFlags.Normal | RegistryKeyFlags.Root;
            newHive.UpdateCell(rootCell, true);

            RegistrySecurity sd = new RegistrySecurity();
            sd.SetSecurityDescriptorSddlForm("O:BAG:BAD:PAI(A;;KA;;;SY)(A;CI;KA;;;BA)", AccessControlSections.All);
            SecurityCell secCell = new SecurityCell(sd);
            newHive.UpdateCell(secCell, true);
            secCell.NextIndex = secCell.Index;
            secCell.PreviousIndex = secCell.Index;
            newHive.UpdateCell(secCell, false);

            rootCell.SecurityIndex = secCell.Index;
            newHive.UpdateCell(rootCell, false);

            // Ref the root cell from the hive header
            hiveHeader.RootCell = rootCell.Index;
            buffer = new byte[hiveHeader.Size];
            hiveHeader.WriteTo(buffer, 0);
            stream.Position = 0;
            stream.Write(buffer, 0, buffer.Length);

            // Finally, return the new hive
            return new RegistryHive(stream, ownership);
        }

        /// <summary>
        /// Creates a new (empty) registry hive.
        /// </summary>
        /// <param name="path">The file to create the new hive in</param>
        /// <returns>The new hive</returns>
        public static RegistryHive Create(string path)
        {
            return Create(new FileStream(path, FileMode.Create, FileAccess.ReadWrite), Ownership.Dispose);
        }

        /// <summary>
        /// Gets the root key in the registry hive.
        /// </summary>
        public RegistryKey Root
        {
            get { return new RegistryKey(this, GetCell<KeyNodeCell>(_header.RootCell)); }
        }

        internal K GetCell<K>(int index)
            where K : Cell
        {
            Bin bin = GetBin(index);

            if (bin != null)
            {
                return (K)bin.TryGetCell(index);
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
                bin.FreeCell(index);
            }
        }

        internal int UpdateCell(Cell cell, bool canRelocate)
        {
            if (cell.Index == -1 && canRelocate)
            {
                cell.Index = AllocateRawCell(cell.Size);
            }

            Bin bin = GetBin(cell.Index);

            if (bin != null)
            {
                if (bin.UpdateCell(cell))
                {
                    return cell.Index;
                }
                else if (canRelocate)
                {
                    int oldCell = cell.Index;
                    cell.Index = AllocateRawCell(cell.Size);
                    bin = GetBin(cell.Index);
                    if (!bin.UpdateCell(cell))
                    {
                        cell.Index = oldCell;
                        throw new RegistryCorruptException("Failed to migrate cell to new location");
                    }

                    FreeCell(oldCell);
                    return cell.Index;
                }
                else
                {
                    throw new ArgumentException("Can't update cell, needs relocation but relocation disabled", "canRelocate");
                }
            }
            else
            {
                throw new RegistryCorruptException("No bin found containing index: " + cell.Index);
            }
        }

        internal byte[] RawCellData(int index, int maxBytes)
        {
            Bin bin = GetBin(index);

            if (bin != null)
            {
                return bin.ReadRawCellData(index, maxBytes);
            }
            else
            {
                return null;
            }
        }

        internal bool WriteRawCellData(int index, byte[] data, int offset, int count)
        {
            Bin bin = GetBin(index);

            if (bin != null)
            {
                return bin.WriteRawCellData(index, data, offset, count);
            }
            else
            {
                throw new RegistryCorruptException("No bin found containing index: " + index);
            }
        }

        internal int AllocateRawCell(int capacity)
        {
            int minSize = Utilities.RoundUp(capacity + 4, 8); // Allow for size header and ensure multiple of 8

            // Incredibly inefficient algorithm...
            foreach (var binHeader in _bins)
            {
                Bin bin = LoadBin(binHeader);
                int cellIndex = bin.AllocateCell(minSize);

                if(cellIndex >= 0)
                {
                    return cellIndex;
                }
            }

            BinHeader newBinHeader = AllocateBin(minSize);
            Bin newBin = LoadBin(newBinHeader);
            return newBin.AllocateCell(minSize);
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
                return LoadBin(binHeader);
            }

            return null;
        }

        private Bin LoadBin(BinHeader binHeader)
        {
            _fileStream.Position = BinStart + binHeader.FileOffset;
            return new Bin(this, _fileStream);
        }

        private BinHeader AllocateBin(int minSize)
        {
            BinHeader lastBin = _bins[_bins.Count - 1];

            BinHeader newBinHeader = new BinHeader();
            newBinHeader.FileOffset = lastBin.FileOffset + lastBin.BinSize;
            newBinHeader.BinSize = Utilities.RoundUp(minSize + newBinHeader.Size, 4 * (int)Sizes.OneKiB);

            byte[] buffer = new byte[newBinHeader.Size];
            newBinHeader.WriteTo(buffer, 0);
            _fileStream.Position = BinStart + newBinHeader.FileOffset;
            _fileStream.Write(buffer, 0, buffer.Length);

            byte[] cellHeader = new byte[4];
            Utilities.WriteBytesLittleEndian(newBinHeader.BinSize - newBinHeader.Size, cellHeader, 0);
            _fileStream.Write(cellHeader, 0, 4);

            // Update hive with new length
            _header.Length = newBinHeader.FileOffset + newBinHeader.BinSize;
            _header.Timestamp = DateTime.UtcNow;
            _header.Sequence1++;
            _header.Sequence2++;
            _fileStream.Position = 0;
            byte[] hiveHeader = Utilities.ReadFully(_fileStream, _header.Size);
            _header.WriteTo(hiveHeader, 0);
            _fileStream.Position = 0;
            _fileStream.Write(hiveHeader, 0, hiveHeader.Length);

            // Make sure the file is initialized to desired position
            _fileStream.Position = BinStart + _header.Length - 1;
            _fileStream.WriteByte(0);

            _bins.Add(newBinHeader);
            return newBinHeader;
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
