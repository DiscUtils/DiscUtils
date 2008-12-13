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

namespace DiscUtils.Ntfs
{
    internal class NonResidentAttributeStream : Stream
    {
        private Stream _fsStream;
        private long _bytesPerCluster;
        private CookedDataRun[] _runs;
        private NonResidentFileAttributeRecord _record;

        private FileAccess _access;
        private long _length;
        private long _position;

        private bool _atEOF;

        private byte[] _cachedDecompressedBlock;
        private long _cachedBlockStartVcn;

        public NonResidentAttributeStream(Stream fsStream, long bytesPerCluster, FileAccess access, NonResidentFileAttributeRecord record)
        {
            _fsStream = fsStream;
            _bytesPerCluster = bytesPerCluster;
            _runs = CookDataRuns(record.DataRuns);
            _record = record;
            _access = access;
            _length = record.DataLength;
        }

        public override bool CanRead
        {
            get { return _access == FileAccess.Read || _access == FileAccess.ReadWrite; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return _access == FileAccess.Write || _access == FileAccess.ReadWrite; }
        }

        public override void Flush()
        {
            ;
        }

        public override long Length
        {
            get { return _length; }
        }

        public override long Position
        {
            get
            {
                return _position;
            }
            set
            {
                _position = value;
                _atEOF = false;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!CanRead)
            {
                throw new IOException("Attempt to read from file not opened for read");
            }

            if (_atEOF)
            {
                throw new IOException("Attempt to read beyond end of file");
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count", "Attempt to read negative number of bytes");
            }

            if (_position >= _length)
            {
                _atEOF = true;
                return 0;
            }

            // Limit read to length of attribute
            int toRead = (int)Math.Min(count, _length - _position);

            // Handle uninitialized bytes at end of attribute
            if (_position + toRead > _record.InitializedDataLength)
            {
                if (_position >= _record.InitializedDataLength)
                {
                    // We're just reading zero bytes from the uninitialized area
                    Array.Clear(buffer, offset, toRead);
                    return toRead;
                }
                else
                {
                    // Partial read of uninitialized area
                    Array.Clear(buffer, offset + (int)(_record.InitializedDataLength - _position), (int)((_position + toRead) - _record.InitializedDataLength));
                    toRead = (int)(_record.InitializedDataLength - _position);
                }
            }

            int numRead;
            if (_record.Flags == FileAttributeFlags.None)
            {
                numRead = DoReadNormal(buffer, offset, toRead);
            }
            else if (_record.Flags == FileAttributeFlags.Compressed)
            {
                numRead = DoReadCompressed(buffer, offset, toRead);
            }
            else
            {
                throw new NotImplementedException("Sparse files");
            }

            _position += numRead;

            return numRead;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPos = offset;
            if (origin == SeekOrigin.Current)
            {
                newPos += _position;
            }
            else if (origin == SeekOrigin.End)
            {
                newPos += Length;
            }
            _position = newPos;
            _atEOF = false;
            return newPos;
        }

        private int DoReadNormal(byte[] buffer, int offset, int count)
        {
            long vcn = _position / _bytesPerCluster;
            int dataRunIdx = FindDataRun(vcn);
            RawRead(dataRunIdx, _position - (_runs[dataRunIdx].StartVcn * _bytesPerCluster), buffer, offset, count, true);
            return count;
        }

        private int DoReadCompressed(byte[] buffer, int offset, int count)
        {
            long compressionUnitLength = _record.CompressionUnitSize * _bytesPerCluster;

            long startVcn = (_position / compressionUnitLength) * _record.CompressionUnitSize;
            long targetCluster = (_position / _bytesPerCluster);
            long blockOffset = _position - (startVcn * _bytesPerCluster);

            int dataRunIdx = FindDataRun(startVcn);
            if (_runs[dataRunIdx].IsSparse)
            {
                int numBytes = (int)Math.Min(count, compressionUnitLength);
                Array.Clear(buffer, offset, numBytes);
                return numBytes;
            }
            else if (IsBlockCompressed(dataRunIdx, _record.CompressionUnitSize))
            {
                byte[] decompBuffer;
                if (_cachedDecompressedBlock != null && _cachedBlockStartVcn == dataRunIdx)
                {
                    decompBuffer = _cachedDecompressedBlock;
                }
                else
                {
                    byte[] compBuffer = new byte[compressionUnitLength];
                    RawRead(dataRunIdx, 0, compBuffer, 0, (int)compressionUnitLength, false);

                    decompBuffer = Decompress(compBuffer);

                    _cachedDecompressedBlock = decompBuffer;
                    _cachedBlockStartVcn = dataRunIdx;
                }

                int numBytes = (int)Math.Min(count, decompBuffer.Length - blockOffset);
                Array.Copy(decompBuffer, blockOffset, buffer, offset, numBytes);
                return numBytes;
            }
            else
            {
                // Whole block is uncompressed.

                // Skip forward to the data run containing the first cluster we need to read
                dataRunIdx = FindDataRun(targetCluster, dataRunIdx);

                // Read to the end of the compression cluster
                int numBytes = (int)Math.Min(count, compressionUnitLength - blockOffset);
                RawRead(dataRunIdx, _position - _runs[dataRunIdx].StartVcn, buffer, offset, numBytes, true);
                return numBytes;
            }
        }

        private byte[] Decompress(byte[] compBuffer)
        {
            const ushort SubBlockIsCompressedFlag = 0x8000;
            const ushort SubBlockSizeMask = 0x0fff;
            const ushort SubBlockSize = 0x1000;

            byte[] resultBuffer = new byte[compBuffer.Length];

            int sourceIdx = 0;
            int destIdx = 0;

            while (destIdx < resultBuffer.Length)
            {
                ushort header = Utilities.ToUInt16LittleEndian(compBuffer, sourceIdx);
                sourceIdx += 2;

                // Look for null-terminating sub-block header
                if (header == 0)
                {
                    break;
                }

                if ((header & SubBlockIsCompressedFlag) == 0)
                {
                    // not compressed
                    if ((header & SubBlockSizeMask) != SubBlockSize)
                    {
                        throw new IOException("Found short non-compressed sub-block");
                    }
                    Array.Copy(compBuffer, sourceIdx, resultBuffer, destIdx, (header & SubBlockSizeMask));
                    sourceIdx += (header & SubBlockSizeMask);
                    destIdx += (header & SubBlockSizeMask);
                }
                else
                {
                    // compressed
                    int destSubBlockStart = destIdx;
                    int srcSubBlockEnd = sourceIdx + (header & SubBlockSizeMask) + 1;
                    while (sourceIdx < srcSubBlockEnd)
                    {
                        byte tag = compBuffer[sourceIdx];
                        ++sourceIdx;

                        for (int token = 0; token < 8; ++token)
                        {
                            // We might have hit the end of the sub block whilst still working though
                            // a tag - abort if we have...
                            if (sourceIdx >= srcSubBlockEnd)
                            {
                                break;
                            }

                            if ((tag & 1) == 0)
                            {
                                resultBuffer[destIdx] = compBuffer[sourceIdx];
                                ++destIdx;
                                ++sourceIdx;
                            }
                            else
                            {
                                ushort lengthMask = 0xFFF;
                                ushort deltaShift = 12;
                                for (int i = (destIdx - destSubBlockStart) - 1; i >= 0x10; i >>= 1)
                                {
                                    lengthMask >>= 1;
                                    --deltaShift;
                                }

                                ushort phraseToken = Utilities.ToUInt16LittleEndian(compBuffer, sourceIdx);
                                sourceIdx += 2;

                                int destBackAddr = destIdx - (phraseToken >> deltaShift) - 1;
                                int length = (phraseToken & lengthMask) + 3;
                                for (int i = 0; i < length; ++i)
                                {
                                    resultBuffer[destIdx++] = resultBuffer[destBackAddr++];
                                }
                            }

                            tag >>= 1;
                        }
                    }

                    if (destIdx < destSubBlockStart + SubBlockSize)
                    {
                        // Zero buffer here in future, if not known to be zero's - this handles the case
                        // of a decompressed sub-block not being full-length
                        destIdx = destSubBlockStart + SubBlockSize;
                    }
                }
            }

            return resultBuffer;
        }

        /// <summary>
        /// Read data from one or more runs.
        /// </summary>
        /// <param name="startRunIdx">The start run index</param>
        /// <param name="startRunOffset">The first byte in the run to read (as byte offset)</param>
        /// <param name="data">The buffer to fill</param>
        /// <param name="dataOffset">Offset to first byte in buffer to fill</param>
        /// <param name="count">Number of bytes to read</param>
        /// <param name="clearHoles">Whether to zero-out sparse runs, or to just skip</param>
        private void RawRead(int startRunIdx, long startRunOffset, byte[] data, int dataOffset, int count, bool clearHoles)
        {
            int totalRead = 0;
            int runIdx = startRunIdx;
            long runOffset = startRunOffset;


            while (totalRead < count)
            {
                int toRead = (int)Math.Min(count - totalRead, (_runs[runIdx].Length * _bytesPerCluster) - runOffset);

                if (_runs[runIdx].IsSparse)
                {
                    if (clearHoles)
                    {
                        Array.Clear(data, dataOffset + totalRead, toRead);
                    }
                    totalRead += toRead;
                    runOffset = 0;
                    runIdx++;
                }
                else
                {
                    _fsStream.Position = (_runs[runIdx].StartLcn * _bytesPerCluster) + runOffset;
                    int numRead = _fsStream.Read(data, dataOffset + totalRead, toRead);
                    totalRead += numRead;
                    runOffset += numRead;

                    if (runOffset >= _runs[runIdx].Length * _bytesPerCluster)
                    {
                        runOffset = 0;
                        runIdx++;
                    }
                }
            }
        }

        private bool IsBlockCompressed(int startDataRunIdx, int compressionUnitSize)
        {
            int clustersRemaining = compressionUnitSize;
            int dataRunIdx = startDataRunIdx;

            while (clustersRemaining > 0)
            {
                // We're looking for this - a sparse record within compressionUnit Virtual Clusters
                // from the start of the compression unit.  If we don't find it, then the compression
                // unit is not actually compressed.
                if (_runs[dataRunIdx].IsSparse)
                {
                    return true;
                }
                if (_runs[dataRunIdx].Length > clustersRemaining)
                {
                    return false;
                }
                clustersRemaining -= (int)_runs[dataRunIdx].Length;
                dataRunIdx++;
            }

            return false;
        }

        private int FindDataRun(long targetVcn)
        {
            return FindDataRun(targetVcn, 0);
        }

        private int FindDataRun(long targetVcn, int startIdx)
        {
            for (int i = startIdx; i < _runs.Length; ++i)
            {
                if (_runs[i].StartVcn + _runs[i].Length > targetVcn)
                {
                    return i;
                }
            }

            throw new IOException("Looking for VCN outside or data runs");
        }

        private CookedDataRun[] CookDataRuns(DataRun[] runs)
        {
            CookedDataRun[] result = new CookedDataRun[runs.Length];

            long vcn = 0;
            long lcn = 0;
            for (int i = 0; i < runs.Length; ++i)
            {
                result[i] = new CookedDataRun(runs[i], vcn, lcn + runs[i].RunOffset);
                vcn += runs[i].RunLength;
                lcn += runs[i].RunOffset;
            }

            return result;
        }

        private class CookedDataRun
        {
            private long _startVcn;
            private long _startLcn;
            private DataRun _raw;

            public CookedDataRun(DataRun raw, long startVcn, long startLcn)
            {
                _raw = raw;
                _startVcn = startVcn;
                _startLcn = startLcn;

                if (startVcn < 0)
                {
                    throw new ArgumentOutOfRangeException("startVcn", startVcn, "VCN must be >= 0");
                }
                if (_startLcn < 0)
                {
                    throw new ArgumentOutOfRangeException("startLcn", startLcn, "LCN must be >= 0");
                }
            }

            public long StartVcn
            {
                get { return _startVcn; }
            }

            public long StartLcn
            {
                get { return _startLcn; }
            }

            public long Length
            {
                get { return (long)_raw.RunLength; }
            }

            public bool IsSparse
            {
                get { return _raw.IsSparse; }
            }
        }
    }
}
