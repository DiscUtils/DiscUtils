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

using System.Collections.ObjectModel;
using System.IO;

namespace DiscUtils.Xva
{
    /// <summary>
    /// Class representing a disk containing withing an XVA file.
    /// </summary>
    public class Disk : VirtualDisk
    {
        private VirtualMachine _vm;
        private string _id;
        private string _displayName;
        private string _location;
        private long _capacity;

        private Stream _content;

        internal Disk(VirtualMachine vm, string id, string displayname, string location, long capacity)
        {
            _vm = vm;
            _id = id;
            _displayName = displayname;
            _location = location;
            _capacity = capacity;
        }

        /// <summary>
        /// Disposes of this instance, freeing underlying resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> if running inside Dispose(), indicating
        /// graceful cleanup of all managed objects should be performed, or <c>false</c>
        /// if running inside destructor.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_content != null)
                {
                    _content.Dispose();
                    _content = null;
                }
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// The Unique id of the disk, as known by XenServer.
        /// </summary>
        public string Uuid
        {
            get { return _id; }
        }

        /// <summary>
        /// The display name of the disk, as shown by XenServer.
        /// </summary>
        public string DisplayName
        {
            get { return _displayName; }
        }

        /// <summary>
        /// Gets the disk's geometry.
        /// </summary>
        /// <remarks>The geometry is not stored with the disk, so this is at best
        /// a guess of the actual geometry.</remarks>
        public override Geometry Geometry
        {
            get { return Geometry.FromCapacity(_capacity); }
        }

        /// <summary>
        /// Gets the disk's capacity (in bytes).
        /// </summary>
        public override long Capacity
        {
            get { return _capacity; }
        }

        /// <summary>
        /// Gets the content of the disk as a stream.
        /// </summary>
        public override Stream Content
        {
            get
            {
                if (_content == null)
                {
                    _content = new DiskStream(_vm.Archive, _capacity, _location);
                }
                return _content;
            }
        }

        /// <summary>
        /// Gets the (single) layer of an XVA disk.
        /// </summary>
        public override ReadOnlyCollection<VirtualDiskLayer> Layers
        {
            get { return new ReadOnlyCollection<VirtualDiskLayer>(new VirtualDiskLayer[] { new DiskLayer(Capacity) }); }
        }
    }
}
