namespace DiscUtils
{
    /// <summary>
    /// Enumeration of the health status of a logical volume.
    /// </summary>
    public enum LogicalVolumeStatus
    {
        /// <summary>
        /// The volume is healthy and fully functional.
        /// </summary>
        Healthy = 0,

        /// <summary>
        /// The volume is completely accessible, but at degraded redundancy.
        /// </summary>
        FailedRedundancy = 1,

        /// <summary>
        /// The volume is wholey, or partly, inaccessible.
        /// </summary>
        Failed = 2
    }
}