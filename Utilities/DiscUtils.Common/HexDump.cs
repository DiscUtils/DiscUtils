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

namespace DiscUtils.Common
{
    /// <summary>
    /// Provides utility methods to produce hex dumps of binary data.
    /// </summary>
    public static class HexDump
    {
        /// <summary>
        /// Creates a hex dump from a byte array.
        /// </summary>
        /// <param name="data">The buffer to generate the hex dump from.</param>
        /// <param name="output">The destination for the hex dump.</param>
        public static void Generate(byte[] data, TextWriter output)
        {
            Generate(SparseStream.FromStream(new MemoryStream(data, false), Ownership.None), output);
        }

        /// <summary>
        /// Creates a hex dump from a byte array.
        /// </summary>
        /// <param name="data">The buffer to generate the hex dump from.</param>
        /// <param name="offset">Offset of the first byte to hex dump.</param>
        /// <param name="count">The number of bytes to hex dump</param>
        /// <param name="output">The destination for the hex dump.</param>
        public static void Generate(byte[] data, int offset, int count, TextWriter output)
        {
            byte[] tempBuffer = new byte[count];
            Array.Copy(data, offset, tempBuffer, 0, count);
            Generate(SparseStream.FromStream(new MemoryStream(tempBuffer, false), Ownership.None), output);
        }

        /// <summary>
        /// Creates a hex dump from a stream.
        /// </summary>
        /// <param name="stream">The stream to generate the hex dump from.</param>
        /// <param name="output">The destination for the hex dump.</param>
        public static void Generate(Stream stream, TextWriter output)
        {
            Generate(SparseStream.FromStream(stream, Ownership.None), output);
        }

        /// <summary>
        /// Creates a hex dump from a stream.
        /// </summary>
        /// <param name="stream">The stream to generate the hex dump from.</param>
        /// <param name="output">The destination for the hex dump.</param>
        public static void Generate(SparseStream stream, TextWriter output)
        {
            stream.Position = 0;
            byte[] buffer = new byte[1024 * 1024];

            foreach(var block in StreamExtent.Blocks(stream.Extents, buffer.Length))
            {
                long startPos = block.Offset * (long)buffer.Length;
                long endPos = Math.Min((block.Offset + block.Count) * (long)buffer.Length, stream.Length);
                stream.Position = startPos;

                while (stream.Position < endPos)
                {
                    int numLoaded = 0;
                    long readStart = stream.Position;
                    while (numLoaded < buffer.Length)
                    {
                        int bytesRead = stream.Read(buffer, numLoaded, buffer.Length - numLoaded);
                        if (bytesRead == 0)
                        {
                            break;
                        }
                        numLoaded += bytesRead;
                    }

                    for (int i = 0; i < numLoaded; i += 16)
                    {
                        bool foundVal = false;
                        if (i > 0)
                        {
                            for (int j = 0; j < 16; j++)
                            {
                                if (buffer[i + j] != buffer[i + j - 16])
                                {
                                    foundVal = true;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            foundVal = true;
                        }

                        if (foundVal)
                        {
                            output.Write("{0:x8}", i + readStart);

                            for (int j = 0; j < 16; j++)
                            {
                                if (j % 8 == 0)
                                {
                                    output.Write(" ");
                                }
                                output.Write(" {0:x2}", buffer[i + j]);
                            }

                            output.Write("  |");
                            for (int j = 0; j < 16; j++)
                            {
                                if (j % 8 == 0 && j != 0)
                                {
                                    output.Write(" ");
                                }
                                output.Write("{0}", (buffer[i + j] >= 32 && buffer[i + j] < 127) ? (char)buffer[i + j] : '.');
                            }
                            output.Write("|");

                            output.WriteLine();
                        }
                    }
                }
            }
        }
    }
}
