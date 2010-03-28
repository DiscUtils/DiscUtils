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

            int numRead = 0;
            while (numRead < toRead)
            {
                long extentStart;
                IBuffer extentBuffer = GetExtentBuffer(pos + numRead, out extentStart);

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
            if (!CanWrite)
            {
                throw new IOException("Attempt to change length of file not opened for write");
            }

            if (value == Capacity)
            {
                return;
            }

            long bytesPerCluster = _file.Context.BiosParameterBlock.BytesPerCluster;
            long lastVcn = Utilities.Ceil(value, bytesPerCluster);

            Dictionary<AttributeReference, AttributeRecord> extentCache = new Dictionary<AttributeReference, AttributeRecord>(_attribute.Extents);
            foreach (var extent in extentCache)
            {
                if (extent.Value.StartVcn > lastVcn)
                {
                    extent.Value.GetDataBuffer(_file).SetCapacity(0);
                    _file.RemoveAttributeExtent(extent.Key);
                    _attribute.RemoveExtent(extent.Key);
                }
            }

            var record = _attribute.Record;
            var lastExtent = _attribute.LastExtent;
            IBuffer buffer = lastExtent.GetDataBuffer(_file);

            _file.MarkMftRecordDirty();

            if (!record.IsNonResident)
            {
                buffer.SetCapacity(value);
                _file.MarkMftRecordDirty();
            }
            else
            {
                if (_file.Context.ClusterBitmap != null)
                {
                    long clusterLength = lastVcn * bytesPerCluster;
                    buffer.SetCapacity(clusterLength);
                    record.AllocatedLength = clusterLength;
                }

                record.DataLength = value;
                record.InitializedDataLength = Math.Min(record.InitializedDataLength, value);
            }
        }

        public override void Write(long pos, byte[] buffer, int offset, int count)
        {
            var record = _attribute.Record;

            if (!CanWrite)
            {
                throw new IOException("Attempt to write to file not opened for write");
            }

            if (record.Flags != AttributeFlags.None)
            {
                throw new NotImplementedException("Writing to compressed / sparse attributes");
            }

            if (count == 0)
            {
                return;
            }


            if (!record.IsNonResident)
            {
                record.GetDataBuffer(_file).Write(pos, buffer, offset, count);
                _file.MarkMftRecordDirty();
            }
            else
            {
                NonResidentAttributeRecord lastExtent = (NonResidentAttributeRecord)_attribute.LastExtent;
                IBuffer lastExtentBuffer = lastExtent.GetDataBuffer(_file);

                long bytesPerCluster = _file.Context.BiosParameterBlock.BytesPerCluster;

                if (pos + count > record.AllocatedLength)
                {
                    _file.MarkMftRecordDirty();

                    long clusterLength = Utilities.RoundUp(pos + count, bytesPerCluster);
                    lastExtentBuffer.SetCapacity(clusterLength - (lastExtent.StartVcn * bytesPerCluster));
                    record.AllocatedLength = clusterLength;
                }

                // Write zeros from end of current initialized data to the start of the new write
                if (pos > record.InitializedDataLength + 1)
                {
                    _file.MarkMftRecordDirty();

                    byte[] wipeBuffer = new byte[bytesPerCluster * 4];

                    long wipePos = record.InitializedDataLength;
                    while(wipePos < pos)
                    {
                        long extentStartPos;
                        IBuffer extentBuffer = GetExtentBuffer(wipePos, out extentStartPos);

                        long writePos = wipePos - extentStartPos;
                        int numToWrite = (int)Math.Min(Math.Min(extentBuffer.Capacity - writePos, pos - wipePos), wipeBuffer.Length);
                        extentBuffer.Write(writePos, wipeBuffer, 0, numToWrite);
                        wipePos += numToWrite;
                    }
                }

                long focusPos = pos;
                int bytesWritten = 0;
                while (bytesWritten < count)
                {
                    long extentStartPos;
                    IBuffer extentBuffer = GetExtentBuffer(focusPos, out extentStartPos);

                    long writePos = focusPos - extentStartPos;
                    int numToWrite = (int)Math.Min(extentBuffer.Capacity - writePos, count - bytesWritten);
                    extentBuffer.Write(writePos, buffer, offset + bytesWritten, numToWrite);
                    focusPos += numToWrite;
                    bytesWritten += numToWrite;
                }

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

        private IBuffer GetExtentBuffer(long targetPos, out long streamStartPos)
        {
            AttributeRecord rec = null;

            if (_attribute.Extents.Count == 1)
            {
                // Handled as a special case, because sometimes _file can be null (for diagnostics)
                rec = _attribute.LastExtent;
                streamStartPos = 0;
            }
            else
            {
                long bytesPerCluster = _file.Context.BiosParameterBlock.BytesPerCluster;
                long vcn = targetPos / bytesPerCluster;

                NonResidentAttributeRecord nonResident = _attribute.GetNonResidentExtent(vcn);
                streamStartPos = nonResident.StartVcn * bytesPerCluster;
                rec = nonResident;
            }

            return rec.GetDataBuffer(_file);
        }
    }
}
