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
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscUtils.Fat
{
    internal class FileAllocationTable
    {
        private FatType _type;
        private byte[] _cache;

        public FileAllocationTable(FatType type, byte[] buffer)
        {
            _type = type;
            _cache = buffer;
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
                return BitConverter.ToUInt16(_cache, (int)(cluster * 2));
            }
            else if (_type == FatType.FAT32)
            {
                return BitConverter.ToUInt32(_cache, (int)(cluster * 4)) & 0x0FFFFFFF;
            }
            else // FAT12
            {
                if ((cluster & 1) != 0)
                {
                    return (uint)((BitConverter.ToUInt16(_cache, (int)(cluster + (cluster / 2))) >> 4) & 0x0FFF);
                }
                else
                {
                    return (uint)(BitConverter.ToUInt16(_cache, (int)(cluster + (cluster / 2))) & 0x0FFF);
                }
            }
        }
    }
}
