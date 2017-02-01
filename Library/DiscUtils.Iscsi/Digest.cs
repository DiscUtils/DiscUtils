namespace DiscUtils.Iscsi
{
    internal enum Digest
    {
        [ProtocolKeyValue("None")]
        None,

        [ProtocolKeyValue("CRC32C")]
        Crc32c
    }
}