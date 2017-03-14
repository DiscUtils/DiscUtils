using System;

namespace DiscUtils.Ntfs
{
    [Flags]
    internal enum AttributeTypeFlags
    {
        None = 0x00,
        Indexed = 0x02,
        Multiple = 0x04,
        NotZero = 0x08,
        IndexedUnique = 0x10,
        NamedUnique = 0x20,
        MustBeResident = 0x40,
        CanBeNonResident = 0x80
    }
}