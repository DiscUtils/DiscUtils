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

namespace DiscUtils.Ntfs
{
    internal class NonResidentAttributeExtentStream : SparseStream
    {
        private File _file;
        private Stream _fsStream;
        private long _bytesPerCluster;
        private NonResidentAttributeRecord _record;

        private FileAccess _access;
        private long _position;

        private bool _atEOF;

        private byte[] _cachedDecompressedBlock;
        private long _cachedBlockStartVcn;

        public NonResidentAttributeExtentStream(File file, FileAccess access, NonResidentAttributeRecord record)
        {
            _file = file;
            _fsStream = _file.Context.RawStream;
            _bytesPerCluster = file.Context.BiosParameterBlock.BytesPerCluster;
            _record = record;
            _access = access;
        }

        public override void Close()
        {
            base.Close();
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
        }

        public override long Length
        {
            get
            {
                return _record.CookedDataRuns.Count == 0 ? 0 : ((1 + _record.LastVcn - _record.StartVcn) * _bytesPerCluster);
            }
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

            if (_position >= Length)
            {
                _atEOF = true;
                return 0;
            }

            // Limit read to length of attribute
            int toRead = (int)Math.Min(count, Length - _position);

            int numRead;
            if (_record.Flags == AttributeFlags.None)
            {
                numRead = DoReadNormal(buffer, offset, toRead);
            }
            else if (_record.Flags == AttributeFlags.Compressed)
            {
                numRead = DoReadCompressed(buffer, offset, toRead);
            }
            else
            {
                numRead = DoReadSparse(buffer, offset, toRead);
            }

            _position += numRead;

            return numRead;
        }

        public override void SetLength(long value)
        {
            if (!CanWrite)
            {
                throw new IOException("Attempt to change length of file not opened for write");
            }

            if ((value % _bytesPerCluster) != 0)
            {
                throw new ArgumentException("Length not a multiple of cluster size", "value");
            }

            if (value == Length)
            {
                return;
            }

            _file.MarkMftRecordDirty();

            if (value < Length)
            {
                Truncate(value);
            }
            else
            {
                long numClusters = value / _bytesPerCluster;
                if (value > Length)
                {
                    long numToAllocate = numClusters - (_record.CookedDataRuns.Count == 0 ? 0 : (1 + _record.LastVcn - _record.StartVcn));
                    Tuple<long, long>[] runs = _file.Context.ClusterBitmap.AllocateClusters(numToAllocate, _record.NextCluster, _file.IndexInMft == MasterFileTable.MftIndex, numClusters);
                    foreach (var run in runs)
                    {
                        AddDataRun(run.First, run.Second);
                    }
                }
                _record.LastVcn = _record.StartVcn + numClusters - 1;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!CanWrite)
            {
                throw new IOException("Attempt to write to file not opened for write");
            }

            if (_record.Flags != AttributeFlags.None)
            {
                throw new NotImplementedException("Writing to compressed / sparse attributes");
            }

            if (count == 0)
            {
                return;
            }

            if (_position + count > Length)
            {
                SetLength(Utilities.RoundUp(_position + count, _bytesPerCluster));
            }

            RawWrite(_position, buffer, offset, count);
            _position += count;
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

        private void Truncate(long value)
        {
            if (value == 0)
            {
                RemoveAndFreeRuns(0);
                _record.LastVcn = 0;
            }
            else
            {
                var runs = _record.CookedDataRuns;
                int firstRunToDelete = FindDataRun((value - 1) / _bytesPerCluster) + 1;

                RemoveAndFreeRuns(firstRunToDelete);

                TruncateAndFreeRun(runs.Count - 1, value - runs[runs.Count - 1].StartVcn);

                _record.LastVcn = Utilities.Ceil(value, _bytesPerCluster) - 1;
            }
        }

        private void TruncateAndFreeRun(int index, long bytesRequired)
        {
            long firstClusterToFree = Utilities.Ceil(bytesRequired, _bytesPerCluster);

            var runs = _record.CookedDataRuns;
            long oldLength = runs[index].Length;
            runs[index].Length = firstClusterToFree;
            _file.Context.ClusterBitmap.FreeClusters(new Tuple<long, long>(runs[index].StartLcn + firstClusterToFree, oldLength - firstClusterToFree));
        }

        private void RemoveAndFreeRuns(int firstRunToDelete)
        {
            var runs = _record.CookedDataRuns;

            Tuple<long, long>[] deadRuns = new Tuple<long, long>[runs.Count - firstRunToDelete];
            for (int i = firstRunToDelete; i < runs.Count; ++i)
            {
                deadRuns[i - firstRunToDelete] = new Tuple<long, long>(runs[i].StartLcn, runs[i].Length);
            }

            RemoveDataRuns(firstRunToDelete, runs.Count - firstRunToDelete);
            _file.Context.ClusterBitmap.FreeClusters(deadRuns);
        }

        private int DoReadNormal(byte[] buffer, int offset, int count)
        {
            var runs = _record.CookedDataRuns;

            long vcn = _position / _bytesPerCluster;
            int dataRunIdx = FindDataRun(vcn);
            RawRead(dataRunIdx, _position - (runs[dataRunIdx].StartVcn * _bytesPerCluster), buffer, offset, count, true);
            return count;
        }

        private int DoReadSparse(byte[] buffer, int offset, int count)
        {
            var runs = _record.CookedDataRuns;

            long vcn = _position / _bytesPerCluster;
            int dataRunIdx = FindDataRun(vcn);
            long runOffset = _position - (runs[dataRunIdx].StartVcn * _bytesPerCluster);

            if (runs[dataRunIdx].IsSparse)
            {
                int numBytes = (int)Math.Min(count, (runs[dataRunIdx].Length * _bytesPerCluster) - runOffset);
                Array.Clear(buffer, offset, numBytes);
                return numBytes;
            }
            else
            {
                RawRead(dataRunIdx, runOffset, buffer, offset, count, true);
                return count;
            }
        }

        private int DoReadCompressed(byte[] buffer, int offset, int count)
        {
            var runs = _record.CookedDataRuns;

            long compressionUnitLength = _record.CompressionUnitSize * _bytesPerCluster;

            long startVcn = (_position / compressionUnitLength) * _record.CompressionUnitSize;
            long targetCluster = (_position / _bytesPerCluster);
            long blockOffset = _position - (startVcn * _bytesPerCluster);

            int dataRunIdx = FindDataRun(startVcn);
            if (runs[dataRunIdx].IsSparse)
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
                RawRead(dataRunIdx, _position - (runs[dataRunIdx].StartVcn * _bytesPerCluster), buffer, offset, numBytes, true);
                return numBytes;
            }
        }

        private static byte[] Decompress(byte[] compBuffer)
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
                    if ((header & SubBlockSizeMask) + 1 != SubBlockSize)
                    {
                        throw new IOException("Found short non-compressed sub-block");
                    }
                    Array.Copy(compBuffer, sourceIdx, resultBuffer, destIdx, SubBlockSize);
                    sourceIdx += SubBlockSize;
                    destIdx += SubBlockSize;
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
            var runs = _record.CookedDataRuns;

            int totalRead = 0;
            int runIdx = startRunIdx;
            long runOffset = startRunOffset;


            while (totalRead < count)
            {
                int toRead = (int)Math.Min(count - totalRead, (runs[runIdx].Length * _bytesPerCluster) - runOffset);

                if (runs[runIdx].IsSparse)
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
                    _fsStream.Position = (runs[runIdx].StartLcn * _bytesPerCluster) + runOffset;
                    int numRead = _fsStream.Read(data, dataOffset + totalRead, toRead);
                    totalRead += numRead;
                    runOffset += numRead;

                    if (runOffset >= runs[runIdx].Length * _bytesPerCluster)
                    {
                        runOffset = 0;
                        runIdx++;
                    }
                }
            }
        }

        /// <summary>
        /// Write data to one or more runs.
        /// </summary>
        /// <param name="position">Logical position within stream to start writing</param>
        /// <param name="data">The buffer to write</param>
        /// <param name="dataOffset">Offset to first byte in buffer to write</param>
        /// <param name="count">Number of bytes to write</param>
        private void RawWrite(long position, byte[] data, int dataOffset, int count)
        {
            var runs = _record.CookedDataRuns;
            long vcn = position / _bytesPerCluster;
            int runIdx = FindDataRun(vcn);
            long runOffset = position - (runs[runIdx].StartVcn * _bytesPerCluster);

            int totalWritten = 0;
            while (totalWritten < count)
            {
                int toWrite = (int)Math.Min(count - totalWritten, (runs[runIdx].Length * _bytesPerCluster) - runOffset);

                if (runs[runIdx].IsSparse)
                {
                    throw new NotImplementedException("Writing to sparse dataruns");
                }
                else
                {
                    _fsStream.Position = (runs[runIdx].StartLcn * _bytesPerCluster) + runOffset;
                    _fsStream.Write(data, dataOffset + totalWritten, toWrite);
                    totalWritten += toWrite;
                    runOffset += toWrite;

                    if (runOffset >= runs[runIdx].Length * _bytesPerCluster)
                    {
                        runOffset = 0;
                        runIdx++;
                    }
                }
            }
        }

        private bool IsBlockCompressed(int startDataRunIdx, int compressionUnitSize)
        {
            var runs = _record.CookedDataRuns;
            int clustersRemaining = compressionUnitSize;
            int dataRunIdx = startDataRunIdx;

            while (clustersRemaining > 0)
            {
                // We're looking for this - a sparse record within compressionUnit Virtual Clusters
                // from the start of the compression unit.  If we don't find it, then the compression
                // unit is not actually compressed.
                if (runs[dataRunIdx].IsSparse)
                {
                    return true;
                }
                if (runs[dataRunIdx].Length > clustersRemaining)
                {
                    return false;
                }
                clustersRemaining -= (int)runs[dataRunIdx].Length;
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
            var runs = _record.CookedDataRuns;

            for (int i = startIdx; i < runs.Count; ++i)
            {
                if (runs[i].StartVcn + runs[i].Length > targetVcn)
                {
                    return i;
                }
            }

            throw new IOException("Looking for VCN outside of data runs");
        }

        private void AddDataRun(long startLcn, long length)
        {
            var runs = _record.CookedDataRuns;
            long startVcn = 0;
            long prevLcn = 0;
            if (runs.Count > 0)
            {
                CookedDataRun tailRun = runs[runs.Count - 1];
                startVcn = tailRun.StartVcn + tailRun.Length;
                prevLcn = tailRun.StartLcn;

                // Continuation of last run...
                if (startLcn == prevLcn + tailRun.Length)
                {
                    tailRun.Length += length;
                    return;
                }
            }

            DataRun newRun = new DataRun(startLcn - prevLcn, length);
            CookedDataRun newCookedRun = new CookedDataRun(newRun, startVcn, prevLcn);

            _record.CookedDataRuns.Add(newCookedRun);
        }

        private void RemoveDataRuns(int index, int count)
        {
            _record.CookedDataRuns.RemoveRange(index, count);
        }

        public override IEnumerable<StreamExtent> Extents
        {
            get { return new StreamExtent[] { new StreamExtent(0, Length) }; }
        }
    }
}
