using System;

namespace DiscUtils.Udf
{
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
        Stream = 0x1000
    }
}