//
// Copyright (c) 2016, Bianco Veigel
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
using System.IO;

namespace DiscUtils.Xfs
{
    internal class BlockDirectoryDataEntry : BlockDirectoryData, IDirectoryEntry
    {
        public ulong Inode { get; private set; }

        public byte NameLength { get; private set; }

        public byte[] Name { get; private set; }

        public ushort Tag { get; private set; }

        public SuperBlock SuperBlock { get; private set; }

        public bool sb_ok = false;

        public int has_ftype = 0;

        public BlockDirectoryDataEntry(SuperBlock sb)
        {
            SuperBlock = sb;
            sb_ok = true;
        }

        public override int Size
        {
            get
            {
                var size = 0xb + NameLength;
                if(has_ftype != 0)
                {
                    size++;
                }
                var padding = size%8;
                if (padding != 0)
                    return size + (8 - padding);
                return size;
            }
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            if (sb_ok == false)
                throw new IOException("not initial SuperBlock");


            Inode = EndianUtilities.ToUInt64BigEndian(buffer, offset);
            NameLength = buffer[offset + 0x8];
            Name = EndianUtilities.ToByteArray(buffer, offset + 0x9, NameLength);

            /* according to linux kernel
             static inline int xfs_sb_version_hasftype(struct xfs_sb *sbp)
                {
	                return (XFS_SB_VERSION_NUM(sbp) == XFS_SB_VERSION_5 &&
		                xfs_sb_has_incompat_feature(sbp, XFS_SB_FEAT_INCOMPAT_FTYPE)) ||
	                       (xfs_sb_version_hasmorebits(sbp) &&
		                 (sbp->sb_features2 & XFS_SB_VERSION2_FTYPE));
                }
             *******/
             if(SuperBlock.SB_hasftype == true)
            //if( (((SuperBlock.SbVersion == 5) || ((SuperBlock.Version & 0x8000) != 0)) && ((SuperBlock.Features2 & 0x00000200) != 0)) ||
             //   ((SuperBlock.SbVersion == 5) && (SuperBlock.IncompatibleFeatures&0x0001)!=0))
            {//has ftype in dir inode
                var padding = 6 - ((NameLength + 1 + 1) % 8);//skip ftype
                offset += padding;
                has_ftype = 1;
            }
            else
            {
                var padding = 6 - ((NameLength + 1) % 8);
                offset += padding;
                has_ftype = 0;
            }
            Tag = EndianUtilities.ToUInt16BigEndian(buffer, offset + 0x9 + NameLength);//length u16
            return Size;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{Inode}: {EndianUtilities.BytesToString(Name, 0, NameLength)}";
        }
    }
}
