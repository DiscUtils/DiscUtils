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
using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.Vmdk
{
    internal sealed class ServerSparseExtentStream : CommonSparseExtentStream
    {
        private readonly ServerSparseExtentHeader _serverHeader;

        public ServerSparseExtentStream(Stream file, Ownership ownsFile, long diskOffset, SparseStream parentDiskStream,
                                        Ownership ownsParentDiskStream)
        {
            _fileStream = file;
            _ownsFileStream = ownsFile;
            _diskOffset = diskOffset;
            _parentDiskStream = parentDiskStream;
            _ownsParentDiskStream = ownsParentDiskStream;

            file.Position = 0;
            byte[] firstSectors = StreamUtilities.ReadExact(file, Sizes.Sector * 4);
            _serverHeader = ServerSparseExtentHeader.Read(firstSectors, 0);
            _header = _serverHeader;

            _gtCoverage = _header.NumGTEsPerGT * _header.GrainSize * Sizes.Sector;

            LoadGlobalDirectory();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            CheckDisposed();

            if (_position + count > Length)
            {
                throw new IOException("Attempt to write beyond end of stream");
            }

            int totalWritten = 0;
            while (totalWritten < count)
            {
                int grainTable = (int)(_position / _gtCoverage);
                int grainTableOffset = (int)(_position - grainTable * _gtCoverage);

                if (!LoadGrainTable(grainTable))
                {
                    AllocateGrainTable(grainTable);
                }

                int grainSize = (int)(_header.GrainSize * Sizes.Sector);
                int startGrain = grainTableOffset / grainSize;
                int startGrainOffset = grainTableOffset - startGrain * grainSize;

                int numGrains = 0;
                while (startGrain + numGrains < _header.NumGTEsPerGT
                       && numGrains * grainSize - startGrainOffset < count - totalWritten
                       && GetGrainTableEntry(startGrain + numGrains) == 0)
                {
                    ++numGrains;
                }

                if (numGrains != 0)
                {
                    AllocateGrains(grainTable, startGrain, numGrains);
                }
                else
                {
                    numGrains = 1;
                }

                int numToWrite = Math.Min(count - totalWritten, grainSize * numGrains - startGrainOffset);
                _fileStream.Position = (long)GetGrainTableEntry(startGrain) * Sizes.Sector + startGrainOffset;
                _fileStream.Write(buffer, offset + totalWritten, numToWrite);

                _position += numToWrite;
                totalWritten += numToWrite;
            }

            _atEof = _position == Length;
        }

        private void AllocateGrains(int grainTable, int grain, int count)
        {
            // Calculate start pos for new grain
            long grainStartPos = (long)_serverHeader.FreeSector * Sizes.Sector;

            // Copy-on-write semantics, read the bytes from parent and write them out to this extent.
            _parentDiskStream.Position = _diskOffset +
                                         (grain + _header.NumGTEsPerGT * grainTable) * _header.GrainSize *
                                         Sizes.Sector;
            byte[] content = StreamUtilities.ReadExact(_parentDiskStream, (int)(_header.GrainSize * Sizes.Sector * count));
            _fileStream.Position = grainStartPos;
            _fileStream.Write(content, 0, content.Length);

            // Update next-free-sector in disk header
            _serverHeader.FreeSector += (uint)MathUtilities.Ceil(content.Length, Sizes.Sector);
            byte[] headerBytes = _serverHeader.GetBytes();
            _fileStream.Position = 0;
            _fileStream.Write(headerBytes, 0, headerBytes.Length);

            LoadGrainTable(grainTable);
            for (int i = 0; i < count; ++i)
            {
                SetGrainTableEntry(grain + i, (uint)(grainStartPos / Sizes.Sector + _header.GrainSize * i));
            }

            WriteGrainTable();
        }

        private void AllocateGrainTable(int grainTable)
        {
            // Write out new blank grain table.
            uint startSector = _serverHeader.FreeSector;

            byte[] emptyGrainTable = new byte[_header.NumGTEsPerGT * 4];
            _fileStream.Position = startSector * (long)Sizes.Sector;
            _fileStream.Write(emptyGrainTable, 0, emptyGrainTable.Length);

            // Update header
            _serverHeader.FreeSector += (uint)MathUtilities.Ceil(emptyGrainTable.Length, Sizes.Sector);
            byte[] headerBytes = _serverHeader.GetBytes();
            _fileStream.Position = 0;
            _fileStream.Write(headerBytes, 0, headerBytes.Length);

            // Update the global directory
            _globalDirectory[grainTable] = startSector;
            WriteGlobalDirectory();

            _grainTable = new byte[_header.NumGTEsPerGT * 4];
            _currentGrainTable = grainTable;
        }

        private void WriteGlobalDirectory()
        {
            byte[] buffer = new byte[_globalDirectory.Length * 4];
            for (int i = 0; i < _globalDirectory.Length; ++i)
            {
                EndianUtilities.WriteBytesLittleEndian(_globalDirectory[i], buffer, i * 4);
            }

            _fileStream.Position = _serverHeader.GdOffset * Sizes.Sector;
            _fileStream.Write(buffer, 0, buffer.Length);
        }

        private void WriteGrainTable()
        {
            if (_grainTable == null)
            {
                throw new InvalidOperationException("No grain table loaded");
            }

            _fileStream.Position = _globalDirectory[_currentGrainTable] * (long)Sizes.Sector;
            _fileStream.Write(_grainTable, 0, _grainTable.Length);
        }
    }
}