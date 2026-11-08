//
// Copyright (c) 2026, DiscUtils contributors
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
using DiscUtils.Iscsi;

namespace LibraryTests.Iscsi;

public class ScsiSynchronizeCacheTest
{
    [Fact]
    public void SynchronizeCache10RequestsWholeLunAndWaitsForCompletion()
    {
        var command = new ScsiSynchronizeCache10Command(0x123456);
        var bytes = new byte[command.Size];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = 0xFF;
        }

        command.WriteTo(bytes);

        Assert.Equal(10, command.Size);
        Assert.Equal((ulong)0x123456, command.TargetLun);
        Assert.Equal(TaskAttributes.Simple, command.TaskAttributes);
        // LBA 0, number of blocks 0: all blocks through the end of the LUN.
        // IMMED=0: do not acknowledge before synchronization finishes.
        Assert.Equal(new byte[] { 0x35, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, bytes);
    }

    [Fact]
    public void DiskStreamOverridesTheStrongSparseFlushOverload()
    {
        var flush = typeof(DiskStream).GetMethod(nameof(DiskStream.Flush), new[] { typeof(bool) });

        Assert.NotNull(flush);
        Assert.Equal(typeof(DiskStream), flush.DeclaringType);
    }
}
