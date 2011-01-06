//
// Copyright (c) 2008-2010, Kenneth Bell
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

namespace DiscUtils.SquashFs
{
    using System.IO;
    using System.IO.Compression;
    using DiscUtils.Compression;
    using DiscUtils.Vfs;

    internal class VfsSquashFileSystemReader : VfsReadOnlyFileSystem<DirectoryEntry, File, Directory, Context>
    {
        public const int MetadataBufferSize = 8 * 1024;

        private Context _context;
        private byte[] _ioBuffer;
        private BlockCache<Block> _blockCache;
        private BlockCache<Metablock> _metablockCache;

        public VfsSquashFileSystemReader(Stream stream)
            : base(new DiscFileSystemOptions())
        {
            _context = new Context();
            _context.SuperBlock = new SuperBlock();
            _context.RawStream = stream;

            stream.Position = 0;
            byte[] buffer = Utilities.ReadFully(stream, _context.SuperBlock.Size);
            _context.SuperBlock.ReadFrom(buffer, 0);

            _blockCache = new BlockCache<Block>((int)_context.SuperBlock.BlockSize, 20);
            _metablockCache = new BlockCache<Metablock>(MetadataBufferSize, 20);

            _context.ReadBlock = ReadBlock;
            _context.ReadMetaBlock = ReadMetaBlock;
            _context.InodeReader = new MetablockReader(_context, _context.SuperBlock.InodeTableStart);
            _context.DirectoryReader = new MetablockReader(_context, _context.SuperBlock.DirectoryTableStart);

            _context.RawStream.Position = _context.SuperBlock.FragmentTableStart;
            int numFragBlocks = (int)Utilities.Ceil(_context.SuperBlock.FragmentsCount * FragmentRecord.RecordSize, MetadataBufferSize);

            byte[] fragTableBytes = Utilities.ReadFully(_context.RawStream, numFragBlocks * 8);
            _context.FragmentTableReaders = new MetablockReader[numFragBlocks];
            for (int i = 0; i < numFragBlocks; ++i)
            {
                long block = Utilities.ToInt64LittleEndian(fragTableBytes, i * 8);
                _context.FragmentTableReaders[i] = new MetablockReader(_context, block);
            }

            _context.InodeReader.SetPosition(_context.SuperBlock.RootInode);
            DirectoryInode dirInode = (DirectoryInode)Inode.Read(_context.InodeReader);

            RootDirectory = new Directory(_context, dirInode, _context.SuperBlock.RootInode);
        }

        public override string VolumeLabel
        {
            get { return string.Empty; }
        }

        public override string FriendlyName
        {
            get { return "SquashFs"; }
        }

        protected override File ConvertDirEntryToFile(DirectoryEntry dirEntry)
        {
            MetadataRef inodeRef = dirEntry.InodeReference;
            _context.InodeReader.SetPosition(inodeRef);
            Inode inode = Inode.Read(_context.InodeReader);

            if (dirEntry.IsSymlink)
            {
                return new Symlink(_context, inode, inodeRef);
            }
            else if (dirEntry.IsDirectory)
            {
                return new Directory(_context, inode, inodeRef);
            }
            else
            {
                return new File(_context, inode, inodeRef);
            }
        }

        private Block ReadBlock(long pos, int diskLen)
        {
            Block block = _blockCache.GetBlock(pos);
            if (block.Available >= 0)
            {
                return block;
            }

            Stream stream = _context.RawStream;
            stream.Position = pos;

            int readLen = diskLen & 0x007FFFFF;
            bool isCompressed = (diskLen & 0x00800000) == 0;

            if (isCompressed)
            {
                if (_ioBuffer == null || readLen > _ioBuffer.Length)
                {
                    _ioBuffer = new byte[readLen];
                }

                if (Utilities.ReadFully(stream, _ioBuffer, 0, readLen) != readLen)
                {
                    throw new IOException("Truncated stream reading compressed block");
                }

                using (ZlibStream zlibStream = new ZlibStream(new MemoryStream(_ioBuffer, 0, readLen, false), CompressionMode.Decompress, true))
                {
                    block.Available = Utilities.ReadFully(zlibStream, block.Data, 0, (int)_context.SuperBlock.BlockSize);
                }
            }
            else
            {
                block.Available = Utilities.ReadFully(stream, block.Data, 0, readLen);
                if (block.Available != readLen)
                {
                    throw new IOException("Truncated stream reading uncompressed block");
                }
            }

            return block;
        }

        private Metablock ReadMetaBlock(long pos)
        {
            Metablock block = _metablockCache.GetBlock(pos);
            if (block.Available >= 0)
            {
                return block;
            }

            Stream stream = _context.RawStream;
            stream.Position = pos;

            byte[] buffer = Utilities.ReadFully(stream, 2);

            int readLen = Utilities.ToUInt16LittleEndian(buffer, 0);
            bool isCompressed = (readLen & 0x8000) == 0;
            readLen &= 0x7FFF;
            if (readLen == 0)
            {
                readLen = 0x8000;
            }

            block.NextBlockStart = pos + readLen + 2;

            if (isCompressed)
            {
                if (_ioBuffer == null || readLen > _ioBuffer.Length)
                {
                    _ioBuffer = new byte[readLen];
                }

                if (Utilities.ReadFully(stream, _ioBuffer, 0, readLen) != readLen)
                {
                    throw new IOException("Truncated stream reading compressed metadata");
                }

                using (ZlibStream zlibStream = new ZlibStream(new MemoryStream(_ioBuffer, 0, readLen, false), CompressionMode.Decompress, true))
                {
                    block.Available = Utilities.ReadFully(zlibStream, block.Data, 0, MetadataBufferSize);
                }
            }
            else
            {
                block.Available = Utilities.ReadFully(stream, block.Data, 0, readLen);
            }

            return block;
        }
    }
}
