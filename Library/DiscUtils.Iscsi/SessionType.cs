namespace DiscUtils.Iscsi
{
    internal enum SessionType
    {
        [ProtocolKeyValue("Discovery")]
        Discovery = 0,

        [ProtocolKeyValue("Normal")]
        Normal = 1
    }
}