namespace DiscUtils.Ntfs
{
    internal enum AttributeCollationRule
    {
        Binary = 0x00000000,
        Filename = 0x00000001,
        UnicodeString = 0x00000002,
        UnsignedLong = 0x00000010,
        Sid = 0x00000011,
        SecurityHash = 0x00000012,
        MultipleUnsignedLongs = 0x00000013
    }
}