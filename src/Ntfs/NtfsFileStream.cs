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
using System.Linq;
using System.Text;
using System.IO;

namespace DiscUtils.Ntfs
{
    internal sealed class NtfsFileStream : SparseStream
    {
        private DirectoryEntry _entry;

        private File _file;
        private ushort _attrId;
        private SparseStream _attrStream;

        private bool _isDirty;

        public NtfsFileStream(NtfsFileSystem fileSystem, DirectoryEntry entry, AttributeType attrType, string attrName, FileAccess access)
        {
            _entry = entry;

            _file = fileSystem.GetFile(entry.Reference);
            _attrId = _file.GetAttribute(attrType, attrName).Id;
            _attrStream = _file.OpenAttribute(_attrId, access);
        }

        public override void Close()
        {
            base.Close();
            _attrStream.Close();

            UpdateMetadata();
        }

        public override bool CanRead
        {
            get { return _attrStream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return _attrStream.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return _attrStream.CanWrite; }
        }

        public override void Flush()
        {
            _attrStream.Flush();

            UpdateMetadata();
        }

        public override long Length
        {
            get { return _attrStream.Length; }
        }

        public override long Position
        {
            get
            {
                return _attrStream.Position;
            }
            set
            {
                _attrStream.Position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _attrStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _attrStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            if (value != Length)
            {
                _isDirty = true;
                _attrStream.SetLength(value);
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _isDirty = true;
            _attrStream.Write(buffer, offset, count);
        }

        public override IEnumerable<StreamExtent> Extents
        {
            get { return _attrStream.Extents; }
        }

        private void UpdateMetadata()
        {
            if (_isDirty)
            {
                DateTime now = DateTime.UtcNow;

                // Update the standard information attribute - so it reflects the actual file state
                StructuredNtfsAttribute<StandardInformation> saAttr = (StructuredNtfsAttribute<StandardInformation>)_file.GetAttribute(AttributeType.StandardInformation);
                saAttr.Content.ModificationTime = now;
                saAttr.Content.MftChangedTime = now;
                saAttr.Content.LastAccessTime = now;
                saAttr.Save();

                // Update the directory entry used to open the file, so it's accurate
                _entry.UpdateFrom(_file);

                // Write attribute changes back to the Master File Table
                _file.UpdateRecordInMft();
                _isDirty = false;
            }
        }

    }
}
