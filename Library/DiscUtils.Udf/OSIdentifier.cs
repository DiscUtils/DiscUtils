namespace DiscUtils.Udf
{
    internal enum OSIdentifier : ushort
    {
        DosOrWindows3 = 0x0100,
        Os2 = 0x0200,
        MacintoshOs9 = 0x0300,
        MacintoshOsX = 0x0301,
        UnixGeneric = 0x0400,
        UnixAix = 0x0401,
        UnixSunOS = 0x0402,
        UnixHPUX = 0x0403,
        UnixIrix = 0x0404,
        UnixLinux = 0x0405,
        UnixMkLinux = 0x0406,
        UnixFreeBsd = 0x0407,
        UnixNetBsd = 0x0408,
        Windows9x = 0x0500,
        WindowsNt = 0x0600,
        Os400 = 0x0700,
        BeOS = 0x0800,
        WindowsCe = 0x0900
    }
}