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
using DiscUtils.Vfs;

namespace DiscUtils.Udf
{
    internal class FileIdentifier : VfsDirEntry, IByteArraySerializable
    {
        public DescriptorTag DescriptorTag;
        public FileCharacteristic FileCharacteristics;
        public LongAllocationDescriptor FileLocation;
        public ushort FileVersionNumber;
        public byte[] ImplementationUse;
        public ushort ImplementationUseLength;
        public string Name;
        public byte NameLength;

        public override DateTime CreationTimeUtc
        {
            get { throw new NotSupportedException(); }
        }

        public override FileAttributes FileAttributes
        {
            get { throw new NotSupportedException(); }
        }

        public override string FileName
        {
            get { return Name; }
        }

        public override bool HasVfsFileAttributes
        {
            get { return false; }
        }

        public override bool HasVfsTimeInfo
        {
            get { return false; }
        }

        public override bool IsDirectory
        {
            get { return (FileCharacteristics & FileCharacteristic.Directory) != 0; }
        }

        public override bool IsSymlink
        {
            get { return false; }
        }

        public override DateTime LastAccessTimeUtc
        {
            get { throw new NotSupportedException(); }
        }

        public override DateTime LastWriteTimeUtc
        {
            get { throw new NotSupportedException(); }
        }

        public override long UniqueCacheId
        {
            get { return (long)FileLocation.ExtentLocation.Partition << 32 | FileLocation.ExtentLocation.LogicalBlock; }
        }

        public int Size
        {
            get { throw new NotImplementedException(); }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            DescriptorTag = EndianUtilities.ToStruct<DescriptorTag>(buffer, offset);
            FileVersionNumber = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 16);
            FileCharacteristics = (FileCharacteristic)buffer[offset + 18];
            NameLength = buffer[offset + 19];
            FileLocation = EndianUtilities.ToStruct<LongAllocationDescriptor>(buffer, offset + 20);
            ImplementationUseLength = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 36);
            ImplementationUse = EndianUtilities.ToByteArray(buffer, offset + 38, ImplementationUseLength);
            Name = UdfUtilities.ReadDCharacters(buffer, offset + 38 + ImplementationUseLength, NameLength);

            return MathUtilities.RoundUp(38 + ImplementationUseLength + NameLength, 4);
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }
    }
}