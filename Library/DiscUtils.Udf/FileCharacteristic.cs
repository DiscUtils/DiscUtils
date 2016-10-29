using System;

namespace DiscUtils.Udf
{
    [Flags]
    internal enum FileCharacteristic : byte
    {
        Existence = 0x01,
        Directory = 0x02,
        Deleted = 0x04,
        Parent = 0x08,
        Metadata = 0x10
    }
}