namespace DiscUtils.Ntfs
{
    internal enum FileNameNamespace : byte
    {
        Posix = 0,
        Win32 = 1,
        Dos = 2,
        Win32AndDos = 3
    }
}