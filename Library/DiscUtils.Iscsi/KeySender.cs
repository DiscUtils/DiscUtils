using System;

namespace DiscUtils.Iscsi
{
    [Flags]
    internal enum KeySender
    {
        Initiator = 0x01,

        Target = 0x02,

        Both = 0x03
    }
}