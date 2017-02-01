using DiscUtils.CoreCompat;
using DiscUtils.Iscsi;
using DiscUtils.Nfs;
using DiscUtils.OpticalDisk;

namespace DiscUtils.Transports
{
    public static class SetupHelper
    {
        public static void SetupTransports()
        {
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Disk)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(NfsFileSystem)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Disc)));
        }
    }
}