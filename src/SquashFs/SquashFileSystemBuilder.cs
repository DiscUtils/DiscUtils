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

namespace DiscUtils.SquashFs
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using DiscUtils.Compression;

    /// <summary>
    /// Class that creates SquashFs file systems.
    /// </summary>
    public sealed class SquashFileSystemBuilder
    {
        private const int DefaultBlockSize = 131072;

        private BuilderDirectory _rootDir;
        private BuilderContext _context;
        private uint _nextInode;

        /// <summary>
        /// Initializes a new instance of the SquashFileSystemBuilder class.
        /// </summary>
        public SquashFileSystemBuilder()
        {
            _rootDir = new BuilderDirectory();
            _rootDir.Mode = UnixFilePermissions.OwnerAll | UnixFilePermissions.GroupRead | UnixFilePermissions.GroupExecute | UnixFilePermissions.OthersRead | UnixFilePermissions.OthersExecute;
        }

        /// <summary>
        /// Adds a file to the file system.
        /// </summary>
        /// <param name="path">The full path to the file.</param>
        /// <param name="content">The content of the file.</param>
        public void AddFile(string path, Stream content)
        {
            _rootDir.AddChild(path, new BuilderFile(content));
        }

        /// <summary>
        /// Builds the file system, returning a new stream.
        /// </summary>
        /// <returns>The stream containing the file system.</returns>
        /// <remarks>
        /// This method uses a temporary file to construct the file system, use of
        /// the <c>Build(Stream)</c> or <c>Build(string)</c> variant is recommended
        /// when the file system will be written to a file.
        /// </remarks>
        public SparseStream Build()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Writes the file system to an existing stream.
        /// </summary>
        /// <param name="output">The stream to write to.</param>
        /// <remarks>The <c>output</c> stream must support seeking and writing.</remarks>
        public void Build(Stream output)
        {
            if (output == null)
            {
                throw new ArgumentNullException("output");
            }

            if (!output.CanWrite)
            {
                throw new ArgumentException("Output stream must be writable", "output");
            }

            if (!output.CanSeek)
            {
                throw new ArgumentException("Output stream must support seeking", "output");
            }

            _context = new BuilderContext()
            {
                RawStream = output,
                DataBlockSize = DefaultBlockSize,
                IoBuffer = new byte[DefaultBlockSize],
            };

            MetablockWriter inodeWriter = new MetablockWriter();
            MetablockWriter dirWriter = new MetablockWriter();
            FragmentWriter fragWriter = new FragmentWriter(_context);
            IdTableWriter idWriter = new IdTableWriter(_context);

            _context.AllocateInode = AllocateInode;
            _context.AllocateId = idWriter.AllocateId;
            _context.WriteDataBlock = WriteDataBlock;
            _context.WriteFragment = fragWriter.WriteFragment;
            _context.InodeWriter = inodeWriter;
            _context.DirectoryWriter = dirWriter;

            _nextInode = 1;

            SuperBlock superBlock = new SuperBlock();
            superBlock.Magic = SuperBlock.SquashFsMagic;
            superBlock.CreationTime = DateTime.Now;
            superBlock.BlockSize = (uint)_context.DataBlockSize;
            superBlock.Compression = 1; // DEFLATE
            superBlock.BlockSizeLog2 = (ushort)Utilities.Log2(superBlock.BlockSize);
            superBlock.MajorVersion = 4;
            superBlock.MinorVersion = 0;

            output.Position = superBlock.Size;

            _rootDir.Reset();
            _rootDir.Write(_context);
            fragWriter.Flush();
            superBlock.RootInode = _rootDir.InodeRef;
            superBlock.InodesCount = _nextInode - 1;
            superBlock.FragmentsCount = (uint)fragWriter.FragmentCount;
            superBlock.UidGidCount = (ushort)idWriter.IdCount;

            superBlock.InodeTableStart = output.Position;
            inodeWriter.Persist(output);

            superBlock.DirectoryTableStart = output.Position;
            dirWriter.Persist(output);

            if (fragWriter.FragmentCount > 0)
            {
                superBlock.FragmentTableStart = output.Position;
                fragWriter.Persist();
            }
            else
            {
                superBlock.FragmentTableStart = -1;
            }

            superBlock.LookupTableStart = -1;

            if (idWriter.IdCount > 0)
            {
                superBlock.UidGidTableStart = output.Position;
                idWriter.Persist();
            }
            else
            {
                superBlock.UidGidTableStart = -1;
            }

            superBlock.ExtendedAttrsTableStart = -1;

            // Go back and write the superblock
            long end = output.Position;
            superBlock.BytesUsed = end;
            output.Position = 0;
            byte[] buffer = new byte[superBlock.Size];
            superBlock.WriteTo(buffer, 0);
            output.Write(buffer, 0, buffer.Length);
            output.Position = end;
        }

        /// <summary>
        /// Writes the stream contents to a file.
        /// </summary>
        /// <param name="outputFile">The file to write to.</param>
        public void Build(string outputFile)
        {
            using (FileStream destStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
            {
                Build(destStream);
            }
        }

        /// <summary>
        /// Allocates a unique inode identifier.
        /// </summary>
        /// <returns>The inode identifier</returns>
        private uint AllocateInode()
        {
            return _nextInode++;
        }

        /// <summary>
        /// Writes a block of file data, possibly compressing it.
        /// </summary>
        /// <param name="buffer">The data to write</param>
        /// <param name="offset">Offset of the first byte to write</param>
        /// <param name="count">The number of bytes to write</param>
        /// <returns>
        /// The 'length' of the (possibly compressed) data written, including
        /// a flag indicating compression (or not).
        /// </returns>
        private uint WriteDataBlock(byte[] buffer, int offset, int count)
        {
            MemoryStream compressed = new MemoryStream();
            using (ZlibStream compStream = new ZlibStream(compressed, CompressionMode.Compress, true))
            {
                compStream.Write(buffer, offset, count);
            }

            byte[] writeData;
            int writeOffset;
            int writeLen;
            if (compressed.Length < count)
            {
                writeData = compressed.GetBuffer();
                writeOffset = 0;
                writeLen = (int)compressed.Length;
            }
            else
            {
                writeData = buffer;
                writeOffset = offset;
                writeLen = count | 0x01000000;
            }

            _context.RawStream.Write(writeData, writeOffset, writeLen & 0xFFFFFF);

            return (uint)writeLen;
        }
    }
}
