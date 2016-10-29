using System;

namespace DiscUtils.Ntfs
{
    [Flags]
    internal enum FileRecordFlags : ushort
    {
        None = 0x0000,
        InUse = 0x0001,
        IsDirectory = 0x0002,
        IsMetaFile = 0x0004,
        HasViewIndex = 0x0008
    }
}