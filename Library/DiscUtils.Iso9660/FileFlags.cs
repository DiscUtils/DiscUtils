using System;

namespace DiscUtils.Iso9660
{
    [Flags]
    internal enum FileFlags : byte
    {
        None = 0x00,
        Hidden = 0x01,
        Directory = 0x02,
        AssociatedFile = 0x04,
        Record = 0x08,
        Protection = 0x10,
        MultiExtent = 0x80
    }
}