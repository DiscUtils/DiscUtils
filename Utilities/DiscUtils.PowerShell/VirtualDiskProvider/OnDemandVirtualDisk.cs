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
using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.PowerShell.VirtualDiskProvider
{
    internal sealed class OnDemandVirtualDisk : VirtualDisk
    {
        private DiscFileSystem _fileSystem;
        private string _path;
        private FileAccess _access;

        public OnDemandVirtualDisk(string path, FileAccess access)
        {
            _path = path;
            _access = access;
        }

        public OnDemandVirtualDisk(DiscFileSystem fileSystem, string path, FileAccess access)
        {
            _fileSystem = fileSystem;
            _path = path;
            _access = access;
        }

        public bool IsValid
        {
            get
            {
                try
                {
                    using (VirtualDisk disk = OpenDisk())
                    {
                        return disk != null;
                    }
                }
                catch (IOException)
                {
                    return false;
                }
            }
        }

        public override Geometry Geometry
        {
            get
            {
                using (VirtualDisk disk = OpenDisk())
                {
                    return disk.Geometry;
                }
            }
        }

        public override VirtualDiskClass DiskClass
        {
            get
            {
                using (VirtualDisk disk = OpenDisk())
                {
                    return disk.DiskClass;
                }
            }
        }

        public override long Capacity
        {
            get
            {
                using (VirtualDisk disk = OpenDisk())
                {
                    return disk.Capacity;
                }
            }
        }

        public override VirtualDiskParameters Parameters
        {
            get
            {
                using (VirtualDisk disk = OpenDisk())
                {
                    return disk.Parameters;
                }
            }
        }

        public override SparseStream Content
        {
            get { return new StreamWrapper(_fileSystem, _path, _access); }
        }

        public override IEnumerable<VirtualDiskLayer> Layers
        {
            get { throw new NotSupportedException("Access to virtual disk layers is not implemented for on-demand disks"); }
        }

        public override VirtualDiskTypeInfo DiskTypeInfo
        {
            get {
                using (VirtualDisk disk = OpenDisk())
                {
                    return disk.DiskTypeInfo;
                }
            }
        }

        public override VirtualDisk CreateDifferencingDisk(DiscFileSystem fileSystem, string path)
        {
            using (VirtualDisk disk = OpenDisk())
            {
                return disk.CreateDifferencingDisk(fileSystem, path);
            }
        }

        public override VirtualDisk CreateDifferencingDisk(string path)
        {
            using (VirtualDisk disk = OpenDisk())
            {
                return disk.CreateDifferencingDisk(path);
            }
        }

        private VirtualDisk OpenDisk()
        {
            return VirtualDisk.OpenDisk(_fileSystem, _path, _access);
        }

        private class StreamWrapper : SparseStream
        {
            private DiscFileSystem _fileSystem;
            private string _path;
            private FileAccess _access;
            private long _position;

            public StreamWrapper(DiscFileSystem fileSystem, string path, FileAccess access)
            {
                _fileSystem = fileSystem;
                _path = path;
                _access = access;
            }

            public override IEnumerable<StreamExtent> Extents
            {
                get
                {
                    using (VirtualDisk disk = OpenDisk())
                    {
                        return new List<StreamExtent>(disk.Content.Extents);
                    }
                }
            }

            public override bool CanRead
            {
                get
                {
                    using (VirtualDisk disk = OpenDisk())
                    {
                        return disk.Content.CanRead;
                    }
                }
            }

            public override bool CanSeek
            {
                get
                {
                    using (VirtualDisk disk = OpenDisk())
                    {
                        return disk.Content.CanSeek;
                    }
                }
            }

            public override bool CanWrite
            {
                get
                {
                    using (VirtualDisk disk = OpenDisk())
                    {
                        return disk.Content.CanWrite;
                    }
                }
            }

            public override void Flush()
            {
            }

            public override long Length
            {
                get
                {
                    using (VirtualDisk disk = OpenDisk())
                    {
                        return disk.Content.Length;
                    }
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
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                using (VirtualDisk disk = OpenDisk())
                {
                    disk.Content.Position = _position;
                    return disk.Content.Read(buffer, offset, count);
                }
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                long effectiveOffset = offset;
                if (origin == SeekOrigin.Current)
                {
                    effectiveOffset += _position;
                }
                else if (origin == SeekOrigin.End)
                {
                    effectiveOffset += Length;
                }

                if (effectiveOffset < 0)
                {
                    throw new IOException("Attempt to move before beginning of disk");
                }
                else
                {
                    _position = effectiveOffset;
                    return _position;
                }
            }

            public override void SetLength(long value)
            {
                using (VirtualDisk disk = OpenDisk())
                {
                    disk.Content.SetLength(value);
                }
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                using (VirtualDisk disk = OpenDisk())
                {
                    disk.Content.Position = _position;
                    disk.Content.Write(buffer, offset, count);
                }
            }

            private VirtualDisk OpenDisk()
            {
                return VirtualDisk.OpenDisk(_fileSystem, _path, _access);
            }
        }
    }

}
