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

using DiscUtils.Streams;

namespace DiscUtils.Udf
{
    internal sealed class UnallocatedSpaceDescriptor : TaggedDescriptor<UnallocatedSpaceDescriptor>
    {
        public ExtentAllocationDescriptor[] Extents;
        public uint VolumeDescriptorSequenceNumber;

        public UnallocatedSpaceDescriptor()
            : base(TagIdentifier.UnallocatedSpaceDescriptor) {}

        public override int Parse(byte[] buffer, int offset)
        {
            VolumeDescriptorSequenceNumber = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 16);

            uint numDescriptors = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 20);
            Extents = new ExtentAllocationDescriptor[numDescriptors];

            for (int i = 0; i < numDescriptors; ++i)
            {
                Extents[i] = EndianUtilities.ToStruct<ExtentAllocationDescriptor>(buffer, offset + 24 + i * 8);
            }

            return (int)(24 + numDescriptors * 8);
        }
    }
}