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

namespace DiscUtils.Partitions
{
    /// <summary>
    /// Convenient access to well-known BIOS (MBR) Partition Types.
    /// </summary>
    public static class BiosPartitionTypes
    {
        /// <summary>
        /// Microsoft FAT12 (fewer than 32,680 sectors in the volume).
        /// </summary>
        public const byte Fat12 = 0x01;

        /// <summary>
        /// Microsoft FAT16 (32,680–65,535 sectors or 16 MB–33 MB).
        /// </summary>
        public const byte Fat16Small = 0x04;

        /// <summary>
        /// Extended Partition (contains other partitions).
        /// </summary>
        public const byte Extended = 0x05;

        /// <summary>
        /// Microsoft BIGDOS FAT16 (33 MB–4 GB).
        /// </summary>
        public const byte Fat16 = 0x06;

        /// <summary>
        /// Installable File System (NTFS).
        /// </summary>
        public const byte Ntfs = 0x07;

        /// <summary>
        /// Microsoft FAT32.
        /// </summary>
        public const byte Fat32 = 0x0B;

        /// <summary>
        /// Microsoft FAT32, accessed using Int13h BIOS LBA extensions.
        /// </summary>
        public const byte Fat32Lba = 0x0C;

        /// <summary>
        /// Microsoft BIGDOS FAT16, accessed using Int13h BIOS LBA extensions.
        /// </summary>
        public const byte Fat16Lba = 0x0E;

        /// <summary>
        /// Extended Partition (contains other partitions), accessed using Int13h BIOS LBA extensions.
        /// </summary>
        public const byte ExtendedLba = 0x0F;

        /// <summary>
        /// Windows Logical Disk Manager dynamic volume.
        /// </summary>
        public const byte WindowsDynamicVolume = 0x42;

        /// <summary>
        /// Linux Swap.
        /// </summary>
        public const byte LinuxSwap = 0x82;

        /// <summary>
        /// Linux Native (ext2 and friends).
        /// </summary>
        public const byte LinuxNative = 0x83;

        /// <summary>
        /// Linux Logical Volume Manager (LVM).
        /// </summary>
        public const byte LinuxLvm = 0x8E;

        /// <summary>
        /// GUID Partition Table (GPT) protective partition, fills entire disk.
        /// </summary>
        public const byte GptProtective = 0xEE;

        /// <summary>
        /// EFI System partition on an MBR disk.
        /// </summary>
        public const byte EfiSystem = 0xEF;

        /// <summary>
        /// Provides a string representation of some known BIOS partition types.
        /// </summary>
        /// <param name="type">The partition type to represent as a string.</param>
        /// <returns>The string representation.</returns>
        public static string ToString(byte type)
        {
            switch (type)
            {
                case 0x00:
                    return "Unused";
                case 0x01:
                    return "FAT12";
                case 0x02:
                    return "XENIX root";
                case 0x03:
                    return "XENIX /usr";
                case 0x04:
                    return "FAT16 (<32M)";
                case 0x05:
                    return "Extended (non-LBA)";
                case 0x06:
                    return "FAT16 (>32M)";
                case 0x07:
                    return "IFS (NTFS or HPFS)";
                case 0x0B:
                    return "FAT32 (non-LBA)";
                case 0x0C:
                    return "FAT32 (LBA)";
                case 0x0E:
                    return "FAT16 (LBA)";
                case 0x0F:
                    return "Extended (LBA)";
                case 0x11:
                    return "Hidden FAT12";
                case 0x12:
                    return "Vendor Config/Recovery/Diagnostics";
                case 0x14:
                    return "Hidden FAT16 (<32M)";
                case 0x16:
                    return "Hidden FAT16 (>32M)";
                case 0x17:
                    return "Hidden IFS (NTFS or HPFS)";
                case 0x1B:
                    return "Hidden FAT32 (non-LBA)";
                case 0x1C:
                    return "Hidden FAT32 (LBA)";
                case 0x1E:
                    return "Hidden FAT16 (LBA)";
                case 0x27:
                    return "Windows Recovery Environment";
                case 0x42:
                    return "Windows Dynamic Volume";
                case 0x80:
                    return "Minix v1.1 - v1.4a";
                case 0x81:
                    return "Minix / Early Linux";
                case 0x82:
                    return "Linux Swap";
                case 0x83:
                    return "Linux Native";
                case 0x84:
                    return "Hibernation";
                case 0x8E:
                    return "Linux LVM";
                case 0xA0:
                    return "Laptop Hibernation";
                case 0xA8:
                    return "Mac OS-X";
                case 0xAB:
                    return "Mac OS-X Boot";
                case 0xAF:
                    return "Mac OS-X HFS";
                case 0xC0:
                    return "NTFT";
                case 0xDE:
                    return "Dell OEM";
                case 0xEE:
                    return "GPT Protective";
                case 0xEF:
                    return "EFI";
                case 0xFB:
                    return "VMware File System";
                case 0xFC:
                    return "VMware Swap";
                case 0xFE:
                    return "IBM OEM";
                default:
                    return "Unknown";
            }
        }
    }
}