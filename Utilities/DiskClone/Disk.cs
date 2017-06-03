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
using DiscUtils;
using DiscUtils.Streams;
using Microsoft.Win32.SafeHandles;

namespace DiskClone
{
    class Disk : VirtualDisk
    {
        private string _path;
        private SafeFileHandle _handle;
        private SparseStream _stream;

        public Disk(uint number)
        {
            _path = @"\\.\PhysicalDrive" + number;
            _handle = Win32Wrapper.OpenFileHandle(_path);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_stream != null)
                {
                    _stream.Dispose();
                    _stream = null;
                }
            }

            if (!_handle.IsClosed)
            {
                _handle.Dispose();
            }

            base.Dispose(disposing);
        }

        public override SparseStream Content
        {
            get
            {
                if (_stream == null)
                {
                    _stream = new DiskStream(_handle);
                }
                return _stream;
            }
        }

        public override Geometry Geometry
        {
            get
            {
                return Geometry.FromCapacity(Capacity);
            }
        }

        public override Geometry BiosGeometry
        {
            get
            {
                NativeMethods.DiskGeometry diskGeometry = Win32Wrapper.GetDiskGeometry(_handle);
                return new Geometry((int)diskGeometry.Cylinders, diskGeometry.TracksPerCylinder, diskGeometry.SectorsPerTrack, diskGeometry.BytesPerSector);
            }
        }

        public override VirtualDiskClass DiskClass
        {
            get { return VirtualDiskClass.HardDisk; }
        }

        public override long Capacity
        {
            get
            {
                return Win32Wrapper.GetDiskCapacity(_handle);
            }
        }

        public override IEnumerable<VirtualDiskLayer> Layers
        {
            get { throw new NotImplementedException(); }
        }

        public override VirtualDiskTypeInfo DiskTypeInfo
        {
            get {
                return new VirtualDiskTypeInfo()
                {
                    Name="Physical",
                    Variant = "",
                    CanBeHardDisk = true,
                    DeterministicGeometry = false,
                    PreservesBiosGeometry = false,
                    CalcGeometry = c => Geometry.FromCapacity(c),
                };
            }
        }

        public override VirtualDisk CreateDifferencingDisk(DiscFileSystem fileSystem, string path)
        {
            throw new NotImplementedException();
        }

        public override VirtualDisk CreateDifferencingDisk(string path)
        {
            throw new NotImplementedException();
        }
    }
}
