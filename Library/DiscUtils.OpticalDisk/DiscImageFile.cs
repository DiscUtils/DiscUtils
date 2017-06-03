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

namespace DiscUtils.OpticalDisk
{
    /// <summary>
    /// Represents a single optical disc image file.
    /// </summary>
    public sealed class DiscImageFile : VirtualDiskLayer
    {
        internal const int Mode1SectorSize = 2048;
        internal const int Mode2SectorSize = 2352;

        private readonly OpticalFormat _format;

        private IDisposable _toDispose;

        /// <summary>
        /// Initializes a new instance of the DiscImageFile class.
        /// </summary>
        /// <param name="stream">The stream to interpret.</param>
        public DiscImageFile(Stream stream)
            : this(stream, Ownership.None, OpticalFormat.None) {}

        /// <summary>
        /// Initializes a new instance of the DiscImageFile class.
        /// </summary>
        /// <param name="stream">The stream to interpret.</param>
        /// <param name="ownsStream">Indicates if the new instance should control the lifetime of the stream.</param>
        /// <param name="format">The disc image format.</param>
        public DiscImageFile(Stream stream, Ownership ownsStream, OpticalFormat format)
        {
            if (ownsStream == Ownership.Dispose)
            {
                _toDispose = stream;
            }

            if (format == OpticalFormat.None)
            {
                if (stream.Length % Mode1SectorSize == 0 && stream.Length % Mode2SectorSize != 0)
                {
                    _format = OpticalFormat.Mode1;
                }
                else if (stream.Length % Mode1SectorSize != 0 && stream.Length % Mode2SectorSize == 0)
                {
                    _format = OpticalFormat.Mode2;
                }
                else
                {
                    throw new IOException("Unable to detect optical disk format");
                }
            }
            else
            {
                _format = format;
            }

            Content = stream as SparseStream;
            if (Content == null)
            {
                Content = SparseStream.FromStream(stream, Ownership.None);
            }

            if (_format == OpticalFormat.Mode2)
            {
                Mode2Buffer converter = new Mode2Buffer(new StreamBuffer(Content, Ownership.None));
                Content = new BufferStream(converter, FileAccess.Read);
            }
        }

        internal override long Capacity
        {
            get { return Content.Length; }
        }

        internal SparseStream Content { get; private set; }

        /// <summary>
        /// Gets the Geometry of the disc.
        /// </summary>
        /// <remarks>
        /// Optical discs don't fit the CHS model, so dummy CHS data provided, but
        /// sector size is accurate.
        /// </remarks>
        public override Geometry Geometry
        {
            // Note external sector size is always 2048 - 2352 just has extra header
            // & error-correction info
            get { return new Geometry(1, 1, 1, Mode1SectorSize); }
        }

        /// <summary>
        /// Gets a value indicating if the layer only stores meaningful sectors.
        /// </summary>
        public override bool IsSparse
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether the file is a differencing disk.
        /// </summary>
        public override bool NeedsParent
        {
            get { return false; }
        }

        internal override FileLocator RelativeFileLocator
        {
            get { return null; }
        }

        /// <summary>
        /// Gets the content of this layer.
        /// </summary>
        /// <param name="parent">The parent stream (if any).</param>
        /// <param name="ownsParent">Controls ownership of the parent stream.</param>
        /// <returns>The content as a stream.</returns>
        public override SparseStream OpenContent(SparseStream parent, Ownership ownsParent)
        {
            if (ownsParent == Ownership.Dispose && parent != null)
            {
                parent.Dispose();
            }

            return SparseStream.FromStream(Content, Ownership.None);
        }

        /// <summary>
        /// Gets the possible locations of the parent file (if any).
        /// </summary>
        /// <returns>Array of strings, empty if no parent.</returns>
        public override string[] GetParentLocations()
        {
            return new string[0];
        }

        /// <summary>
        /// Disposes of underlying resources.
        /// </summary>
        /// <param name="disposing">Set to <c>true</c> if called within Dispose(),
        /// else <c>false</c>.</param>
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    if (_toDispose != null)
                    {
                        _toDispose.Dispose();
                        _toDispose = null;
                    }

                    if (Content != null)
                    {
                        Content.Dispose();
                        Content = null;
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}