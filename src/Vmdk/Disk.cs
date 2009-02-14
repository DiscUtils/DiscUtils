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
using System.Collections.ObjectModel;
using System.IO;

namespace DiscUtils.Vmdk
{
    /// <summary>
    /// Represents a VMDK-backed disk.
    /// </summary>
    public class Disk : VirtualDisk
    {
        private DescriptorFile _descriptor;
        private SparseStream _contentStream;
        private FileLocator _extentLocator;

        /// <summary>
        /// Creates a new instance from a file on disk.
        /// </summary>
        /// <param name="path">The path to the disk</param>
        public Disk(string path)
        {
            using (FileStream s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                LoadDescriptor(s);
            }

            _extentLocator = new LocalFileLocator(Path.GetDirectoryName(path));
        }

        /// <summary>
        /// Disposes of this instance.
        /// </summary>
        /// <param name="disposing"><c>true</c> if disposing, <c>false</c> if in destructor</param>
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && _contentStream != null)
                {
                    _contentStream.Dispose();
                    _contentStream = null;
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Gets the Geometry of this disk.
        /// </summary>
        public override Geometry Geometry
        {
            get { return _descriptor.DiskGeometry; }
        }

        /// <summary>
        /// Gets the capacity of this disk (in bytes).
        /// </summary>
        public override long Capacity
        {
            get
            {
                long result = 0;
                foreach (var extent in _descriptor.Extents)
                {
                    result += extent.SizeInSectors * Sizes.Sector;
                }
                return result;
            }
        }

        /// <summary>
        /// Gets the contents of this disk as a stream.
        /// </summary>
        public override SparseStream Content
        {
            get
            {
                if (_contentStream == null)
                {
                    SparseStream[] streams = new SparseStream[_descriptor.Extents.Count];
                    for (int i = 0; i < streams.Length; ++i)
                    {
                        streams[i] = OpenExtent(_descriptor.Extents[i]);
                    }
                    _contentStream = new ConcatStream(streams);
                }
                return _contentStream;
            }
        }

        /// <summary>
        /// Gets the layers that make up the disk.
        /// </summary>
        public override ReadOnlyCollection<VirtualDiskLayer> Layers
        {
            get { throw new NotImplementedException(); }
        }

        private SparseStream OpenExtent(ExtentDescriptor extent)
        {
            FileAccess access = FileAccess.Read;
            FileShare share = FileShare.Read;
            if(extent.Access == ExtentAccess.ReadWrite)
            {
                access = FileAccess.ReadWrite;
                share = FileShare.ReadWrite;
            }

            switch (extent.Type)
            {
                case ExtentType.Flat:
                    return SparseStream.FromStream(_extentLocator.Open(extent.FileName, FileMode.Open, access, share), true);
                case ExtentType.Zero:
                    return new ZeroStream(extent.SizeInSectors * Utilities.SectorSize);
                case ExtentType.Sparse:
                    throw new NotImplementedException();
                default:
                    throw new NotSupportedException();
            }
        }

        private void LoadDescriptor(Stream s)
        {
            byte[] header = Utilities.ReadFully(s, (int)Math.Min(Sizes.Sector, s.Length));
            if (header.Length < Sizes.Sector || Utilities.ToUInt32LittleEndian(header, 0) != SparseExtentHeader.VmdkMagicNumber)
            {
                s.Position = 0;
                _descriptor = new DescriptorFile(s);
            }
            else
            {
                // This is a sparse disk extent, hopefully with embedded descriptor...
                SparseExtentHeader hdr = SparseExtentHeader.Read(header, 0);
                if (hdr.DescriptorOffset != 0)
                {
                    _descriptor = new DescriptorFile(new SubStream(s, hdr.DescriptorOffset * Sizes.Sector, hdr.DescriptorSize * Sizes.Sector));
                }
            }
        }
    }
}
