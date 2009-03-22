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
        private ushort _lastKnownUsn;

        public FileAttributeStream(File file, ushort attrId, FileAccess access)
        {
            _file = file;
            _attrId = attrId;
            _access = access;

            _lastKnownUsn = _file.UpdateSequenceNumber;
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
                CheckUsn();
                return _wrapped.Extents;
            }
        }

        public override bool CanRead
        {
            get
            {
                CheckUsn();
                return _wrapped.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                CheckUsn();
                return _wrapped.CanSeek;
            }
        }

        public override bool CanWrite
        {
            get
            {
                CheckUsn();
                return _wrapped.CanWrite;
            }
        }

        public override void Flush()
        {
            CheckUsn();
            _wrapped.Flush();
        }

        public override long Length
        {
            get
            {
                CheckUsn();
                return _wrapped.Length;
            }
        }

        public override long Position
        {
            get
            {
                CheckUsn();
                return _wrapped.Position;
            }
            set
            {
                CheckUsn();
                _wrapped.Position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckUsn();
            return _wrapped.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            CheckUsn();
            return _wrapped.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            CheckUsn();
            _wrapped.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            ChangeAttributeResidencyByLength(_wrapped.Position + count);
            CheckUsn();
            _wrapped.Write(buffer, offset, count);
        }

        private void CheckUsn()
        {
            if (_lastKnownUsn != _file.UpdateSequenceNumber)
            {
                ReopenWrapped();
            }
        }

        private void ReopenWrapped()
        {
            long pos = _wrapped.Position;
            _wrapped.Dispose();
            _lastKnownUsn = _file.UpdateSequenceNumber;
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
