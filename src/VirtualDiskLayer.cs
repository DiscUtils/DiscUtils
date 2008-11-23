using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscUtils
{
    /// <summary>
    /// Represents the base layer, or a differencing layer of a VirtualDisk.
    /// </summary>
    /// <remarks>
    /// <para>VirtualDisks are composed of one or more layers - a base layer
    /// which represents the entire disk (even if not all bytes are actually stored),
    /// and a number of differencing layers that store the disk sectors that are
    /// logically different to the base layer.</para>
    /// <para>Disk Layers may not store all sectors.  Any sectors that are not stored
    /// are logically zero's (for base layers), or holes through to the layer underneath
    /// (all other layers).  This method will not return data for sectors that aren't
    /// stored.</para>
    /// </remarks>
    public abstract class VirtualDiskLayer
    {
        /// <summary>
        /// Gets a value indicating if the layer only stores meaningful sectors.
        /// </summary>
        public abstract bool IsSparse
        {
            get;
        }

        /// <summary>
        /// Reads a logical disk sector, if available.
        /// </summary>
        /// <param name="sector">The logical block address (i.e. index) of the sector</param>
        /// <param name="buffer">The buffer to populate</param>
        /// <param name="offset">The offset in <paramref name="buffer"/> to place the first byte</param>
        /// <returns><code>true</code> if the sector is present, else <code>false</code></returns>
        public abstract bool TryReadSector(long sector, byte[] buffer, int offset);

        /// <summary>
        /// Reads sectors up until there's one that is 'not stored'.
        /// </summary>
        /// <param name="first">The logical block address (i.e. index) of the first sector</param>
        /// <param name="buffer">The buffer to populate</param>
        /// <param name="offset">The offset within the buffer to fill from</param>
        /// <param name="count">The maximum number of sectors to read</param>
        /// <returns>The number of sectors read, zero if none</returns>
        public abstract int ReadSectors(long first, byte[] buffer, int offset, int count);

        /// <summary>
        /// Writes one or more sectors.
        /// </summary>
        /// <param name="first">The logical block address (i.e. index) of the first sector</param>
        /// <param name="buffer">The buffer containing the data to write</param>
        /// <param name="offset">The offset within the buffer of the data to write</param>
        /// <param name="count">The number of bytes to write</param>
        public abstract void WriteSectors(long first, byte[] buffer, int offset, int count);

        /// <summary>
        /// Deletes one or more sectors.
        /// </summary>
        /// <param name="first">The logical block address (i.e. index) of the first sector to delete</param>
        /// <param name="count">The number of sectors to delete</param>
        /// <exception cref="System.NotSupportedException">The layer doesn't support absent sectors</exception>
        public abstract void DeleteSectors(long first, int count);

        /// <summary>
        /// Indicates if a particular sector is present in this layer.
        /// </summary>
        /// <param name="sector">The logical block address (i.e. index) of the sector</param>
        /// <returns><code>true</code> if the sector is present, else <code>false</code></returns>
        public abstract bool HasSector(long sector);

        /// <summary>
        /// Gets the Logical Block Address of the next sector stored in this layer.
        /// </summary>
        /// <param name="sector">The reference sector</param>
        /// <returns>The LBA of the next sector, or <code>-1</code> if none.</returns>
        public abstract long NextSector(long sector);
    }
}
