using System;
using System.Collections.Generic;
using System.Text;

namespace DiscUtils.Nfs
{
    // For more information, see
    // https://www.ietf.org/rfc/rfc1813.txt Appendix I: Mount Protocol
    internal enum MountProc3 : uint
    {
        // Null - Do nothing
        Null = 0,

        // MNT - Add mount entry
        Mnt = 1,

        // DUMP - Return mount entries
        Dump = 2,

        // UMNT - Remove mount entry
        Umnt = 3,

        // UMNTALL - Remove all mount entries
        UmntAll = 4,

        // EXPORT - Return export list
        Export = 5,
    }
}
