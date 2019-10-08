//
// Copyright (c) 2019, Quamotion bvba
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

namespace DiscUtils.Dmg
{
    public class Lzfse
    {
        /// <summary>
        /// Decompresses a LZFSE compressed buffer
        /// </summary>
        /// <param name="buffer">
        /// The buffer to decompress
        /// </param>
        /// <param name="uncompressedSize">
        /// The buffer into which to decompress the data.
        /// </param>
        public static unsafe int Decompress(byte[] buffer, byte[] decompressedBuffer)
        {
            int actualDecompressedSize = 0;

            fixed (byte* decompressedBufferPtr = decompressedBuffer)
            fixed (byte* bufferPtr = buffer)
            {
                actualDecompressedSize = NativeMethods.lzfse_decode_buffer(decompressedBufferPtr, decompressedBuffer.Length, bufferPtr, buffer.Length, null);
            }

            if (actualDecompressedSize == 0)
            {
                throw new Exception("There was an error decompressing the specified buffer.");
            }

            return actualDecompressedSize;
        }
    }
}
