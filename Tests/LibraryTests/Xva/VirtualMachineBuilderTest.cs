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
using DiscUtils;
using DiscUtils.Streams;
using DiscUtils.Xva;
using Xunit;

namespace LibraryTests.Xva
{
    public class VirtualMachineBuilderTest
    {
        [Fact]
        public void TestEmpty()
        {
            MemoryStream xvaStream = new MemoryStream();
            VirtualMachineBuilder vmb = new VirtualMachineBuilder();
            vmb.AddDisk("Foo", new MemoryStream(), Ownership.Dispose);
            vmb.Build(xvaStream);

            Assert.NotEqual(0, xvaStream.Length);

            VirtualMachine vm = new VirtualMachine(xvaStream);
            List<Disk> disks = new List<Disk>(vm.Disks);
            Assert.Equal(1, disks.Count);
            Assert.Equal(0, disks[0].Capacity);
        }

        [Fact]
        public void TestNotEmpty()
        {
            MemoryStream xvaStream = new MemoryStream();
            VirtualMachineBuilder vmb = new VirtualMachineBuilder();

            MemoryStream ms = new MemoryStream();
            for (int i = 0; i < 1024 * 1024; ++i)
            {
                ms.Position = i * 10;
                ms.WriteByte((byte)(i ^ (i >> 8) ^ (i >> 16) ^ (i >> 24)));
            }

            vmb.AddDisk("Foo", ms, Ownership.Dispose);
            vmb.Build(xvaStream);

            Assert.NotEqual(0, xvaStream.Length);

            VirtualMachine vm = new VirtualMachine(xvaStream);
            List<Disk> disks = new List<Disk>(vm.Disks);
            Assert.Equal(1, disks.Count);
            Assert.Equal(10 * 1024 * 1024, disks[0].Capacity);

            Stream diskContent = disks[0].Content;
            for (int i = 0; i < 1024 * 1024; ++i)
            {
                diskContent.Position = i * 10;
                if ((byte)(i ^ (i >> 8) ^ (i >> 16) ^ (i >> 24)) != diskContent.ReadByte())
                {
                    Assert.True(false, "Mismatch at offset " + i);
                }
            }
        }
    }
}