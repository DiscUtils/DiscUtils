//
// Copyright (c) 2008-2011, Kenneth Bell
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//

using System;
using System.Globalization;
using DiscUtils.Streams;

namespace DiscUtils
{
    /// <summary>
    /// Class whose instances represent disk geometries.
    /// </summary>
    /// <remarks>Instances of this class are immutable.</remarks>
    public sealed class Geometry
    {
        /// <summary>
        /// Initializes a new instance of the Geometry class.  The default 512 bytes per sector is assumed.
        /// </summary>
        /// <param name="cylinders">The number of cylinders of the disk.</param>
        /// <param name="headsPerCylinder">The number of heads (aka platters) of the disk.</param>
        /// <param name="sectorsPerTrack">The number of sectors per track/cylinder of the disk.</param>
        public Geometry(int cylinders, int headsPerCylinder, int sectorsPerTrack)
        {
            Cylinders = cylinders;
            HeadsPerCylinder = headsPerCylinder;
            SectorsPerTrack = sectorsPerTrack;
            BytesPerSector = 512;
        }

        /// <summary>
        /// Initializes a new instance of the Geometry class.
        /// </summary>
        /// <param name="cylinders">The number of cylinders of the disk.</param>
        /// <param name="headsPerCylinder">The number of heads (aka platters) of the disk.</param>
        /// <param name="sectorsPerTrack">The number of sectors per track/cylinder of the disk.</param>
        /// <param name="bytesPerSector">The number of bytes per sector of the disk.</param>
        public Geometry(int cylinders, int headsPerCylinder, int sectorsPerTrack, int bytesPerSector)
        {
            Cylinders = cylinders;
            HeadsPerCylinder = headsPerCylinder;
            SectorsPerTrack = sectorsPerTrack;
            BytesPerSector = bytesPerSector;
        }

        /// <summary>
        /// Initializes a new instance of the Geometry class.
        /// </summary>
        /// <param name="capacity">The total capacity of the disk.</param>
        /// <param name="headsPerCylinder">The number of heads (aka platters) of the disk.</param>
        /// <param name="sectorsPerTrack">The number of sectors per track/cylinder of the disk.</param>
        /// <param name="bytesPerSector">The number of bytes per sector of the disk.</param>
        public Geometry(long capacity, int headsPerCylinder, int sectorsPerTrack, int bytesPerSector)
        {
            Cylinders = (int)(capacity / (headsPerCylinder * (long)sectorsPerTrack * bytesPerSector));
            HeadsPerCylinder = headsPerCylinder;
            SectorsPerTrack = sectorsPerTrack;
            BytesPerSector = bytesPerSector;
        }

        /// <summary>
        /// Gets the number of bytes in each sector.
        /// </summary>
        public int BytesPerSector { get; }

        /// <summary>
        /// Gets the total capacity of the disk (in bytes).
        /// </summary>
        public long Capacity
        {
            get { return TotalSectorsLong * BytesPerSector; }
        }

        /// <summary>
        /// Gets the number of cylinders.
        /// </summary>
        public int Cylinders { get; }

        /// <summary>
        /// Gets the number of heads (aka platters).
        /// </summary>
        public int HeadsPerCylinder { get; }

        /// <summary>
        /// Gets a value indicating whether the Geometry is representable both by the BIOS and by IDE.
        /// </summary>
        public bool IsBiosAndIdeSafe
        {
            get { return Cylinders <= 1024 && HeadsPerCylinder <= 16 && SectorsPerTrack <= 63; }
        }

        /// <summary>
        /// Gets a value indicating whether the Geometry is consistent with the values a BIOS can support.
        /// </summary>
        public bool IsBiosSafe
        {
            get { return Cylinders <= 1024 && HeadsPerCylinder <= 255 && SectorsPerTrack <= 63; }
        }

        /// <summary>
        /// Gets a value indicating whether the Geometry is consistent with the values IDE can represent.
        /// </summary>
        public bool IsIdeSafe
        {
            get { return Cylinders <= 65536 && HeadsPerCylinder <= 16 && SectorsPerTrack <= 255; }
        }

        /// <summary>
        /// Gets the address of the last sector on the disk.
        /// </summary>
        public ChsAddress LastSector
        {
            get { return new ChsAddress(Cylinders - 1, HeadsPerCylinder - 1, SectorsPerTrack); }
        }

        /// <summary>
        /// Gets a null geometry, which has 512-byte sectors but zero sectors, tracks or cylinders.
        /// </summary>
        public static Geometry Null
        {
            get { return new Geometry(0, 0, 0, 512); }
        }

        /// <summary>
        /// Gets the number of sectors per track.
        /// </summary>
        public int SectorsPerTrack { get; }

        /// <summary>
        /// Gets the total size of the disk (in sectors).
        /// </summary>
        [Obsolete("Use TotalSectorsLong instead, to support very large disks.")]
        public int TotalSectors
        {
            get { return Cylinders * HeadsPerCylinder * SectorsPerTrack; }
        }

        /// <summary>
        /// Gets the total size of the disk (in sectors).
        /// </summary>
        public long TotalSectorsLong
        {
            get { return Cylinders * (long)HeadsPerCylinder * SectorsPerTrack; }
        }

        /// <summary>
        /// Gets the 'Large' BIOS geometry for a disk, given it's physical geometry.
        /// </summary>
        /// <param name="ideGeometry">The physical (aka IDE) geometry of the disk.</param>
        /// <returns>The geometry a BIOS using the 'Large' method for calculating disk geometry will indicate for the disk.</returns>
        public static Geometry LargeBiosGeometry(Geometry ideGeometry)
        {
            int cylinders = ideGeometry.Cylinders;
            int heads = ideGeometry.HeadsPerCylinder;
            int sectors = ideGeometry.SectorsPerTrack;

            while (cylinders > 1024 && heads <= 127)
            {
                cylinders >>= 1;
                heads <<= 1;
            }

            return new Geometry(cylinders, heads, sectors);
        }

        /// <summary>
        /// Gets the 'LBA Assisted' BIOS geometry for a disk, given it's capacity.
        /// </summary>
        /// <param name="capacity">The capacity of the disk.</param>
        /// <returns>The geometry a BIOS using the 'LBA Assisted' method for calculating disk geometry will indicate for the disk.</returns>
        public static Geometry LbaAssistedBiosGeometry(long capacity)
        {
            int heads;
            if (capacity <= 504 * Sizes.OneMiB)
            {
                heads = 16;
            }
            else if (capacity <= 1008 * Sizes.OneMiB)
            {
                heads = 32;
            }
            else if (capacity <= 2016 * Sizes.OneMiB)
            {
                heads = 64;
            }
            else if (capacity <= 4032 * Sizes.OneMiB)
            {
                heads = 128;
            }
            else
            {
                heads = 255;
            }

            int sectors = 63;
            int cylinders = (int)Math.Min(1024, capacity / (sectors * (long)heads * Sizes.Sector));
            return new Geometry(cylinders, heads, sectors, Sizes.Sector);
        }

        /// <summary>
        /// Converts a geometry into one that is BIOS-safe, if not already.
        /// </summary>
        /// <param name="geometry">The geometry to make BIOS-safe.</param>
        /// <param name="capacity">The capacity of the disk.</param>
        /// <returns>The new geometry.</returns>
        /// <remarks>This method returns the LBA-Assisted geometry if the given geometry isn't BIOS-safe.</remarks>
        public static Geometry MakeBiosSafe(Geometry geometry, long capacity)
        {
            if (geometry == null)
            {
                return LbaAssistedBiosGeometry(capacity);
            }
            if (geometry.IsBiosSafe)
            {
                return geometry;
            }
            return LbaAssistedBiosGeometry(capacity);
        }

        /// <summary>
        /// Calculates a sensible disk geometry for a disk capacity using the VHD algorithm (errs under).
        /// </summary>
        /// <param name="capacity">The desired capacity of the disk.</param>
        /// <returns>The appropriate disk geometry.</returns>
        /// <remarks>The geometry returned tends to produce a disk with less capacity
        /// than requested (an exact capacity is not always possible).  The geometry returned is the IDE
        /// (aka Physical) geometry of the disk, not necessarily the geometry used by the BIOS.</remarks>
        public static Geometry FromCapacity(long capacity)
        {
            return FromCapacity(capacity, Sizes.Sector);
        }

        /// <summary>
        /// Calculates a sensible disk geometry for a disk capacity using the VHD algorithm (errs under).
        /// </summary>
        /// <param name="capacity">The desired capacity of the disk.</param>
        /// <param name="sectorSize">The logical sector size of the disk.</param>
        /// <returns>The appropriate disk geometry.</returns>
        /// <remarks>The geometry returned tends to produce a disk with less capacity
        /// than requested (an exact capacity is not always possible).  The geometry returned is the IDE
        /// (aka Physical) geometry of the disk, not necessarily the geometry used by the BIOS.</remarks>
        public static Geometry FromCapacity(long capacity, int sectorSize)
        {
            int totalSectors;
            int cylinders;
            int headsPerCylinder;
            int sectorsPerTrack;

            // If more than ~128GB truncate at ~128GB
            if (capacity > 65535 * (long)16 * 255 * sectorSize)
            {
                totalSectors = 65535 * 16 * 255;
            }
            else
            {
                totalSectors = (int)(capacity / sectorSize);
            }

            // If more than ~32GB, break partition table compatibility.
            // Partition table has max 63 sectors per track.  Otherwise
            // we're looking for a geometry that's valid for both BIOS
            // and ATA.
            if (totalSectors > 65535 * 16 * 63)
            {
                sectorsPerTrack = 255;
                headsPerCylinder = 16;
            }
            else
            {
                sectorsPerTrack = 17;
                int cylindersTimesHeads = totalSectors / sectorsPerTrack;
                headsPerCylinder = (cylindersTimesHeads + 1023) / 1024;

                if (headsPerCylinder < 4)
                {
                    headsPerCylinder = 4;
                }

                // If we need more than 1023 cylinders, or 16 heads, try more sectors per track
                if (cylindersTimesHeads >= headsPerCylinder * 1024U || headsPerCylinder > 16)
                {
                    sectorsPerTrack = 31;
                    headsPerCylinder = 16;
                    cylindersTimesHeads = totalSectors / sectorsPerTrack;
                }

                // We need 63 sectors per track to keep the cylinder count down
                if (cylindersTimesHeads >= headsPerCylinder * 1024U)
                {
                    sectorsPerTrack = 63;
                    headsPerCylinder = 16;
                }
            }

            cylinders = totalSectors / sectorsPerTrack / headsPerCylinder;

            return new Geometry(cylinders, headsPerCylinder, sectorsPerTrack, sectorSize);
        }

        /// <summary>
        /// Converts a CHS (Cylinder,Head,Sector) address to a LBA (Logical Block Address).
        /// </summary>
        /// <param name="chsAddress">The CHS address to convert.</param>
        /// <returns>The Logical Block Address (in sectors).</returns>
        public long ToLogicalBlockAddress(ChsAddress chsAddress)
        {
            return ToLogicalBlockAddress(chsAddress.Cylinder, chsAddress.Head, chsAddress.Sector);
        }

        /// <summary>
        /// Converts a CHS (Cylinder,Head,Sector) address to a LBA (Logical Block Address).
        /// </summary>
        /// <param name="cylinder">The cylinder of the address.</param>
        /// <param name="head">The head of the address.</param>
        /// <param name="sector">The sector of the address.</param>
        /// <returns>The Logical Block Address (in sectors).</returns>
        public long ToLogicalBlockAddress(int cylinder, int head, int sector)
        {
            if (cylinder < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cylinder), cylinder, "cylinder number is negative");
            }

            if (head >= HeadsPerCylinder)
            {
                throw new ArgumentOutOfRangeException(nameof(head), head, "head number is larger than disk geometry");
            }

            if (head < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(head), head, "head number is negative");
            }

            if (sector > SectorsPerTrack)
            {
                throw new ArgumentOutOfRangeException(nameof(sector), sector,
                    "sector number is larger than disk geometry");
            }

            if (sector < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(sector), sector,
                    "sector number is less than one (sectors are 1-based)");
            }

            return (cylinder * (long)HeadsPerCylinder + head) * SectorsPerTrack + sector - 1;
        }

        /// <summary>
        /// Converts a LBA (Logical Block Address) to a CHS (Cylinder, Head, Sector) address.
        /// </summary>
        /// <param name="logicalBlockAddress">The logical block address (in sectors).</param>
        /// <returns>The address in CHS form.</returns>
        public ChsAddress ToChsAddress(long logicalBlockAddress)
        {
            if (logicalBlockAddress < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(logicalBlockAddress), logicalBlockAddress,
                    "Logical Block Address is negative");
            }

            int cylinder = (int)(logicalBlockAddress / (HeadsPerCylinder * SectorsPerTrack));
            int temp = (int)(logicalBlockAddress % (HeadsPerCylinder * SectorsPerTrack));
            int head = temp / SectorsPerTrack;
            int sector = temp % SectorsPerTrack + 1;

            return new ChsAddress(cylinder, head, sector);
        }

        /// <summary>
        /// Translates an IDE (aka Physical) geometry to a BIOS (aka Logical) geometry.
        /// </summary>
        /// <param name="translation">The translation to perform.</param>
        /// <returns>The translated disk geometry.</returns>
        public Geometry TranslateToBios(GeometryTranslation translation)
        {
            return TranslateToBios(0, translation);
        }

        /// <summary>
        /// Translates an IDE (aka Physical) geometry to a BIOS (aka Logical) geometry.
        /// </summary>
        /// <param name="capacity">The capacity of the disk, required if the geometry is an approximation on the actual disk size.</param>
        /// <param name="translation">The translation to perform.</param>
        /// <returns>The translated disk geometry.</returns>
        public Geometry TranslateToBios(long capacity, GeometryTranslation translation)
        {
            if (capacity <= 0)
            {
                capacity = TotalSectorsLong * 512L;
            }

            switch (translation)
            {
                case GeometryTranslation.None:
                    return this;

                case GeometryTranslation.Auto:
                    if (IsBiosSafe)
                    {
                        return this;
                    }
                    return LbaAssistedBiosGeometry(capacity);

                case GeometryTranslation.Lba:
                    return LbaAssistedBiosGeometry(capacity);

                case GeometryTranslation.Large:
                    return LargeBiosGeometry(this);

                default:
                    throw new ArgumentException(
                        string.Format(CultureInfo.InvariantCulture, "Translation mode '{0}' not yet implemented",
                            translation), nameof(translation));
            }
        }

        /// <summary>
        /// Determines if this object is equivalent to another.
        /// </summary>
        /// <param name="obj">The object to test against.</param>
        /// <returns><c>true</c> if the <paramref name="obj"/> is equivalent, else <c>false</c>.</returns>
        public override bool Equals(object obj)
        {
            if (obj == null || obj.GetType() != GetType())
            {
                return false;
            }

            Geometry other = (Geometry)obj;

            return Cylinders == other.Cylinders && HeadsPerCylinder == other.HeadsPerCylinder
                   && SectorsPerTrack == other.SectorsPerTrack && BytesPerSector == other.BytesPerSector;
        }

        /// <summary>
        /// Calculates the hash code for this object.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            return Cylinders.GetHashCode() ^ HeadsPerCylinder.GetHashCode()
                   ^ SectorsPerTrack.GetHashCode() ^ BytesPerSector.GetHashCode();
        }

        /// <summary>
        /// Gets a string representation of this object, in the form (C/H/S).
        /// </summary>
        /// <returns>The string representation.</returns>
        public override string ToString()
        {
            if (BytesPerSector == 512)
            {
                return "(" + Cylinders + "/" + HeadsPerCylinder + "/" + SectorsPerTrack + ")";
            }
            return "(" + Cylinders + "/" + HeadsPerCylinder + "/" + SectorsPerTrack + ":" + BytesPerSector + ")";
        }
    }
}