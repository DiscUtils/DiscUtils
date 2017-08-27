using DiscUtils.Btrfs;
using DiscUtils.BootConfig;
using DiscUtils.CoreCompat;
using DiscUtils.Dmg;
using DiscUtils.Ext;
using DiscUtils.Fat;
using DiscUtils.HfsPlus;
using DiscUtils.Iso9660;
using DiscUtils.Nfs;
using DiscUtils.Ntfs;
using DiscUtils.OpticalDisk;
using DiscUtils.Registry;
using DiscUtils.Sdi;
using DiscUtils.SquashFs;
using DiscUtils.Udf;
using DiscUtils.Wim;
using DiscUtils.Xfs;

#if !NETCORE
using DiscUtils.Net.Dns;
using DiscUtils.OpticalDiscSharing;
#endif

namespace DiscUtils.Complete
{
    public static class SetupHelper
    {
        public static void SetupComplete()
        {
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Store)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Disk)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(BtrfsFileSystem)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(ExtFileSystem)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(FatFileSystem)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(HfsPlusFileSystem)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Iscsi.Disk)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(BuildFileInfo)));
#if !NETCORE
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(DnsClient)));
#endif
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Nfs3Status)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(NtfsFileSystem)));
#if !NETCORE
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(DiscInfo)));
#endif
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Disc)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(RegistryHive)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(SdiFile)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(SquashFileSystemBuilder)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Swap.SwapFileSystem)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(UdfReader)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Vdi.Disk)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Vhd.Disk)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Vhdx.Disk)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Vmdk.Disk)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(WimFile)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(XfsFileSystem)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Xva.Disk)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Lvm.LogicalVolumeManager)));
        }
    }
}