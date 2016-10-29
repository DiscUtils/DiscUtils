using System;

namespace DiscUtils.Wim
{
    [Flags]
    internal enum FileFlags
    {
        Compression = 0x00000002,
        ReadOnly = 0x00000004,
        Spanned = 0x00000008,
        ResourceOnly = 0x00000010,
        MetaDataOnly = 0x00000020,
        WriteInProgress = 0x00000040,
        ReparsePointFix = 0x00000080,
        XpressCompression = 0x00020000,
        LzxCompression = 0x00040000
    }
}