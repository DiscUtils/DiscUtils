using System;
using DiscUtils.Swap;
using Xunit;

namespace LibraryTests.Swap
{
    public class InvalidDataTests
    {
        [Fact]
        public void InvalidSwapHeader()
        {
            var buffer = new byte[SwapHeader.PageSize];
            for (int i = 0; i < 16; i++)
            {
                buffer[0x41c + i] = 1;
            }
            var header = new SwapHeader();
            header.ReadFrom(buffer, 0);
        }
    }
}