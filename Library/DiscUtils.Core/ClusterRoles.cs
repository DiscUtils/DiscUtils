using System;

namespace DiscUtils
{
    /// <summary>
    /// Enumeration of possible cluster roles.
    /// </summary>
    /// <remarks>A cluster may be in more than one role.</remarks>
    [Flags]
    public enum ClusterRoles
    {
        /// <summary>
        /// Unknown, or unspecified role.
        /// </summary>
        None = 0x00,

        /// <summary>
        /// Cluster is free.
        /// </summary>
        Free = 0x01,

        /// <summary>
        /// Cluster is in use by a normal file.
        /// </summary>
        DataFile = 0x02,

        /// <summary>
        /// Cluster is in use by a system file.
        /// </summary>
        /// <remarks>This isn't a file marked with the 'system' attribute,
        /// rather files that form part of the file system namespace but also
        /// form part of the file system meta-data.</remarks>
        SystemFile = 0x04,

        /// <summary>
        /// Cluster is in use for meta-data.
        /// </summary>
        Metadata = 0x08,

        /// <summary>
        /// Cluster contains the boot region.
        /// </summary>
        BootArea = 0x10,

        /// <summary>
        /// Cluster is marked bad.
        /// </summary>
        Bad = 0x20
    }
}