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
    /// <summary>
    /// Wrapper for Resident/Non-Resident attribute streams, that remains valid
    /// despite the attribute oscillating between resident and not.
    /// </summary>
    internal class FileAttributeStream : SparseStream
    {
        private File _file;
        private ushort _attrId;
        private FileAccess _access;

        private SparseStream _wrapped;
        private uint _lastKnownStreamChangeSeqNum;

        public FileAttributeStream(File file, ushort attrId, FileAccess access)
        {
            _file = file;
            _attrId = attrId;
            _access = access;

            _lastKnownStreamChangeSeqNum = _file.AttributeStreamChangeId;
            _wrapped = _file.GetAttribute(_attrId).OpenRaw(_access);
        }

        public override void Close()
        {
            base.Close();
            _wrapped.Close();
        }

        public override IEnumerable<StreamExtent> Extents
        {
            get
            {
                CheckStreamValid();
                return _wrapped.Extents;
            }
        }

        public override bool CanRead
        {
            get
            {
                CheckStreamValid();
                return _wrapped.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                CheckStreamValid();
                return _wrapped.CanSeek;
            }
        }

        public override bool CanWrite
        {
            get
            {
                CheckStreamValid();
                return _wrapped.CanWrite;
            }
        }

        public override void Flush()
        {
            CheckStreamValid();
            _wrapped.Flush();
        }

        public override long Length
        {
            get
            {
                CheckStreamValid();
                return _wrapped.Length;
            }
        }

        public override long Position
        {
            get
            {
                CheckStreamValid();
                return _wrapped.Position;
            }
            set
            {
                CheckStreamValid();
                _wrapped.Position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckStreamValid();
            return _wrapped.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            CheckStreamValid();
            return _wrapped.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            CheckStreamValid();
            _wrapped.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            ChangeAttributeResidencyByLength(_wrapped.Position + count);
            CheckStreamValid();
            _wrapped.Write(buffer, offset, count);
        }

        public override string ToString()
        {
            return _file.ToString() + ".attr[" + _attrId + "]";
        }

        private void CheckStreamValid()
        {
            if (_lastKnownStreamChangeSeqNum != _file.AttributeStreamChangeId)
            {
                ReopenWrapped();
            }
        }

        private void ReopenWrapped()
        {
            long pos = _wrapped.Position;
            _wrapped.Dispose();
            _lastKnownStreamChangeSeqNum = _file.AttributeStreamChangeId;
            _wrapped = _file.GetAttribute(_attrId).OpenRaw(_access);
            _wrapped.Position = pos;
        }

        /// <summary>
        /// Change attribute residency if it gets too big (or small).
        /// </summary>
        /// <param name="value">The new (anticipated) length of the stream</param>
        /// <remarks>Has hysteresis - the decision is based on the input and the current
        /// state, not the current state alone</remarks>
        private void ChangeAttributeResidencyByLength(long value)
        {
            NtfsAttribute attr = _file.GetAttribute(_attrId);
            if (!attr.IsNonResident && value >= _file.MaxMftRecordSize)
            {
                _file.MakeAttributeNonResident(_attrId, (int)Math.Min(value, _wrapped.Length));
                ReopenWrapped();
            }
            else if (attr.IsNonResident && value <= _file.MaxMftRecordSize / 4)
            {
                // Use of 1/4 of record size here is just a heuristic - the important thing is not to end up with
                // zero-length non-resident attributes
                _file.MakeAttributeResident(_attrId, (int)value);
                ReopenWrapped();
            }
        }
    }
}
