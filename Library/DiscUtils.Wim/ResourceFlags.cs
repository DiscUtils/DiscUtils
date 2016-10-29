using System;

namespace DiscUtils.Wim
{
    [Flags]
    internal enum ResourceFlags : byte
    {
        None = 0x00,
        Free = 0x01,
        MetaData = 0x02,
        Compressed = 0x04,
        Spanned = 0x08
    }
}