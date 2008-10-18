using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace DiscUtils
{
    internal class Utilities
    {
        /// <summary>
        /// The number of bytes in a standard disk sector (512).
        /// </summary>
        internal const int SectorSize = 512;

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

    }
}
