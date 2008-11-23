//
// Copyright (c) 2008, Kenneth Bell
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
    public sealed class BiosPartitionTypes
    {
        private BiosPartitionTypes() { }

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
    }
}
