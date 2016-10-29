using DiscUtils.CoreCompat;

namespace DiscUtils.Complete
{
    public static class SetupHelper
    {
        public static void SetupComplete()
        {
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Fat.FatFileSystem)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(HfsPlus.HfsPlusFileSystem)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Iscsi.Disk)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Iso9660.BuildFileInfo)));
#if !NETCORE
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Net.Dns.DnsClient)));
#endif
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Ntfs.NtfsFileSystem)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(OpticalDisk.Disc)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(SquashFs.SquashFileSystemBuilder)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Udf.UdfReader)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Vdi.Disk)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Vhd.Disk)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Vhdx.Disk)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Vmdk.Disk)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Wim.WimFile)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Xva.Disk)));
        }
    }
}
