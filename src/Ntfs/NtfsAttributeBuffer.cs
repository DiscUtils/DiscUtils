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

namespace DiscUtils.Ntfs
{
    internal class NtfsAttributeBuffer : Buffer
    {
        private File _file;
        private NtfsAttribute _attribute;

        public NtfsAttributeBuffer(File file, NtfsAttribute attribute)
        {
            _file = file;
            _attribute = attribute;
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return _file.Context.RawStream.CanWrite; }
        }

        public override long Capacity
        {
            get
            {
                return _attribute.Record.DataLength;
            }
        }

        public override int Read(long pos, byte[] buffer, int offset, int count)
        {
            var record = _attribute.Record;

            if (!CanRead)
            {
                throw new IOException("Attempt to read from file not opened for read");
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count", "Attempt to read negative number of bytes");
            }

            if (pos >= Capacity)
            {
                return 0;
            }

            // Limit read to length of attribute
            int totalToRead = (int)Math.Min(count, Capacity - pos);
            int toRead = totalToRead;

            // Handle uninitialized bytes at end of attribute
            if (pos + totalToRead > record.InitializedDataLength)
            {
                if (pos >= record.InitializedDataLength)
                {
                    // We're just reading zero bytes from the uninitialized area
                    Array.Clear(buffer, offset, totalToRead);
                    pos += totalToRead;
                    return totalToRead;
                }
                else
                {
                    // Partial read of uninitialized area
                    Array.Clear(buffer, offset + (int)(record.InitializedDataLength - pos), (int)((pos + toRead) - record.InitializedDataLength));
                    toRead = (int)(record.InitializedDataLength - pos);
                }
            }

            var extents = _attribute.Extents;

            int numRead = 0;
            while (numRead < toRead)
            {
                long extentStart;
                int extentIdx = GetExtent(pos + numRead, extents, out extentStart);

                IBuffer extentBuffer = extents[extentIdx].GetDataBuffer(_file);
                int justRead = extentBuffer.Read(pos + numRead - extentStart, buffer, offset + numRead, toRead - numRead);
                if (justRead == 0)
                {
                    break;
                }

                numRead += justRead;
            }

            return totalToRead;
        }

        public override void SetCapacity(long value)
        {
            var record = _attribute.Record;
            var extents = _attribute.Extents;

            if (!CanWrite)
            {
                throw new IOException("Attempt to change length of file not opened for write");
            }

            if (extents.Count > 1)
            {
                throw new NotImplementedException("Changing length of multi-extent stream");
            }
            IBuffer onlyExtentBuffer = extents[0].GetDataBuffer(_file);

            if (value == Capacity)
            {
                return;
            }

            _file.MarkMftRecordDirty();

            if (!record.IsNonResident)
            {
                onlyExtentBuffer.SetCapacity(value);
                _file.MarkMftRecordDirty();
            }
            else
            {
                long bytesPerCluster = _file.Context.BiosParameterBlock.BytesPerCluster;
                if (_file.Context.ClusterBitmap != null)
                {
                    long clusterLength = Utilities.RoundUp(value, bytesPerCluster);
                    onlyExtentBuffer.SetCapacity(clusterLength);
                    record.AllocatedLength = clusterLength;
                }

                record.DataLength = value;
                record.InitializedDataLength = Math.Min(record.InitializedDataLength, value);
            }
        }

        public override void Write(long pos, byte[] buffer, int offset, int count)
        {
            var record = _attribute.Record;
            var extents = _attribute.Extents;

            if (!CanWrite)
            {
                throw new IOException("Attempt to write to file not opened for write");
            }

            if (extents.Count > 1)
            {
                throw new NotImplementedException("Changing length of multi-extent stream");
            }

            if (record.Flags != AttributeFlags.None)
            {
                throw new NotImplementedException("Writing to compressed / sparse attributes");
            }

            if (count == 0)
            {
                return;
            }

            IBuffer onlyExtentBuffer = extents[0].GetDataBuffer(_file);

            if (!record.IsNonResident)
            {
                onlyExtentBuffer.Write(pos, buffer, offset, count);
                _file.MarkMftRecordDirty();
            }
            else
            {
                long bytesPerCluster = _file.Context.BiosParameterBlock.BytesPerCluster;

                if (pos + count > record.AllocatedLength)
                {
                    _file.MarkMftRecordDirty();

                    long clusterLength = Utilities.RoundUp(pos + count, bytesPerCluster);
                    onlyExtentBuffer.SetCapacity(clusterLength);
                    record.AllocatedLength = clusterLength;
                }

                // Write zeros from end of current initialized data to the start of the new write
                if (pos > record.InitializedDataLength + 1)
                {
                    _file.MarkMftRecordDirty();

                    byte[] wipeBuffer = new byte[bytesPerCluster * 4];
                    for (long wipePos = record.InitializedDataLength; wipePos < pos; wipePos += wipeBuffer.Length)
                    {
                        onlyExtentBuffer.Write(wipePos, wipeBuffer, 0, (int)Math.Min(wipeBuffer.Length, pos - wipePos));
                    }
                }

                onlyExtentBuffer.Write(pos, buffer, offset, count);

                if (pos + count > record.InitializedDataLength)
                {
                    _file.MarkMftRecordDirty();

                    record.InitializedDataLength = pos + count;
                }

                if (pos + count > record.DataLength)
                {
                    _file.MarkMftRecordDirty();

                    record.DataLength = pos + count;
                }
            }
        }

        public override IEnumerable<StreamExtent> GetExtentsInRange(long start, long count)
        {
            return StreamExtent.Intersect(
                new StreamExtent[] { new StreamExtent(0, Capacity) },
                new StreamExtent(start, count));
        }

        private int GetExtent(long targetPos, IList<AttributeRecord> extents, out long streamStartPos)
        {
            if(extents.Count <= 1)
            {
                streamStartPos = 0;
                return 0;
            }

            long vcn = targetPos / _file.Context.BiosParameterBlock.BytesPerCluster;
            for (int i = 0; i < extents.Count; ++i)
            {
                NonResidentAttributeRecord extent = (NonResidentAttributeRecord)extents[i];
                if (extent.StartVcn <= vcn && extent.LastVcn >= vcn)
                {
                    streamStartPos = extent.StartVcn * _file.Context.BiosParameterBlock.BytesPerCluster;
                    return i;
                }
            }

            throw new IOException("Attempt to access position outside of a known extent");
        }
    }
}
