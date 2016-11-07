using DiscUtils.CoreCompat;
using DiscUtils.Dmg;
using DiscUtils.Iso9660;
using DiscUtils.Wim;

namespace DiscUtils.Containers
{
    public static class SetupHelper
    {
        public static void SetupContainers()
        {
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Disk)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(BuildFileInfo)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Vhd.Disk)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Vhdx.Disk)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Vmdk.Disk)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(WimFile)));
            Setup.SetupHelper.RegisterAssembly(ReflectionHelper.GetAssembly(typeof(Xva.Disk)));
        }
    }
}