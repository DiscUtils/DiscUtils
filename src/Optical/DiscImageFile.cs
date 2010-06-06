//
// Copyright (c) 2008-2010, Kenneth Bell
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
using DiscUtils.Partitions;

namespace DiscUtils.Optical
{
    /// <summary>
    /// Represents a single optical disc image file.
    /// </summary>
    public sealed class DiscImageFile : VirtualDiskLayer
    {
        internal const int Mode1SectorSize = 2048;
        internal const int Mode2SectorSize = 2352;

        private IDisposable _toDispose;

        private SparseStream _content;
        private OpticalFormat _format;

        /// <summary>
        /// Creates a new instance from a stream.
        /// </summary>
        /// <param name="stream">The stream to interpret</param>
        public DiscImageFile(Stream stream)
            : this(stream, Ownership.None, OpticalFormat.None)
        {
        }

        /// <summary>
        /// Creates a new instance from a stream.
        /// </summary>
        /// <param name="stream">The stream to interpret</param>
        /// <param name="ownsStream">Indicates if the new instance should control the lifetime of the stream.</param>
        /// <param name="format">The disc image format</param>
        public DiscImageFile(Stream stream, Ownership ownsStream, OpticalFormat format)
        {
            if (ownsStream == Ownership.Dispose)
            {
                _toDispose = stream;
            }

            if (format == OpticalFormat.None)
            {
                if ((stream.Length % Mode1SectorSize) == 0 && (stream.Length % Mode2SectorSize) != 0)
                {
                    _format = OpticalFormat.Mode1;
                }
                else if ((stream.Length % Mode1SectorSize) != 0 && (stream.Length % Mode2SectorSize) == 0)
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

            _content = stream as SparseStream;
            if (_content == null)
            {
                _content = SparseStream.FromStream(stream, Ownership.None);
            }

            if (_format == OpticalFormat.Mode2)
            {
                Mode2Buffer converter = new Mode2Buffer(new StreamBuffer(_content, Ownership.None));
                _content = new BufferStream(converter, FileAccess.Read);
            }
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
                    }
                    _toDispose = null;
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Gets a value indicating if the layer only stores meaningful sectors.
        /// </summary>
        public override bool IsSparse
        {
            get { return false; }
        }

        internal override long Capacity
        {
            get { return _content.Length; }
        }

        internal override FileLocator RelativeFileLocator
        {
            get { return null; }
        }

        internal SparseStream Content
        {
            get { return _content; }
        }

        /// <summary>
        /// Gets the Geometry of the disc.
        /// </summary>
        /// <remarks>
        /// Optical discs don't fit the CHS model, so dummy CHS data provided, but
        /// sector size is accurate.
        /// </remarks>
        internal static Geometry Geometry
        {
            // Note external sector size is always 2048 - 2352 just has extra header
            // & error-correction info
            get { return new Geometry(1, 1, 1, Mode1SectorSize); }
        }

    }
}
