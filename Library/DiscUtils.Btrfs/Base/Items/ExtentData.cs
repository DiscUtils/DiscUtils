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
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using DiscUtils.Compression;
using DiscUtils.Streams;
using lzo.net;

namespace DiscUtils.Btrfs.Base.Items
{
    /// <summary>
    /// The contents of a file
    /// </summary>
    internal class ExtentData : BaseItem
    {
        public ExtentData(Key key) : base(key) { }

        /// <summary>
        ///  generation 
        /// </summary>
        public ulong Generation { get; private set; }

        /// <summary>
        ///  (n) size of decoded extent 
        /// </summary>
        public ulong DecodedSize { get; private set; }

        /// <summary>
        ///  compression (0=none, 1=zlib, 2=LZO) 
        /// </summary>
        public ExtentDataCompression Compression { get; private set; }

        /// <summary>
        ///  encryption (0=none) 
        /// </summary>
        public bool Encryption { get; private set; }

        /// <summary>
        ///  type (0=inline, 1=regular, 2=prealloc) 
        /// </summary>
        public ExtentDataType Type { get; private set; }

        /// <summary>
        /// If the extent is inline, the bytes are the data bytes (n bytes in case no compression/encryption/other encoding is used)
        /// </summary>
        public byte[] InlineData { get; private set; }

        /// <summary>
        ///  (ea) logical address of extent. If this is zero, the extent is sparse and consists of all zeroes. 
        /// </summary>
        public ulong ExtentAddress { get; private set; }

        /// <summary>
        ///  (es) size of extent 
        /// </summary>
        public ulong ExtentSize { get; private set; }

        /// <summary>
        ///  (o) offset within the extent 
        /// </summary>
        public ulong ExtentOffset { get; private set; }

        /// <summary>
        ///  (s) logical number of bytes in file 
        /// </summary>
        public ulong LogicalSize { get; private set; }

        public override int Size
        {
            get { return Type == ExtentDataType.Inline ? InlineData.Length + 0x15 : 0x35; }
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            Generation = EndianUtilities.ToUInt64LittleEndian(buffer, offset);
            DecodedSize = EndianUtilities.ToUInt64LittleEndian(buffer, offset+0x8);
            Compression = (ExtentDataCompression)buffer[offset + 0x10];
            Encryption = buffer[offset + 0x11] != 0;
            //12 	2 	UINT 	other encoding (0=none)
            Type = (ExtentDataType)buffer[offset + 0x14];

            if (Type == ExtentDataType.Inline)
            {
                InlineData = EndianUtilities.ToByteArray(buffer, offset + 0x15, buffer.Length - (offset + 0x15));
            }
            else
            {
                ExtentAddress = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x15);
                ExtentSize = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x1d);
                ExtentOffset = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x25);
                LogicalSize = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x2d);
            }

            return Size;
        }

        public Stream GetStream(Context context)
        {
            if (Encryption)
                throw new IOException("Extent encryption is not supported");
            Stream stream;
            switch (Type)
            {
                case ExtentDataType.Inline:
                    byte[] data = InlineData;
                    stream = new MemoryStream(data);
                    break;
                case ExtentDataType.Regular:
                    var address = ExtentAddress;
                    if (address == 0)
                    {
                        stream = new ZeroStream((long)LogicalSize);
                    }
                    else
                    {
                        var physicalAddress = context.MapToPhysical(address);
                        stream = new SubStream(context.RawStream, Ownership.None, (long)(physicalAddress + ExtentOffset), (long)ExtentSize);
                    }
                    break;
                case ExtentDataType.PreAlloc:
                    throw new NotImplementedException();
                default:
                    throw new IOException("invalid extent type");
            }
            switch (Compression)
            {
                case ExtentDataCompression.None:
                    break;
                case ExtentDataCompression.Zlib:
                {
                    var zlib = new ZlibStream(stream, CompressionMode.Decompress, false);
                    var sparse = SparseStream.FromStream(zlib, Ownership.Dispose);
                    var length = new LengthWrappingStream(sparse, (long)LogicalSize, Ownership.Dispose);
                    stream = new PositionWrappingStream(length, 0, Ownership.Dispose);
                    break;
                }
                case ExtentDataCompression.Lzo:
                {
                    var buffer = StreamUtilities.ReadExact(stream, sizeof(uint));
                    var totalLength = EndianUtilities.ToUInt32LittleEndian(buffer, 0);
                    long processed = sizeof(uint);
                    var parts = new List<SparseStream>();
                    var remaining = (long)LogicalSize;
                    while (processed < totalLength)
                    {
                        stream.Position = processed;
                        StreamUtilities.ReadExact(stream, buffer, 0, sizeof(uint));
                        var partLength = EndianUtilities.ToUInt32LittleEndian(buffer, 0);
                        processed += sizeof(uint);
                        var part = new SubStream(stream, Ownership.Dispose, processed, partLength);
                        var uncompressed = new SeekableLzoStream(part, CompressionMode.Decompress, false);
                        uncompressed.SetLength(Math.Min(Sizes.OneKiB*4, remaining));
                        remaining -= uncompressed.Length;
                        parts.Add(SparseStream.FromStream(uncompressed, Ownership.Dispose));
                        processed +=  partLength;
                    }
                    stream = new ConcatStream(Ownership.Dispose, parts.ToArray());
                    break;
                }
                default:
                    throw new IOException($"Unsupported extent compression ({Compression})");
            }
            return stream;
        }
    }
}
