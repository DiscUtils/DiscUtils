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
using System.Runtime.InteropServices;
using DiscUtils.Streams;
using Microsoft.Win32.SafeHandles;

namespace DiskClone
{
    /// <summary>
    /// A stream implementation that honours the alignment rules for unbuffered streams.
    /// </summary>
    /// <remarks>
    /// To support the stream interface, which permits unaligned access, all accesses
    /// are routed through an appropriately aligned buffer.
    /// </remarks>
    public class UnbufferedNativeStream : SparseStream
    {
        private const int BufferSize = 64 * 1024;
        private const int Alignment = 512;

        private long _position;
        private SafeFileHandle _handle;
        private IntPtr _bufferAllocHandle;
        private IntPtr _buffer;

        public UnbufferedNativeStream(SafeFileHandle handle)
        {
            _handle = handle;

            _bufferAllocHandle = Marshal.AllocHGlobal(BufferSize + Alignment);
            _buffer = new IntPtr(((_bufferAllocHandle.ToInt64() + Alignment - 1) / Alignment) * Alignment);

            _position = 0;
        }

        protected override void Dispose(bool disposing)
        {
            if (_bufferAllocHandle != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_bufferAllocHandle);
                _bufferAllocHandle = IntPtr.Zero;
                _bufferAllocHandle = IntPtr.Zero;
            }

            if (!_handle.IsClosed)
            {
                _handle.Close();
            }

            base.Dispose(disposing);
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void Flush()
        {
        }

        public override long Length
        {
            get
            {
                long result;
                if (NativeMethods.GetFileSizeEx(_handle, out result))
                {
                    return result;
                }
                else
                {
                    throw Win32Wrapper.GetIOExceptionForLastError();
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
            int totalBytesRead = 0;
            long length = Length;

            while (totalBytesRead < count)
            {
                long alignedStart = (_position / Alignment) * Alignment;
                int alignmentOffset = (int)(_position - alignedStart);

                long newPos;
                if (!NativeMethods.SetFilePointerEx(_handle, alignedStart, out newPos, 0))
                {
                    throw Win32Wrapper.GetIOExceptionForLastError();
                }

                int toRead = (int)Math.Min(length - alignedStart, BufferSize);
                int numRead;
                if (!NativeMethods.ReadFile(_handle, _buffer, toRead, out numRead, IntPtr.Zero))
                {
                    throw Win32Wrapper.GetIOExceptionForLastError();
                }

                int usefulData = numRead - alignmentOffset;
                if (usefulData <= 0)
                {
                    return totalBytesRead;
                }

                int toCopy = Math.Min(count - totalBytesRead, usefulData);

                Marshal.Copy(_buffer + alignmentOffset, buffer, offset + totalBytesRead, toCopy);

                totalBytesRead += toCopy;
                _position += toCopy;
            }

            return totalBytesRead;
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
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override IEnumerable<StreamExtent> Extents
        {
            get
            {
                return new StreamExtent[] { new StreamExtent(0, Length) };
            }
        }
    }
}
