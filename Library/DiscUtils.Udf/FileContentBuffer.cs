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
using DiscUtils.Streams;

namespace DiscUtils.Udf
{
    internal class FileContentBuffer : IBuffer
    {
        private readonly uint _blockSize;
        private readonly UdfContext _context;
        private List<CookedExtent> _extents;
        private readonly FileEntry _fileEntry;
        private readonly Partition _partition;

        public FileContentBuffer(UdfContext context, Partition partition, FileEntry fileEntry, uint blockSize)
        {
            _context = context;
            _partition = partition;
            _fileEntry = fileEntry;
            _blockSize = blockSize;
            LoadExtents();
        }

        public bool CanRead
        {
            get { return true; }
        }

        public bool CanWrite
        {
            get { return false; }
        }

        public long Capacity
        {
            get { return (long)_fileEntry.InformationLength; }
        }

        public IEnumerable<StreamExtent> Extents
        {
            get { throw new NotImplementedException(); }
        }

        public int Read(long pos, byte[] buffer, int offset, int count)
        {
            if (_fileEntry.InformationControlBlock.AllocationType == AllocationType.Embedded)
            {
                byte[] srcBuffer = _fileEntry.AllocationDescriptors;
                if (pos > srcBuffer.Length)
                {
                    return 0;
                }

                int toCopy = (int)Math.Min(srcBuffer.Length - pos, count);
                Array.Copy(srcBuffer, (int)pos, buffer, offset, toCopy);
                return toCopy;
            }
            return ReadFromExtents(pos, buffer, offset, count);
        }

        public void Write(long pos, byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public void Clear(long pos, int count)
        {
            throw new NotSupportedException();
        }

        public void Flush() {}

        public void SetCapacity(long value)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<StreamExtent> GetExtentsInRange(long start, long count)
        {
            throw new NotImplementedException();
        }

        private void LoadExtents()
        {
            _extents = new List<CookedExtent>();
            byte[] activeBuffer = _fileEntry.AllocationDescriptors;

            AllocationType allocType = _fileEntry.InformationControlBlock.AllocationType;
            if (allocType == AllocationType.ShortDescriptors)
            {
                long filePos = 0;

                int i = 0;
                while (i < activeBuffer.Length)
                {
                    ShortAllocationDescriptor sad = EndianUtilities.ToStruct<ShortAllocationDescriptor>(activeBuffer, i);
                    if (sad.ExtentLength == 0)
                    {
                        break;
                    }

                    if (sad.Flags != ShortAllocationFlags.RecordedAndAllocated)
                    {
                        throw new NotImplementedException(
                            "Extents that are not 'recorded and allocated' not implemented");
                    }

                    CookedExtent newExtent = new CookedExtent
                    {
                        FileContentOffset = filePos,
                        Partition = int.MaxValue,
                        StartPos = sad.ExtentLocation * (long)_blockSize,
                        Length = sad.ExtentLength
                    };
                    _extents.Add(newExtent);

                    filePos += sad.ExtentLength;
                    i += sad.Size;
                }
            }
            else if (allocType == AllocationType.Embedded)
            {
                // do nothing
            }
            else if (allocType == AllocationType.LongDescriptors)
            {
                long filePos = 0;

                int i = 0;
                while (i < activeBuffer.Length)
                {
                    LongAllocationDescriptor lad = EndianUtilities.ToStruct<LongAllocationDescriptor>(activeBuffer, i);
                    if (lad.ExtentLength == 0)
                    {
                        break;
                    }

                    CookedExtent newExtent = new CookedExtent
                    {
                        FileContentOffset = filePos,
                        Partition = lad.ExtentLocation.Partition,
                        StartPos = lad.ExtentLocation.LogicalBlock * (long)_blockSize,
                        Length = lad.ExtentLength
                    };
                    _extents.Add(newExtent);

                    filePos += lad.ExtentLength;
                    i += lad.Size;
                }
            }
            else
            {
                throw new NotImplementedException("Allocation Type: " +
                                                  _fileEntry.InformationControlBlock.AllocationType);
            }
        }

        private int ReadFromExtents(long pos, byte[] buffer, int offset, int count)
        {
            int totalToRead = (int)Math.Min(Capacity - pos, count);
            int totalRead = 0;

            while (totalRead < totalToRead)
            {
                CookedExtent extent = FindExtent(pos + totalRead);

                long extentOffset = pos + totalRead - extent.FileContentOffset;
                int toRead = (int)Math.Min(totalToRead - totalRead, extent.Length - extentOffset);

                Partition part;
                if (extent.Partition != int.MaxValue)
                {
                    part = _context.LogicalPartitions[extent.Partition];
                }
                else
                {
                    part = _partition;
                }

                int numRead = part.Content.Read(extent.StartPos + extentOffset, buffer, offset + totalRead, toRead);
                if (numRead == 0)
                {
                    return totalRead;
                }

                totalRead += numRead;
            }

            return totalRead;
        }

        private CookedExtent FindExtent(long pos)
        {
            foreach (CookedExtent extent in _extents)
            {
                if (extent.FileContentOffset + extent.Length > pos)
                {
                    return extent;
                }
            }

            return null;
        }

        private class CookedExtent
        {
            public long FileContentOffset;
            public long Length;
            public int Partition;
            public long StartPos;
        }
    }
}