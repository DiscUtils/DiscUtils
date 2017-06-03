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
using System.Globalization;
using System.IO;
using DiscUtils.Archives;
using DiscUtils.Internal;
using DiscUtils.Streams;

namespace DiscUtils.Xva
{
    internal class DiskStream : SparseStream
    {
        private readonly TarFile _archive;
        private readonly string _dir;
        private readonly long _length;
        private Stream _currentChunkData;

        private int _currentChunkIndex;

        private long _position;
        private int[] _skipChunks;

        public DiskStream(TarFile archive, long length, string dir)
        {
            _archive = archive;
            _length = length;
            _dir = dir;

            if (!archive.DirExists(_dir))
            {
                throw new IOException("No such disk");
            }

            ReadChunkSkipList();
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

        public override IEnumerable<StreamExtent> Extents
        {
            get
            {
                List<StreamExtent> extents = new List<StreamExtent>();

                long chunkSize = Sizes.OneMiB;
                int i = 0;
                int numChunks = (int)((_length + chunkSize - 1) / chunkSize);
                while (i < numChunks)
                {
                    // Find next stored block
                    while (i < numChunks && !ChunkExists(i))
                    {
                        ++i;
                    }

                    int start = i;

                    // Find next absent block
                    while (i < numChunks && ChunkExists(i))
                    {
                        ++i;
                    }

                    if (start != i)
                    {
                        extents.Add(new StreamExtent(start * chunkSize, (i - start) * chunkSize));
                    }
                }

                return extents;
            }
        }

        public override long Length
        {
            get { return _length; }
        }

        public override long Position
        {
            get { return _position; }

            set
            {
                if (value > _length)
                {
                    throw new IOException("Attempt to move beyond end of stream");
                }

                _position = value;
            }
        }

        public override void Flush() {}

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position == _length)
            {
                return 0;
            }

            if (_position > _length)
            {
                throw new IOException("Attempt to read beyond end of stream");
            }

            int chunk = CorrectChunkIndex((int)(_position / Sizes.OneMiB));

            if (_currentChunkIndex != chunk || _currentChunkData == null)
            {
                if (_currentChunkData != null)
                {
                    _currentChunkData.Dispose();
                    _currentChunkData = null;
                }

                if (!_archive.TryOpenFile(string.Format(CultureInfo.InvariantCulture, @"{0}/{1:D8}", _dir, chunk), out _currentChunkData))
                {
                    _currentChunkData = new ZeroStream(Sizes.OneMiB);
                }

                _currentChunkIndex = chunk;
            }

            long chunkOffset = _position % Sizes.OneMiB;
            int toRead = Math.Min((int)Math.Min(Sizes.OneMiB - chunkOffset, _length - _position), count);

            _currentChunkData.Position = chunkOffset;

            int numRead = _currentChunkData.Read(buffer, offset, toRead);
            _position += numRead;
            return numRead;
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
                effectiveOffset += _length;
            }

            if (effectiveOffset < 0)
            {
                throw new IOException("Attempt to move before beginning of disk");
            }
            Position = effectiveOffset;
            return Position;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        private bool ChunkExists(int i)
        {
            return _archive.FileExists(string.Format(CultureInfo.InvariantCulture, @"{0}/{1:D8}", _dir, CorrectChunkIndex(i)));
        }

        private void ReadChunkSkipList()
        {
            List<int> skipChunks = new List<int>();
            foreach (FileRecord fileInfo in _archive.GetFiles(_dir))
            {
                if (fileInfo.Length == 0)
                {
                    string path = fileInfo.Name.Replace('/', '\\');
                    int index;
                    if (int.TryParse(Utilities.GetFileFromPath(path), NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
                    {
                        skipChunks.Add(index);
                    }
                }
            }

            skipChunks.Sort();
            _skipChunks = skipChunks.ToArray();
        }

        private int CorrectChunkIndex(int rawIndex)
        {
            int index = rawIndex;
            for (int i = 0; i < _skipChunks.Length; ++i)
            {
                if (index >= _skipChunks[i])
                {
                    ++index;
                }
                else if (index < +_skipChunks[i])
                {
                    break;
                }
            }

            return index;
        }
    }
}