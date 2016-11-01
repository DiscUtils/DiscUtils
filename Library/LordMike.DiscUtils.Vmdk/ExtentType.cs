namespace DiscUtils.Vmdk
{
    internal enum ExtentType
    {
        Flat = 0,
        Sparse = 1,
        Zero = 2,
        Vmfs = 3,
        VmfsSparse = 4,
        VmfsRdm = 5,
        VmfsRaw = 6
    }
}