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
    internal class NonResidentAttributeStream : SparseStream
    {
        private File _file;
        private FileAccess _access;
        private NonResidentAttributeRecord _record;
        private SparseStream[] _extents;

        private long _bytesPerCluster;
        private long _position;
        private bool _atEOF;

        public NonResidentAttributeStream(File file, FileAccess access, NonResidentAttributeRecord record, params SparseStream[] extents)
        {
            _file = file;
            _access = access;
            _record = record;
            _extents = extents;
            _bytesPerCluster = file.Context.BiosParameterBlock.BytesPerCluster;
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
                return _record.RealLength;
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
            int totalToRead = (int)Math.Min(count, Length - _position);
            int toRead = totalToRead;

            // Handle uninitialized bytes at end of attribute
            if (_position + totalToRead > _record.InitializedDataLength)
            {
                if (_position >= _record.InitializedDataLength)
                {
                    // We're just reading zero bytes from the uninitialized area
                    Array.Clear(buffer, offset, totalToRead);
                    _position += totalToRead;
                    return totalToRead;
                }
                else
                {
                    // Partial read of uninitialized area
                    Array.Clear(buffer, offset + (int)(_record.InitializedDataLength - _position), (int)((_position + toRead) - _record.InitializedDataLength));
                    toRead = (int)(_record.InitializedDataLength - _position);
                }
            }

            int numRead = 0;
            while (numRead < toRead)
            {
                long extentStart;
                int extentIdx = GetActiveExtent(out extentStart);

                _extents[extentIdx].Position = _position + numRead - extentStart;
                int justRead = _extents[extentIdx].Read(buffer, offset + numRead, toRead - numRead);
                if (justRead == 0)
                {
                    break;
                }

                numRead += justRead;
            }

            _position += totalToRead;

            return totalToRead;
        }

        public override void SetLength(long value)
        {
            if (!CanWrite)
            {
                throw new IOException("Attempt to change length of file not opened for write");
            }

            if (_extents.Length > 1)
            {
                throw new NotImplementedException("Changing length of multi-extent stream");
            }

            if (value == Length)
            {
                return;
            }

            _file.MarkMftRecordDirty();

            long clusterLength = Utilities.RoundUp(value, _bytesPerCluster);
            _extents[0].SetLength(clusterLength);

            _record.AllocatedLength = clusterLength;
            _record.RealLength = value;
            _record.InitializedDataLength = Math.Min(_record.InitializedDataLength, value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!CanWrite)
            {
                throw new IOException("Attempt to write to file not opened for write");
            }

            if (_extents.Length > 1)
            {
                throw new NotImplementedException("Changing length of multi-extent stream");
            }

            if (_record.Flags != AttributeFlags.None)
            {
                throw new NotImplementedException("Writing to compressed / sparse attributes");
            }

            if (count == 0)
            {
                return;
            }

            if (_position + count > _record.AllocatedLength)
            {
                _file.MarkMftRecordDirty();

                long clusterLength = Utilities.RoundUp(_position + count, _bytesPerCluster);
                _extents[0].SetLength(clusterLength);
                _record.AllocatedLength = clusterLength;
            }

            // Write zeros from end of current initialized data to the start of the new write
            if (_position > _record.InitializedDataLength + 1)
            {
                _file.MarkMftRecordDirty();

                byte[] wipeBuffer = new byte[_bytesPerCluster * 4];
                for (long wipePos = _record.InitializedDataLength; wipePos < _position; wipePos += wipeBuffer.Length)
                {
                    _extents[0].Position = wipePos;
                    _extents[0].Write(wipeBuffer, 0, (int)Math.Min(wipeBuffer.Length, _position - wipePos));
                }
            }

            _extents[0].Position = _position;
            _extents[0].Write(buffer, offset, count);

            if (_position + count > _record.InitializedDataLength)
            {
                _file.MarkMftRecordDirty();

                _record.InitializedDataLength = _position + count;
            }

            if (_position + count > _record.RealLength)
            {
                _file.MarkMftRecordDirty();

                _record.RealLength = _position + count;
            }

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

        public override IEnumerable<StreamExtent> Extents
        {
            get { throw new NotImplementedException(); }
        }

        private int GetActiveExtent(out long startPos)
        {
            return GetExtent(_position, out startPos);
        }

        private int GetExtent(long targetPos, out long streamStartPos)
        {
            // Find the stream that _position is within
            streamStartPos = 0;
            int focusStream = 0;
            while (focusStream < _extents.Length - 1 && streamStartPos + _extents[focusStream].Length <= targetPos)
            {
                streamStartPos += _extents[focusStream].Length;
                focusStream++;
            }

            return focusStream;
        }
    }
}
