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

using System.Collections.Generic;
using System.IO;
using DiscUtils.Internal;
using DiscUtils.Streams;

namespace DiscUtils.SquashFs
{
    internal sealed class BuilderFile : BuilderNode
    {
        private RegularInode _inode;
        private List<uint> _lengths;
        private Stream _source;
        private readonly string _sourcePath;

        public BuilderFile(Stream source)
        {
            _source = source;
            NumLinks = 1;
        }

        public BuilderFile(string source)
        {
            _sourcePath = source;
            NumLinks = 1;
        }

        public override Inode Inode
        {
            get { return _inode; }
        }

        public override void Reset()
        {
            _inode = new RegularInode();
            _lengths = null;
        }

        public override void Write(BuilderContext context)
        {
            if (!_written)
            {
                WriteFileData(context);

                WriteInode(context);

                _written = true;
            }
        }

        private void WriteFileData(BuilderContext context)
        {
            Stream outStream = context.RawStream;

            bool disposeSource = false;
            try
            {
                if (_source == null)
                {
                    var locator = new LocalFileLocator(string.Empty);
                    _source = locator.Open(_sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    disposeSource = true;
                }

                if (_source.Position != 0)
                {
                    _source.Position = 0;
                }

                long startPos = outStream.Position;
                int bufferedBytes = StreamUtilities.ReadMaximum(_source, context.IoBuffer, 0, context.DataBlockSize);

                if (bufferedBytes < context.DataBlockSize)
                {
                    // Fragment - less than one complete block of data
                    _inode.StartBlock = 0xFFFFFFFF;

                    _inode.FragmentKey = context.WriteFragment(bufferedBytes, out _inode.FragmentOffset);
                    _inode.FileSize = (uint)bufferedBytes;
                }
                else
                {
                    // At least one full block, no fragments used
                    _inode.FragmentKey = 0xFFFFFFFF;

                    _lengths = new List<uint>();
                    _inode.StartBlock = (uint)startPos;
                    _inode.FileSize = bufferedBytes;
                    while (bufferedBytes > 0)
                    {
                        _lengths.Add(context.WriteDataBlock(context.IoBuffer, 0, bufferedBytes));
                        bufferedBytes = StreamUtilities.ReadMaximum(_source, context.IoBuffer, 0, context.DataBlockSize);
                        _inode.FileSize += (uint)bufferedBytes;
                    }
                }
            }
            finally
            {
                if (disposeSource)
                {
                    _source.Dispose();
                }
            }
        }

        private void WriteInode(BuilderContext context)
        {
            if (NumLinks != 1)
            {
                throw new IOException("Extended file records (with multiple hard links) not supported");
            }

            FillCommonInodeData(context);
            _inode.Type = InodeType.File;

            InodeRef = context.InodeWriter.Position;

            int totalSize = _inode.Size;
            _inode.WriteTo(context.IoBuffer, 0);
            if (_lengths != null && _lengths.Count > 0)
            {
                for (int i = 0; i < _lengths.Count; ++i)
                {
                    EndianUtilities.WriteBytesLittleEndian(_lengths[i], context.IoBuffer, _inode.Size + i * 4);
                }

                totalSize += _lengths.Count * 4;
            }

            context.InodeWriter.Write(context.IoBuffer, 0, totalSize);
        }
    }
}