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

namespace DiscUtils.Vmdk
{
    /// <summary>
    /// Represents and extent from a sparse disk from 'hosted' software (VMWare Workstation, etc).
    /// </summary>
    /// <remarks>Hosted disks and server disks (ESX, etc) are subtly different formats.</remarks>
    internal class HostedSparseExtentStream : CommonSparseExtentStream
    {
        private HostedSparseExtentHeader _hostedHeader;

        public HostedSparseExtentStream(Stream file, bool ownsFile, long diskOffset, SparseStream parentDiskStream, bool ownsParentDiskStream)
        {
            _fileStream = file;
            _ownsFileStream = ownsFile;
            _diskOffset = diskOffset;
            _parentDiskStream = parentDiskStream;
            _ownsParentDiskStream = ownsParentDiskStream;

            file.Position = 0;
            byte[] firstSector = Utilities.ReadFully(file, Sizes.Sector);
            _hostedHeader = HostedSparseExtentHeader.Read(firstSector, 0);
            _header = _hostedHeader;

            if (_hostedHeader.CompressAlgorithm != 0)
            {
                throw new NotImplementedException("No support for compressed disks");
            }

            if ((_hostedHeader.Flags & (HostedSparseExtentFlags.MarkersInUse | HostedSparseExtentFlags.CompressedGrains)) != 0)
            {
                throw new NotImplementedException("No support for streamed disk format");
            }

            _gtCoverage = _header.NumGTEsPerGT * _header.GrainSize * Sizes.Sector;

            LoadGlobalDirectory();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            int totalWritten = 0;
            while (totalWritten < count)
            {
                int grainTable = (int)(_position / _gtCoverage);
                int grainTableOffset = (int)(_position - (grainTable * _gtCoverage));

                LoadGrainTable(grainTable);

                int grainSize = (int)(_header.GrainSize * Sizes.Sector);
                int grain = grainTableOffset / grainSize;
                int grainOffset = grainTableOffset - (grain * grainSize);

                if (_grainTable[grain] == 0)
                {
                    AllocateGrain(grainTable, grain);
                }

                int numToWrite = Math.Min(count - totalWritten, grainSize - grainOffset);
                _fileStream.Position = (_grainTable[grain] * Sizes.Sector) + grainOffset;
                _fileStream.Write(buffer, offset + totalWritten, numToWrite);

                _position += numToWrite;
                totalWritten += numToWrite;
            }
        }

        protected override void LoadGlobalDirectory()
        {
            base.LoadGlobalDirectory();

            if ((_hostedHeader.Flags & HostedSparseExtentFlags.RedundantGrainTable) != 0)
            {
                int numGTs = (int)Utilities.Ceil(_header.Capacity * Sizes.Sector, _gtCoverage);
                _redundantGlobalDirectory = new uint[numGTs];
                _fileStream.Position = _hostedHeader.RgdOffset * Sizes.Sector;
                byte[] gdAsBytes = Utilities.ReadFully(_fileStream, numGTs * 4);
                for (int i = 0; i < _globalDirectory.Length; ++i)
                {
                    _redundantGlobalDirectory[i] = Utilities.ToUInt32LittleEndian(gdAsBytes, i * 4);
                }
            }
        }

        private void AllocateGrain(int grainTable, int grain)
        {
            // Calculate start pos for new grain
            long grainStartPos = Utilities.RoundUp(_fileStream.Length, _header.GrainSize * Sizes.Sector);

            // Copy-on-write semantics, read the bytes from parent and write them out to this extent.
            _parentDiskStream.Position = _diskOffset + grainStartPos;
            byte[] content = Utilities.ReadFully(_parentDiskStream, (int)_header.GrainSize * Sizes.Sector);
            _fileStream.Position = grainStartPos;
            _fileStream.Write(content, 0, content.Length);

            LoadGrainTable(grainTable);
            _grainTable[grain] = (uint)(grainStartPos / Sizes.Sector);
            WriteGrainTable();
        }

        private void WriteGrainTable()
        {
            if (_grainTable == null)
            {
                throw new InvalidOperationException("No grain table loaded");
            }

            byte[] buffer = new byte[_header.NumGTEsPerGT * 4];
            for (int i = 0; i < _grainTable.Length; ++i)
            {
                Utilities.WriteBytesLittleEndian(_grainTable[i], buffer, i * 4);
            }

            _fileStream.Position = _globalDirectory[_currentGrainTable] * Sizes.Sector;
            _fileStream.Write(buffer, 0, buffer.Length);

            if (_redundantGlobalDirectory != null)
            {
                _fileStream.Position = _redundantGlobalDirectory[_currentGrainTable] * Sizes.Sector;
                _fileStream.Write(buffer, 0, buffer.Length);
            }
        }
    }
}
