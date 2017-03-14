using System;

namespace DiscUtils.Iscsi
{
    [Flags]
    internal enum KeyUsagePhase
    {
        SecurityNegotiation = 0x01,

        OperationalNegotiation = 0x02,

        FullyFeatured = 0x04,

        All = 0x07
    }
}