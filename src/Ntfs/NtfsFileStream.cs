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
using DiscUtils.Ntfs.Attributes;

namespace DiscUtils.Ntfs
{
    internal sealed class NtfsFileStream : SparseStream
    {
        private NtfsFileSystem _fileSystem;
        private AttributeReference _attributeRef;
        private FileAccess _access;

        private File _file;
        private BaseAttribute _attr;
        private SparseStream _attrStream;

        public NtfsFileStream(NtfsFileSystem fileSystem, AttributeReference attribute, FileAccess access)
        {
            _fileSystem = fileSystem;
            _attributeRef = attribute;
            _access = access;

            _file = fileSystem.MasterFileTable.GetFile(_attributeRef.FileReference);
            _attr = _file.GetAttribute(_attributeRef.Type, _attributeRef.Name);
            _attrStream = _attr.Open(_access);
        }

        public override void Close()
        {
            base.Close();
            _attrStream.Close();
            _file.UpdateRecordInMft();
        }

        public override bool CanRead
        {
            get { return _access != FileAccess.Write; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return _access != FileAccess.Read; }
        }

        public override void Flush()
        {
            _attrStream.Flush();
            _file.UpdateRecordInMft();
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
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_attrStream.Position + count > _attrStream.Length)
            {
                if (!SoftCheckAttributeLengthIsOK(_attrStream.Position + count))
                {
                    throw new NotImplementedException("Convert stream to non-resident");
                }
            }

            _attrStream.Write(buffer, offset, count);
        }

        public override IEnumerable<StreamExtent> Extents
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Indicates if an attribute is getting too big to fit into the MFT record.
        /// </summary>
        /// <param name="newLength">The new length of the attribute</param>
        /// <returns><c>true</c> if the attribute possibly still fits, <c>false</c> if not.</returns>
        /// <remarks>This method indicates when it's sensible to make an attribute non-resident.  A
        /// firm decision is deferred until the MFT record is regenerated.</remarks>
        private bool SoftCheckAttributeLengthIsOK(long newLength)
        {
            if (_attr.IsNonResident)
            {
                return true;
            }

            return newLength < _file.MaxMftRecordSize;
        }
    }
}
