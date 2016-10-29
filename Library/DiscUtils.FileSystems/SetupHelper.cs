using DiscUtils.CoreCompat;

namespace DiscUtils.FileSystems
{
    public static class SetupHelper
    {
        public static void SetupFileSystems()
        {
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Fat.FatFileSystem)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(HfsPlus.HfsPlusFileSystem)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Ntfs.NtfsFileSystem)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(OpticalDisk.Disc)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(SquashFs.SquashFileSystemBuilder)));
        }
    }
}
