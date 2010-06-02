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

using System;

namespace DiscUtils.Udf
{
    internal class File
    {
        protected UdfContext _context;
        protected Partition _partition;
        protected ExtendedFileEntry _fileEntry;
        protected uint _blockSize;
        protected IBuffer _content;

        public File(UdfContext context, Partition partition, ExtendedFileEntry fileEntry, uint blockSize)
        {
            _context = context;
            _partition = partition;
            _fileEntry = fileEntry;
            _blockSize = blockSize;
        }

        public IBuffer Content
        {
            get
            {
                if (_content != null)
                {
                    return _content;
                }

                _content = new FileContentBuffer(_partition, _fileEntry, _blockSize);
                return _content;
            }
        }

        public FileType FileType
        {
            get { return _fileEntry.InformationControlBlock.FileType; }
        }

        public DateTime AccessTimeUtc
        {
            get { return _fileEntry.AccessTime; }
        }

        public DateTime ModificationTimeUtc
        {
            get { return _fileEntry.ModificationTime; }
        }

        public DateTime CreationTimeUtc
        {
            get { return _fileEntry.CreationTime; }
        }

        public static File FromDescriptor(UdfContext context, LongAllocationDescriptor icb)
        {
            LogicalPartition partition = context.LogicalPartitions[icb.ExtentLocation.Partition];

            byte[] rootDirData = UdfUtilities.ReadExtent(context, icb);
            DescriptorTag rootDirTag = Utilities.ToStruct<DescriptorTag>(rootDirData, 0);

            if (rootDirTag.TagIdentifier == TagIdentifier.ExtendedFileEntry)
            {
                ExtendedFileEntry fileEntry = Utilities.ToStruct<ExtendedFileEntry>(rootDirData, 0);
                if (fileEntry.InformationControlBlock.FileType == FileType.Directory)
                {
                    return new Directory(context, partition, fileEntry);
                }
                else
                {
                    return new File(context, partition, fileEntry, (uint)partition.LogicalBlockSize);
                }
            }
            else
            {
                throw new NotImplementedException("Only ExtendedFileEntries implemented");
            }
        }

    }
}
