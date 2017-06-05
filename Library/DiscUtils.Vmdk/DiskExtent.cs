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
using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.Vmdk
{
    internal sealed class DiskExtent : VirtualDiskExtent
    {
        private readonly FileAccess _access;
        private readonly ExtentDescriptor _descriptor;
        private readonly long _diskOffset;
        private readonly FileLocator _fileLocator;
        private readonly Stream _monolithicStream;

        public DiskExtent(ExtentDescriptor descriptor, long diskOffset, FileLocator fileLocator, FileAccess access)
        {
            _descriptor = descriptor;
            _diskOffset = diskOffset;
            _fileLocator = fileLocator;
            _access = access;
        }

        public DiskExtent(ExtentDescriptor descriptor, long diskOffset, Stream monolithicStream)
        {
            _descriptor = descriptor;
            _diskOffset = diskOffset;
            _monolithicStream = monolithicStream;
        }

        public override long Capacity
        {
            get { return _descriptor.SizeInSectors * Sizes.Sector; }
        }

        public override bool IsSparse
        {
            get
            {
                return _descriptor.Type == ExtentType.Sparse || _descriptor.Type == ExtentType.VmfsSparse ||
                       _descriptor.Type == ExtentType.Zero;
            }
        }

        public override long StoredSize
        {
            get
            {
                if (_monolithicStream != null)
                {
                    return _monolithicStream.Length;
                }
                using (
                    Stream s = _fileLocator.Open(_descriptor.FileName, FileMode.Open, FileAccess.Read,
                        FileShare.Read))
                {
                    return s.Length;
                }
            }
        }

        public override MappedStream OpenContent(SparseStream parent, Ownership ownsParent)
        {
            FileAccess access = FileAccess.Read;
            FileShare share = FileShare.Read;
            if (_descriptor.Access == ExtentAccess.ReadWrite && _access != FileAccess.Read)
            {
                access = FileAccess.ReadWrite;
                share = FileShare.None;
            }

            if (_descriptor.Type != ExtentType.Sparse && _descriptor.Type != ExtentType.VmfsSparse &&
                _descriptor.Type != ExtentType.Zero)
            {
                if (ownsParent == Ownership.Dispose && parent != null)
                {
                    parent.Dispose();
                }
            }
            else if (parent == null)
            {
                parent = new ZeroStream(_descriptor.SizeInSectors * Sizes.Sector);
            }

            if (_monolithicStream != null)
            {
                // Early-out for monolithic VMDKs
                return new HostedSparseExtentStream(
                    _monolithicStream,
                    Ownership.None,
                    _diskOffset,
                    parent,
                    ownsParent);
            }
            switch (_descriptor.Type)
            {
                case ExtentType.Flat:
                case ExtentType.Vmfs:
                    return MappedStream.FromStream(
                        _fileLocator.Open(_descriptor.FileName, FileMode.Open, access, share),
                        Ownership.Dispose);

                case ExtentType.Zero:
                    return new ZeroStream(_descriptor.SizeInSectors * Sizes.Sector);

                case ExtentType.Sparse:
                    return new HostedSparseExtentStream(
                        _fileLocator.Open(_descriptor.FileName, FileMode.Open, access, share),
                        Ownership.Dispose,
                        _diskOffset,
                        parent,
                        ownsParent);

                case ExtentType.VmfsSparse:
                    return new ServerSparseExtentStream(
                        _fileLocator.Open(_descriptor.FileName, FileMode.Open, access, share),
                        Ownership.Dispose,
                        _diskOffset,
                        parent,
                        ownsParent);

                default:
                    throw new NotSupportedException();
            }
        }
    }
}