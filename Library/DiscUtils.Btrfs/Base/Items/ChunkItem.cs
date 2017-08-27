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

using DiscUtils.Streams;

namespace DiscUtils.Btrfs.Base.Items
{
    /// <summary>
    /// Maps logical address to physical
    /// </summary>
    internal class ChunkItem : BaseItem
    {
        public ChunkItem(Key key) : base(key) {}

        /// <summary>
        /// size of chunk (bytes)
        /// </summary>
        public ulong ChunkSize { get; private set; }

        /// <summary>
        /// root referencing this chunk (2)
        /// </summary>
        public ulong ObjectId { get; private set; }

        /// <summary>
        /// stripe length
        /// </summary>
        public ulong StripeLength { get; private set; }

        /// <summary>
        /// type (same as flags for block group?)
        /// </summary>
        public BlockGroupFlag Type { get; private set; }

        /// <summary>
        /// optimal io alignment
        /// </summary>
        public uint OptimalIoAlignment { get; private set; }

        /// <summary>
        /// optimal io width
        /// </summary>
        public uint OptimalIoWidth { get; private set; }

        /// <summary>
        /// minimal io size (sector size)
        /// </summary>
        public uint MinimalIoSize { get; private set; }

        /// <summary>
        /// number of stripes
        /// </summary>
        public ushort StripeCount { get; private set; }

        /// <summary>
        /// sub stripes
        /// </summary>
        public ushort SubStripes { get; private set; }

        public Stripe[] Stripes { get; private set; }

        public override int Size
        {
            get { return 0x30 + StripeCount * Stripe.Length; }
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            ChunkSize = EndianUtilities.ToUInt64LittleEndian(buffer, offset);
            ObjectId = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x8);
            StripeLength = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x10);
            Type = (BlockGroupFlag)EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x18);
            OptimalIoAlignment = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0x20);
            OptimalIoWidth = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0x24);
            MinimalIoSize = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0x28);
            StripeCount = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0x2c);
            SubStripes = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0x2e);
            Stripes = new Stripe[StripeCount];
            offset += 0x30;
            for (int i = 0; i < StripeCount; i++)
            {
                Stripes[i] = new Stripe();
                offset += Stripes[i].ReadFrom(buffer, offset);
            }
            return Size;
        }
    }
}
