using System;

namespace DiscUtils.HfsPlus
{
    [Flags]
    internal enum FileTypeFlags
    {
        None = 0x0,
        SymLinkFileType = 0x736C6E6B, /* 'slnk' */
        SymLinkCreator = 0x72686170, /* 'rhap' */
        HardLinkFileType = 0x686C6E6B, /* 'hlnk' */
        HFSPlusCreator = 0x6866732B /* 'hfs+' */
    }
}