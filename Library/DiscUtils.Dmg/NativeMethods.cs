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
using System.Runtime.InteropServices;

namespace DiscUtils.Dmg
{
    internal static class NativeMethods
    {
        /// <summary>
        /// Decompress a buffer using LZFSE.
        /// </summary>
        /// <param name="decompressedBuffer">
        /// Pointer to the first byte of the destination buffer.
        /// </param>
        /// <param name="decompressedSize">
        /// Size of the destination buffer in bytes.
        /// </param>
        /// <param name="compressedBuffer">
        /// Pointer to the first byte of the source buffer.
        /// </param>
        /// <param name="compressedSize">
        /// Size of the source buffer in bytes.
        /// </param>
        /// <param name="scratchBuffer">
        /// If non-<see langword="null"/>, a pointer to scratch space for the routine to use as workspace;
        /// the routine may use up to <see cref="lzfse_decode_scratch_size"/> bytes of workspace
        /// during its operation, and will not perform any internal allocations. If
        /// <see langword="null"/>, the routine may allocate its own memory to use during operation via
        /// a single call to <c>malloc()</c>, and will release it by calling <c>free()</c> prior
        /// to returning. For most use, passing <see langword="null"/> is perfectly satisfactory, but if
        /// you require strict control over allocation, you will want to pass an
        /// explicit scratch buffer.
        /// </param>
        /// <returns></returns>
        [DllImport("lzfse")]
        public unsafe static extern int lzfse_decode_buffer(byte* decompressedBuffer, int decompressedSize, byte* compressedBuffer, int compressedSize, byte* scratchBuffer);
    }
}
