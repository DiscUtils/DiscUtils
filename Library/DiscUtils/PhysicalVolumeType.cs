namespace DiscUtils
{
    /// <summary>
    /// Enumeration of possible types of physical volume.
    /// </summary>
    public enum PhysicalVolumeType
    {
        /// <summary>
        /// Unknown type.
        /// </summary>
        None,

        /// <summary>
        /// Physical volume encompasses the entire disk.
        /// </summary>
        EntireDisk,

        /// <summary>
        /// Physical volume is defined by a BIOS-style partition table.
        /// </summary>
        BiosPartition,

        /// <summary>
        /// Physical volume is defined by a GUID partition table.
        /// </summary>
        GptPartition,

        /// <summary>
        /// Physical volume is defined by an Apple partition map.
        /// </summary>
        ApplePartition
    }
}