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
using System.Reflection;
using System.Runtime.InteropServices;
using DiscUtils.Compression;
using DiscUtils.Ntfs;
using Xunit;

namespace LibraryTests.Ntfs
{
    public class LZNT1Test
    {
        private byte[] _uncompressedData;

        public LZNT1Test()
        {
            Random rng = new Random(3425);
            _uncompressedData = new byte[64 * 1024];

            // Some test data that is reproducible, and fairly compressible
            for (int i = 0; i < 16 * 4096; ++i)
            {
                byte b = (byte)(rng.Next(26) + 'A');
                int start = rng.Next(_uncompressedData.Length);
                int len = rng.Next(20);

                for (int j = start; j < _uncompressedData.Length && j < start + len; j++)
                {
                    _uncompressedData[j] = b;
                }
            }

            // Make one block uncompressible
            for (int i = 5 * 4096; i < 6 * 4096; ++i)
            {
                _uncompressedData[i] = (byte)rng.Next(256);
            }
        }

        private static object CreateInstance<T>(string name)
        {
#if NETCOREAPP1_1
            return typeof(T).GetTypeInfo().Assembly.CreateInstance(name);
#else
            return typeof(T).Assembly.CreateInstance(name);
#endif
        }

        [Fact]
        public void Compress()
        {
            object instance = CreateInstance<NtfsFileSystem>("DiscUtils.Ntfs.LZNT1");
            BlockCompressor compressor = (BlockCompressor)instance;

            int compressedLength = 16 * 4096;
            byte[] compressedData = new byte[compressedLength];

            // Double-check, make sure native code round-trips
            byte[] nativeCompressed = NativeCompress(_uncompressedData, 0, _uncompressedData.Length, 4096);
            Assert.Equal(_uncompressedData, NativeDecompress(nativeCompressed, 0, nativeCompressed.Length));

            compressor.BlockSize = 4096;
            CompressionResult r = compressor.Compress(_uncompressedData, 0, _uncompressedData.Length, compressedData, 0, ref compressedLength);
            Assert.Equal(CompressionResult.Compressed, r);
            Assert.Equal(_uncompressedData, NativeDecompress(compressedData, 0, compressedLength));

            Assert.True(compressedLength < _uncompressedData.Length * 0.66);
        }

        [Fact]
        public void CompressMidSourceBuffer()
        {
            object instance = CreateInstance<NtfsFileSystem>("DiscUtils.Ntfs.LZNT1");
            BlockCompressor compressor = (BlockCompressor)instance;

            byte[] inData = new byte[128 * 1024];
            Array.Copy(_uncompressedData, 0, inData, 32 * 1024, 64 * 1024);

            int compressedLength = 16 * 4096;
            byte[] compressedData = new byte[compressedLength];

            // Double-check, make sure native code round-trips
            byte[] nativeCompressed = NativeCompress(inData, 32 * 1024, _uncompressedData.Length, 4096);
            Assert.Equal(_uncompressedData, NativeDecompress(nativeCompressed, 0, nativeCompressed.Length));

            compressor.BlockSize = 4096;
            CompressionResult r = compressor.Compress(inData, 32 * 1024, _uncompressedData.Length, compressedData, 0, ref compressedLength);
            Assert.Equal(CompressionResult.Compressed, r);
            Assert.Equal(_uncompressedData, NativeDecompress(compressedData, 0, compressedLength));
        }

        [Fact]
        public void CompressMidDestBuffer()
        {
            object instance = CreateInstance<NtfsFileSystem>("DiscUtils.Ntfs.LZNT1");
            BlockCompressor compressor = (BlockCompressor)instance;

            // Double-check, make sure native code round-trips
            byte[] nativeCompressed = NativeCompress(_uncompressedData, 0, _uncompressedData.Length, 4096);
            Assert.Equal(_uncompressedData, NativeDecompress(nativeCompressed, 0, nativeCompressed.Length));

            int compressedLength = 128 * 1024;
            byte[] compressedData = new byte[compressedLength];

            compressor.BlockSize = 4096;
            CompressionResult r = compressor.Compress(_uncompressedData, 0, _uncompressedData.Length, compressedData, 32 * 1024, ref compressedLength);
            Assert.Equal(CompressionResult.Compressed, r);
            Assert.True(compressedLength < _uncompressedData.Length);

            Assert.Equal(_uncompressedData, NativeDecompress(compressedData, 32 * 1024, compressedLength));
        }

        [Fact]
        public void Compress1KBlockSize()
        {
            object instance = CreateInstance<NtfsFileSystem>("DiscUtils.Ntfs.LZNT1");
            BlockCompressor compressor = (BlockCompressor)instance;

            int compressedLength = 16 * 4096;
            byte[] compressedData = new byte[compressedLength];

            // Double-check, make sure native code round-trips
            byte[] nativeCompressed = NativeCompress(_uncompressedData, 0, _uncompressedData.Length, 1024);
            Assert.Equal(_uncompressedData, NativeDecompress(nativeCompressed, 0, nativeCompressed.Length));

            compressor.BlockSize = 1024;
            CompressionResult r = compressor.Compress(_uncompressedData, 0, _uncompressedData.Length, compressedData, 0, ref compressedLength);
            Assert.Equal(CompressionResult.Compressed, r);

            byte[] duDecompressed = new byte[_uncompressedData.Length];
            int numDuDecompressed = compressor.Decompress(compressedData, 0, compressedLength, duDecompressed, 0);

            byte[] rightSizedDuDecompressed = new byte[numDuDecompressed];
            Array.Copy(duDecompressed, rightSizedDuDecompressed, numDuDecompressed);

            // Note: Due to bug in Windows LZNT1, we compare against native decompression, not the original data, since
            // Windows LZNT1 corrupts data on decompression when block size != 4096.
            Assert.Equal(rightSizedDuDecompressed, NativeDecompress(compressedData, 0, compressedLength));
        }

        [Fact]
        public void Compress1KBlock()
        {
            object instance = CreateInstance<NtfsFileSystem>("DiscUtils.Ntfs.LZNT1");
            BlockCompressor compressor = (BlockCompressor)instance;

            byte[] uncompressed1K = new byte[1024];
            Array.Copy(_uncompressedData, uncompressed1K, 1024);

            int compressedLength = 1024;
            byte[] compressedData = new byte[compressedLength];

            // Double-check, make sure native code round-trips
            byte[] nativeCompressed = NativeCompress(uncompressed1K, 0, 1024, 1024);
            Assert.Equal(uncompressed1K, NativeDecompress(nativeCompressed, 0, nativeCompressed.Length));

            compressor.BlockSize = 1024;
            CompressionResult r = compressor.Compress(uncompressed1K, 0, 1024, compressedData, 0, ref compressedLength);
            Assert.Equal(CompressionResult.Compressed, r);
            Assert.Equal(uncompressed1K, NativeDecompress(compressedData, 0, compressedLength));
        }

        [Fact]
        public void CompressAllZeros()
        {
            object instance = CreateInstance<NtfsFileSystem>("DiscUtils.Ntfs.LZNT1");
            BlockCompressor compressor = (BlockCompressor)instance;

            byte[] compressed = new byte[64 * 1024];
            int numCompressed = 64 * 1024;
            Assert.Equal(CompressionResult.AllZeros, compressor.Compress(new byte[64 * 1024], 0, 64 * 1024, compressed, 0, ref numCompressed));
        }

        [Fact]
        public void CompressIncompressible()
        {
            object instance = CreateInstance<NtfsFileSystem>("DiscUtils.Ntfs.LZNT1");
            BlockCompressor compressor = (BlockCompressor)instance;

            Random rng = new Random(6324);
            byte[] uncompressed = new byte[64 * 1024];
            rng.NextBytes(uncompressed);

            byte[] compressed = new byte[64 * 1024];
            int numCompressed = 64 * 1024;
            Assert.Equal(CompressionResult.Incompressible, compressor.Compress(uncompressed, 0, uncompressed.Length, compressed, 0, ref numCompressed));
        }

        [Fact]
        public void Decompress()
        {
            object instance = CreateInstance<NtfsFileSystem>("DiscUtils.Ntfs.LZNT1");
            BlockCompressor compressor = (BlockCompressor)instance;

            byte[] compressed = NativeCompress(_uncompressedData, 0, _uncompressedData.Length, 4096);

            // Double-check, make sure native code round-trips
            Assert.Equal(_uncompressedData, NativeDecompress(compressed, 0, compressed.Length));

            byte[] decompressed = new byte[_uncompressedData.Length];
            int numDecompressed = compressor.Decompress(compressed, 0, compressed.Length, decompressed, 0);
            Assert.Equal(numDecompressed, _uncompressedData.Length);

            Assert.Equal(_uncompressedData, decompressed);
        }

        [Fact]
        public void DecompressMidSourceBuffer()
        {
            object instance = CreateInstance<NtfsFileSystem>("DiscUtils.Ntfs.LZNT1");
            BlockCompressor compressor = (BlockCompressor)instance;

            byte[] compressed = NativeCompress(_uncompressedData, 0, _uncompressedData.Length, 4096);

            byte[] inData = new byte[128 * 1024];
            Array.Copy(compressed, 0, inData, 32 * 1024, compressed.Length);


            // Double-check, make sure native code round-trips
            Assert.Equal(_uncompressedData, NativeDecompress(inData, 32 * 1024, compressed.Length));

            byte[] decompressed = new byte[_uncompressedData.Length];
            int numDecompressed = compressor.Decompress(inData, 32 * 1024, compressed.Length, decompressed, 0);
            Assert.Equal(numDecompressed, _uncompressedData.Length);

            Assert.Equal(_uncompressedData, decompressed);
        }

        [Fact]
        public void DecompressMidDestBuffer()
        {
            object instance = CreateInstance<NtfsFileSystem>("DiscUtils.Ntfs.LZNT1");
            BlockCompressor compressor = (BlockCompressor)instance;

            byte[] compressed = NativeCompress(_uncompressedData, 0, _uncompressedData.Length, 4096);

            // Double-check, make sure native code round-trips
            Assert.Equal(_uncompressedData, NativeDecompress(compressed, 0, compressed.Length));

            byte[] outData = new byte[128 * 1024];
            int numDecompressed = compressor.Decompress(compressed, 0, compressed.Length, outData, 32 * 1024);
            Assert.Equal(numDecompressed, _uncompressedData.Length);

            byte[] decompressed = new byte[_uncompressedData.Length];
            Array.Copy(outData, 32 * 1024, decompressed, 0, _uncompressedData.Length);
            Assert.Equal(_uncompressedData, decompressed);
        }

        [Fact]
        public void Decompress1KBlockSize()
        {
            object instance = CreateInstance<NtfsFileSystem>("DiscUtils.Ntfs.LZNT1");
            BlockCompressor compressor = (BlockCompressor)instance;

            byte[] compressed = NativeCompress(_uncompressedData, 0, _uncompressedData.Length, 1024);

            Assert.Equal(_uncompressedData, NativeDecompress(compressed, 0, compressed.Length));

            byte[] decompressed = new byte[_uncompressedData.Length];
            int numDecompressed = compressor.Decompress(compressed, 0, compressed.Length, decompressed, 0);
            Assert.Equal(numDecompressed, _uncompressedData.Length);

            Assert.Equal(_uncompressedData, decompressed);
        }



        private static byte[] NativeCompress(byte[] data, int offset, int length, int chunkSize)
        {
            IntPtr compressedBuffer = IntPtr.Zero;
            IntPtr uncompressedBuffer = IntPtr.Zero;
            IntPtr workspaceBuffer = IntPtr.Zero;
            try
            {
                uncompressedBuffer = Marshal.AllocHGlobal(length);
                Marshal.Copy(data, offset, uncompressedBuffer, length);

                compressedBuffer = Marshal.AllocHGlobal(length);

                uint bufferWorkspaceSize;
                uint fragmentWorkspaceSize;
                RtlGetCompressionWorkSpaceSize(2, out bufferWorkspaceSize, out fragmentWorkspaceSize);

                workspaceBuffer = Marshal.AllocHGlobal((int)bufferWorkspaceSize);

                uint compressedSize;
                int ntStatus = RtlCompressBuffer(2, uncompressedBuffer, (uint)length, compressedBuffer, (uint)length, (uint)chunkSize, out compressedSize, workspaceBuffer);
                Assert.Equal(0, ntStatus);

                byte[] result = new byte[compressedSize];

                Marshal.Copy(compressedBuffer, result, 0, (int)compressedSize);

                return result;
            }
            finally
            {
                if (compressedBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(compressedBuffer);
                }

                if (uncompressedBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(uncompressedBuffer);
                }

                if (workspaceBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(workspaceBuffer);
                }
            }
        }

        private static byte[] NativeDecompress(byte[] data, int offset, int length)
        {
            IntPtr compressedBuffer = IntPtr.Zero;
            IntPtr uncompressedBuffer = IntPtr.Zero;
            try
            {
                compressedBuffer = Marshal.AllocHGlobal(length);
                Marshal.Copy(data, offset, compressedBuffer, length);

                uncompressedBuffer = Marshal.AllocHGlobal(64 * 1024);

                uint uncompressedSize;
                int ntStatus = RtlDecompressBuffer(2, uncompressedBuffer, 64 * 1024, compressedBuffer, (uint)length, out uncompressedSize);
                Assert.Equal(0, ntStatus);

                byte[] result = new byte[uncompressedSize];

                Marshal.Copy(uncompressedBuffer, result, 0, (int)uncompressedSize);

                return result;
            }
            finally
            {
                if (compressedBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(compressedBuffer);
                }

                if (uncompressedBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(uncompressedBuffer);
                }
            }
        }

        [DllImport("ntdll")]
        private extern static int RtlGetCompressionWorkSpaceSize(ushort formatAndEngine, out uint bufferWorkspaceSize, out uint fragmentWorkspaceSize);

        [DllImport("ntdll")]
        private extern static int RtlCompressBuffer(ushort formatAndEngine, IntPtr uncompressedBuffer, uint uncompressedBufferSize, IntPtr compressedBuffer, uint compressedBufferSize, uint uncompressedChunkSize, out uint finalCompressedSize, IntPtr workspace);

        [DllImport("ntdll")]
        private extern static int RtlDecompressBuffer(ushort formatAndEngine, IntPtr uncompressedBuffer, uint uncompressedBufferSize, IntPtr compressedBuffer, uint compressedBufferSize, out uint finalUncompressedSize);
    }
}
