//
// Copyright (c) 2017, Bianco Veigel
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

namespace DiscUtils.Nfs
{
    using System;

    public sealed class Nfs3FileSystemStat
    {
        public Nfs3FileSystemStat()
        {
        }

        internal Nfs3FileSystemStat(XdrDataReader reader)
        {
            TotalSizeBytes = reader.ReadUInt64();
            FreeSpaceBytes = reader.ReadUInt64();
            AvailableFreeSpaceBytes = reader.ReadUInt64();
            FileSlotCount = reader.ReadUInt64();
            FreeFileSlotCount = reader.ReadUInt64();
            AvailableFreeFileSlotCount = reader.ReadUInt64();
            uint invarsec = reader.ReadUInt32();
            if (invarsec == UInt32.MaxValue)
            {
                Invariant = TimeSpan.MaxValue;
                InvariantUntil = DateTime.MaxValue;
            }
            else
            {
                Invariant = TimeSpan.FromSeconds(invarsec);
                InvariantUntil = DateTime.Now.Add(Invariant);
            }
        }

        /// <summary>
        /// The total size, in bytes, of the file system.
        /// </summary>
        public ulong TotalSizeBytes { get; set; }

        /// <summary>
        /// The amount of free space, in bytes, in the file
        /// system.
        /// </summary>
        public ulong FreeSpaceBytes { get; set; }

        /// <summary>
        /// The amount of free space, in bytes, available to the
        /// user identified by the authentication information in
        /// the RPC.  (This reflects space that is reserved by the
        /// file system; it does not reflect any quota system
        /// implemented by the server.)
        /// </summary>
        public ulong AvailableFreeSpaceBytes { get; set; }

        /// <summary>
        /// The total number of file slots in the file system. (On
        /// a UNIX server, this often corresponds to the number of
        /// inodes configured.)
        /// </summary>
        public ulong FileSlotCount { get; set; }

        /// <summary>
        /// The number of free file slots in the file system.
        /// </summary>
        public ulong FreeFileSlotCount { get; set; }

        /// <summary>
        /// The number of free file slots that are available to the
        /// user corresponding to the authentication information in
        /// the RPC.  (This reflects slots that are reserved by the
        /// file system; it does not reflect any quota system
        /// implemented by the server.)
        /// </summary>
        public ulong AvailableFreeFileSlotCount { get; set; }

        /// <summary>
        /// A measure of file system volatility: this is the number
        /// of seconds for which the file system is not expected to
        /// change.For a volatile, frequently updated file system,
        /// this will be 0. For an immutable file system, such as a
        /// CD-ROM, this would be the largest unsigned integer.For
        /// file systems that are infrequently modified, for
        /// example, one containing local executable programs and
        /// on-line documentation, a value corresponding to a few
        /// hours or days might be used. The client may use this as
        /// a hint in tuning its cache management. Note however,
        /// this measure is assumed to be dynamic and may change at
        /// any time.
        /// </summary>
        public TimeSpan Invariant { get; set; }

        public DateTime InvariantUntil { get; private set; }

        internal void Write(XdrDataWriter writer)
        {
            writer.Write(TotalSizeBytes);
            writer.Write(FreeSpaceBytes);
            writer.Write(AvailableFreeSpaceBytes);
            writer.Write(FileSlotCount);
            writer.Write(FreeFileSlotCount);
            writer.Write(AvailableFreeFileSlotCount);

            if (Invariant == TimeSpan.MaxValue)
            {
                writer.Write(uint.MaxValue);
            }
            else
            {
                writer.Write((uint)Invariant.TotalSeconds);
            }
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Nfs3FileSystemStat);
        }

        public bool Equals(Nfs3FileSystemStat other)
        {
            if (other == null)
            {
                return false;
            }

            return other.TotalSizeBytes == TotalSizeBytes
                && other.FreeSpaceBytes == FreeSpaceBytes
                && other.AvailableFreeSpaceBytes == AvailableFreeSpaceBytes
                && other.FileSlotCount == FileSlotCount
                && other.FreeFileSlotCount == FreeFileSlotCount
                && other.AvailableFreeFileSlotCount == AvailableFreeFileSlotCount
                && other.Invariant == Invariant;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(TotalSizeBytes, FreeSpaceBytes, AvailableFreeSpaceBytes, FileSlotCount, FreeFileSlotCount, AvailableFreeFileSlotCount, Invariant);
        }
    }
}
