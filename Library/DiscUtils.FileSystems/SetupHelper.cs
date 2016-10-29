using DiscUtils.CoreCompat;
using DiscUtils.Fat;
using DiscUtils.Ntfs;
using DiscUtils.OpticalDisk;

namespace DiscUtils.FileSystems
{
    public static class SetupHelper
    {
        public static void SetupFileSystems()
        {
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(FatFileSystem)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(NtfsFileSystem)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Disc)));
        }
    }
}
