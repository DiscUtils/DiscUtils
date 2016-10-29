namespace DiscUtils.Iscsi
{
    internal enum RejectReason : byte
    {
        None = 0x00,
        Reserved = 0x01,
        DataDigestError = 0x02,
        SNACKReject = 0x03,
        ProtocolError = 0x04,
        CommandNotSupported = 0x05,
        ImmediateCommandReject = 0x06,
        TaskInProgress = 0x07,
        InvalidDataAck = 0x08,
        InvalidPduField = 0x09,
        LongOperationReject = 0x0a,
        NegotiationReset = 0x0b,
        WaitingForLogout = 0x0c
    }
}