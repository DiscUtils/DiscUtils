using DiscUtils.CoreCompat;

namespace DiscUtils.Transports
{
    public static class SetupHelper
    {
        public static void SetupTransports()
        {
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Iscsi.Disk)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Nfs.NfsFileSystem)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(OpticalDisk.Disc)));
        }
    }
}
