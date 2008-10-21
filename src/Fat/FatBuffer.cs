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

using System;

namespace DiscUtils.Fat
{
    internal class FatBuffer
    {
        private FatType _type;
        private byte[] _buffer;

        public FatBuffer(FatType type, byte[] buffer)
        {
            _type = type;
            _buffer = buffer;
        }

        internal byte[] GetBytes()
        {
            return _buffer;
        }

        internal bool IsFree(uint val)
        {
            return val == 0;
        }

        internal bool IsEndOfChain(uint val)
        {
            switch (_type)
            {
                case FatType.FAT12: return val >= 0x0FF8;
                case FatType.FAT16: return val >= 0xFFF8;
                case FatType.FAT32: return val >= 0x0FFFFFF8;
                default: throw new Exception("Unknown FAT type");
            }
        }

        internal bool IsBadCluster(uint val)
        {
            switch (_type)
            {
                case FatType.FAT12: return val == 0x0FF7;
                case FatType.FAT16: return val == 0xFFF7;
                case FatType.FAT32: return val == 0x0FFFFFF7;
                default: throw new Exception("Unknown FAT type");
            }
        }

        internal uint GetNext(uint cluster)
        {
            if (_type == FatType.FAT16)
            {
                return BitConverter.ToUInt16(_buffer, (int)(cluster * 2));
            }
            else if (_type == FatType.FAT32)
            {
                return BitConverter.ToUInt32(_buffer, (int)(cluster * 4)) & 0x0FFFFFFF;
            }
            else // FAT12
            {
                if ((cluster & 1) != 0)
                {
                    return (uint)((BitConverter.ToUInt16(_buffer, (int)(cluster + (cluster / 2))) >> 4) & 0x0FFF);
                }
                else
                {
                    return (uint)(BitConverter.ToUInt16(_buffer, (int)(cluster + (cluster / 2))) & 0x0FFF);
                }
            }
        }

        internal void SetEndOfChain(uint cluster)
        {
            SetNext(cluster, 0xFFFFFFFF);
        }

        internal void SetBadCluster(uint cluster)
        {
            SetNext(cluster, 0xFFFFFFF7);
        }

        internal void SetNext(uint cluster, uint next)
        {
            if (_type == FatType.FAT16)
            {
                Array.Copy(BitConverter.GetBytes((ushort)next), 0, _buffer, (int)(cluster * 2), 2);
            }
            else if (_type == FatType.FAT32)
            {
                uint oldVal = BitConverter.ToUInt32(_buffer, (int)(cluster * 4));
                uint newVal = (oldVal & 0xF0000000) | (next & 0x0FFFFFFF);
                Array.Copy(BitConverter.GetBytes((uint)newVal), 0, _buffer, (int)(cluster * 4), 4);
            }
            else
            {
                int offset = (int)(cluster + (cluster / 2));

                ushort maskedOldVal;
                if ((cluster & 1) != 0)
                {
                    next = next << 4;
                    maskedOldVal = (ushort)(BitConverter.ToUInt16(_buffer, offset) & 0x000F);
                }
                else
                {
                    next = next & 0x0FFF;
                    maskedOldVal = (ushort)(BitConverter.ToUInt16(_buffer, offset) & 0xF000);
                }

                ushort newVal = (ushort)(maskedOldVal | next);

                Array.Copy(BitConverter.GetBytes(newVal), 0, _buffer, offset, 2);
            }
        }

        internal int NumEntries
        {
            get
            {
                switch (_type)
                {
                    case FatType.FAT12:
                        return (_buffer.Length / 3) * 2;
                    case FatType.FAT16:
                        return _buffer.Length / 2;
                    default: // FAT32
                        return _buffer.Length / 4;
                }
            }
        }

        internal bool TryGetFreeCluster(out uint cluster)
        {
            // Simple scan - don't hold a free list...
            uint numEntries = (uint)NumEntries;
            for (uint i = 0; i < numEntries; i++)
            {
                if (IsFree(GetNext(i)))
                {
                    cluster = i;
                    return true;
                }
            }

            cluster = 0;
            return false;
        }
    }
}
