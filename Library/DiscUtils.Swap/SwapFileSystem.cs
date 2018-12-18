//
// Copyright (c) 2017, Bianco Veigel
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
using DiscUtils.Vfs;

namespace DiscUtils.Swap
{
    /// <summary>
    /// Class for accessing Swap file systems.
    /// </summary>
    public sealed class SwapFileSystem : VfsReadOnlyFileSystem<VfsDirEntry, IVfsFile, IVfsDirectory<VfsDirEntry, IVfsFile>, SwapContext>
    {
        /// <summary>
        /// Initializes a new instance of the SwapFileSystem class.
        /// </summary>
        /// <param name="stream">The stream containing the file system.</param>
        public SwapFileSystem(Stream stream):base(new DiscFileSystemOptions())
        {
            Context = new SwapContext();
            Context.Header = ReadSwapHeader(stream);
            if (Context.Header == null) throw new IOException("Swap Header missing");
            if (Context.Header.Magic != SwapHeader.Magic1 && Context.Header.Magic != SwapHeader.Magic2)
                throw new IOException("Invalid Swap header");
        }
        
        /// <summary>
        /// Gets the friendly name for the file system.
        /// </summary>
        public override string FriendlyName
        {
            get
            {
                return "Swap";
            }
        }

        /// <summary>
        /// Gets the volume label.
        /// </summary>
        public override string VolumeLabel
        {
            get { return Context.Header.Volume; }
        }
        
        /// <summary>
        /// Detects if a stream contains a Swap file system.
        /// </summary>
        /// <param name="stream">The stream to inspect.</param>
        /// <returns><c>true</c> if the stream appears to be a Swap file system, else <c>false</c>.</returns>
        public static bool Detect(Stream stream)
        {
            SwapHeader header = ReadSwapHeader(stream);
            return header != null && (header.Magic == SwapHeader.Magic1 || header.Magic == SwapHeader.Magic2);
        }

        private static SwapHeader ReadSwapHeader(Stream stream)
        {
            if (stream.Length < SwapHeader.PageSize)
            {
                return null;
            }
            stream.Position = 0;
            byte[] headerData = StreamUtilities.ReadExact(stream, SwapHeader.PageSize);
            SwapHeader header = new SwapHeader();
            header.ReadFrom(headerData, 0);
            return header;
        }

        /// <summary>
        /// Size of the Filesystem in bytes
        /// </summary>
        public override long Size
        {
            get { return Context.Header.LastPage * SwapHeader.PageSize; }
        }

        /// <summary>
        /// Used space of the Filesystem in bytes
        /// </summary>
        public override long UsedSpace {
            get { return Size; }
        }

        /// <summary>
        /// Available space of the Filesystem in bytes
        /// </summary>
        public override long AvailableSpace { get { return 0; } }

        protected override IVfsFile ConvertDirEntryToFile(VfsDirEntry dirEntry)
        {
            throw new NotImplementedException();
        }
    }
}
