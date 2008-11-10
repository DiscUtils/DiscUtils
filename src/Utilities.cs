using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace DiscUtils
{
    internal class Utilities
    {
        /// <summary>
        /// The number of bytes in a standard disk sector (512).
        /// </summary>
        internal const int SectorSize = 512;

        /// <summary>
        /// Prevent instantiation.
        /// </summary>
        private Utilities() { }

        #region Stream Manipulation
        /// <summary>
        /// Read bytes until buffer filled or EOF.
        /// </summary>
        /// <param name="stream">The stream to read</param>
        /// <param name="buffer">The buffer to populate</param>
        /// <param name="offset">Offset in the buffer to start</param>
        /// <param name="length">The number of bytes to read</param>
        /// <returns>The number of bytes actually read.</returns>
        internal static int ReadFully(Stream stream, byte[] buffer, int offset, int length)
        {
            int totalRead = 0;
            int numRead = stream.Read(buffer, offset, length);
            while (numRead > 0)
            {
                totalRead += numRead;
                numRead = stream.Read(buffer, offset + totalRead, length - totalRead);
            }

            return totalRead;
        }

        /// <summary>
        /// Read bytes until buffer filled or throw IOException.
        /// </summary>
        /// <param name="stream">The stream to read</param>
        /// <param name="count">The number of bytes to read</param>
        /// <returns>The data read from the stream</returns>
        public static byte[] ReadFully(Stream stream, int count)
        {
            byte[] buffer = new byte[count];
            if (ReadFully(stream, buffer, 0, count) == count)
            {
                return buffer;
            }
            else
            {
                throw new IOException("Unable to complete read of " + count + " bytes");
            }
        }

        /// <summary>
        /// Reads a disk sector (512 bytes).
        /// </summary>
        /// <param name="stream">The stream to read</param>
        /// <returns></returns>
        public static byte[] ReadSector(Stream stream)
        {
            return ReadFully(stream, SectorSize);
        }
        #endregion

        #region Filesystem Support
        /// <summary>
        /// Converts a 'standard' wildcard file/path specification into a regular expression.
        /// </summary>
        /// <param name="pattern">The wildcard pattern to convert</param>
        /// <returns>The resultant regular expression</returns>
        /// <remarks>
        /// The wildcard * (star) matches zero or more characters (including '.'), and ?
        /// (question mark) matches precisely one character (except '.').
        /// </remarks>
        internal static Regex ConvertWildcardsToRegEx(string pattern)
        {
            if (!pattern.Contains('.'))
            {
                pattern += ".";
            }
            string query = "^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", "[^.]") + "$";
            return new Regex(query, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        #endregion

        #region LBA / CHS Calculations
        internal static ulong CHSToLBA(uint cylinder, uint head, uint sector, uint headsPerCylinder, uint sectorsPerTrack)
        {
            return ((((ulong)cylinder * (ulong)headsPerCylinder) + head) * (ulong)sectorsPerTrack) + sector - 1;
        }

        internal static void LBAToCHS(ulong LBA, uint headsPerCylinder, uint sectorsPerTrack, out uint cylinder, out uint head, out uint sector)
        {
            cylinder = (uint)(LBA / ((ulong)headsPerCylinder * (ulong)sectorsPerTrack));
            uint temp = (uint)(LBA % ((ulong)headsPerCylinder * (ulong)sectorsPerTrack));
            head = temp / sectorsPerTrack;
            sector = temp % sectorsPerTrack - 1;
        }

        internal static void CalcDefaultVHDGeometry(uint totalSectors, out ushort cylinders, out byte headsPerCylinder, out byte sectorsPerTrack )
        {
            // If more than ~128GB truncate at ~128GB
            if (totalSectors > 65535 * 16 * 255)
            {
                totalSectors = 65535 * 16 * 255;
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
                uint cylindersTimesHeads = totalSectors / sectorsPerTrack;
                headsPerCylinder = (byte)((cylindersTimesHeads + 1023) / 1024);

                if (headsPerCylinder < 4)
                {
                    headsPerCylinder = 4;
                }

                // If we need more than 1023 cylinders, or 16 heads, try more sectors per track
                if (cylindersTimesHeads >= (headsPerCylinder * 1024U) || headsPerCylinder > 16)
                {
                    sectorsPerTrack = 31;
                    headsPerCylinder = 16;
                    cylindersTimesHeads = totalSectors / sectorsPerTrack;
                }

                // We need 63 sectors per track to keep the cylinder count down
                if (cylindersTimesHeads >= (headsPerCylinder * 1024U))
                {
                    sectorsPerTrack = 63;
                    headsPerCylinder = 16;
                }

            }
            cylinders = (ushort)((totalSectors / sectorsPerTrack) / headsPerCylinder);
        }
        #endregion
    }
}
