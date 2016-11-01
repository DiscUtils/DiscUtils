using System;

namespace DiscUtils.Ntfs
{
    [Flags]
    internal enum IndexEntryFlags : ushort
    {
        None = 0x00,
        Node = 0x01,
        End = 0x02
    }
}