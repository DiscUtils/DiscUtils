using DiscUtils.CoreCompat;
using DiscUtils.Ext;
using DiscUtils.Fat;
using DiscUtils.HfsPlus;
using DiscUtils.Ntfs;
using DiscUtils.OpticalDisk;
using DiscUtils.SquashFs;
using DiscUtils.Xfs;

namespace DiscUtils.FileSystems
{
    public static class SetupHelper
    {
        public static void SetupFileSystems()
        {
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(ExtFileSystem)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(FatFileSystem)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(HfsPlusFileSystem)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(NtfsFileSystem)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Disc)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(SquashFileSystemBuilder)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(XfsFileSystem)));
        }
    }
}