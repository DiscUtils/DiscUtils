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
using System.Collections.Generic;
using System.Text;

namespace DiscUtils.Udf
{
    internal enum AllocationType
    {
        ShortDescriptors = 0,
        LongDescriptors = 1,
        ExtendedDescriptors = 2,
        Embedded = 3,
    }

    [Flags]
    internal enum InformationControlBlockFlags
    {
        DirectorySorted = 0x0004,
        NonRelocatable = 0x0008,
        Archive = 0x0010,
        SetUid = 0x0020,
        SetGid = 0x0040,
        Sticky = 0x0080,
        Contiguous = 0x0100,
        System = 0x0200,
        Transformed = 0x0400,
        MultiVersions = 0x0800,
        Stream = 0x1000,
    }

    internal class InformationControlBlock : IByteArraySerializable
    {
        public uint PriorDirectEntries;
        public ushort StrategyType;
        public ushort StrategyParameter;
        public ushort MaxEntries;
        public FileType FileType;
        public LogicalBlockAddress ParentICBLocation;
        public AllocationType AllocationType;
        public InformationControlBlockFlags Flags;

        public int ReadFrom(byte[] buffer, int offset)
        {
            PriorDirectEntries = Utilities.ToUInt32LittleEndian(buffer, offset);
            StrategyType = Utilities.ToUInt16LittleEndian(buffer, offset + 4);
            StrategyParameter = Utilities.ToUInt16LittleEndian(buffer, offset + 6);
            MaxEntries = Utilities.ToUInt16LittleEndian(buffer, offset + 8);
            FileType = (FileType)buffer[offset + 11];
            ParentICBLocation = Utilities.ToStruct<LogicalBlockAddress>(buffer, offset + 12);

            ushort flagsField = Utilities.ToUInt16LittleEndian(buffer, offset + 18);
            AllocationType = (AllocationType)(flagsField & 0x3);
            Flags = (InformationControlBlockFlags)(flagsField & 0xFFFC);

            return 20;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public int Size
        {
            get { return 20; }
        }
    }
}
