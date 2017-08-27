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
using DiscUtils.Streams;

namespace DiscUtils.Wim
{
    /// <summary>
    /// Provides access to a (compressed) resource within the WIM file.
    /// </summary>
    /// <remarks>Stream access must be strictly sequential.</remarks>
    internal class FileResourceStream : SparseStream
    {
        private const int E8DecodeFileSize = 12000000;

        private readonly Stream _baseStream;
        private readonly long[] _chunkLength;

        private readonly long[] _chunkOffsets;
        private readonly int _chunkSize;

        private int _currentChunk;
        private Stream _currentChunkStream;
        private readonly ShortResourceHeader _header;
        private readonly bool _lzxCompression;
        private readonly long _offsetDelta;

        private long _position;

        public FileResourceStream(Stream baseStream, ShortResourceHeader header, bool lzxCompression, int chunkSize)
        {
            _baseStream = baseStream;
            _header = header;
            _lzxCompression = lzxCompression;
            _chunkSize = chunkSize;

            if (baseStream.Length > uint.MaxValue)
            {
                throw new NotImplementedException("Large files >4GB");
            }

            int numChunks = (int)MathUtilities.Ceil(header.OriginalSize, _chunkSize);

            _chunkOffsets = new long[numChunks];
            _chunkLength = new long[numChunks];
            for (int i = 1; i < numChunks; ++i)
            {
                _chunkOffsets[i] = EndianUtilities.ToUInt32LittleEndian(StreamUtilities.ReadExact(_baseStream, 4), 0);
                _chunkLength[i - 1] = _chunkOffsets[i] - _chunkOffsets[i - 1];
            }

            _chunkLength[numChunks - 1] = _baseStream.Length - _baseStream.Position - _chunkOffsets[numChunks - 1];
            _offsetDelta = _baseStream.Position;

            _currentChunk = -1;
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override IEnumerable<StreamExtent> Extents
        {
            get { return new[] { new StreamExtent(0, Length) }; }
        }

        public override long Length
        {
            get { return _header.OriginalSize; }
        }

        public override long Position
        {
            get { return _position; }

            set { _position = value; }
        }

        public override void Flush() {}

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= Length)
            {
                return 0;
            }

            int maxToRead = (int)Math.Min(Length - _position, count);

            int totalRead = 0;
            while (totalRead < maxToRead)
            {
                int chunk = (int)(_position / _chunkSize);
                int chunkOffset = (int)(_position % _chunkSize);
                int numToRead = Math.Min(maxToRead - totalRead, _chunkSize - chunkOffset);

                if (_currentChunk != chunk)
                {
                    _currentChunkStream = OpenChunkStream(chunk);
                    _currentChunk = chunk;
                }

                _currentChunkStream.Position = chunkOffset;
                int numRead = _currentChunkStream.Read(buffer, offset + totalRead, numToRead);
                if (numRead == 0)
                {
                    return totalRead;
                }

                _position += numRead;
                totalRead += numRead;
            }

            return totalRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        private Stream OpenChunkStream(int chunk)
        {
            int targetUncompressed = _chunkSize;
            if (chunk == _chunkLength.Length - 1)
            {
                targetUncompressed = (int)(Length - _position);
            }

            Stream rawChunkStream = new SubStream(_baseStream, _offsetDelta + _chunkOffsets[chunk], _chunkLength[chunk]);
            if ((_header.Flags & ResourceFlags.Compressed) != 0 && _chunkLength[chunk] != targetUncompressed)
            {
                if (_lzxCompression)
                {
                    return new LzxStream(rawChunkStream, 15, E8DecodeFileSize);
                }
                return new XpressStream(rawChunkStream, targetUncompressed);
            }
            return rawChunkStream;
        }
    }
}