//
// Copyright (c) 2008-2010, Kenneth Bell
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
using System.IO;
using System.Collections.Generic;
using System.Security.AccessControl;

namespace DiscUtils.Ntfs
{
    internal sealed class IndexRoot : IByteArraySerializable, IDiagnosticTraceable
    {
        private uint _attrType;
        private AttributeCollationRule _collationRule;
        private uint _indexAllocationEntrySize;
        private byte _rawClustersPerIndexRecord;

        public const int HeaderOffset = 0x10;

        public uint AttributeType
        {
            get { return _attrType; }
            set { _attrType = value; }
        }

        public AttributeCollationRule CollationRule
        {
            get { return _collationRule; }
            set { _collationRule = value; }
        }

        public uint IndexAllocationSize
        {
            get { return _indexAllocationEntrySize; }
            set { _indexAllocationEntrySize = value; }
        }

        public byte RawClustersPerIndexRecord
        {
            get { return _rawClustersPerIndexRecord; }
            set { _rawClustersPerIndexRecord = value; }
        }

        public IComparer<byte[]> GetCollator(UpperCase upCase)
        {
            switch (_collationRule)
            {
                case AttributeCollationRule.Filename:
                    return new FileNameComparer(upCase);
                case AttributeCollationRule.SecurityHash:
                    return new SecurityHashComparer();
                case AttributeCollationRule.UnsignedLong:
                    return new UnsignedLongComparer();
                case AttributeCollationRule.MultipleUnsignedLongs:
                    return new MultipleUnsignedLongComparer();
                case AttributeCollationRule.Sid:
                    return new SidComparer();
                default:
                    throw new NotImplementedException();
            }
        }

        #region IByteArraySerializable Members

        public int ReadFrom(byte[] buffer, int offset)
        {
            _attrType = Utilities.ToUInt32LittleEndian(buffer, 0x00);
            _collationRule = (AttributeCollationRule)Utilities.ToUInt32LittleEndian(buffer, 0x04);
            _indexAllocationEntrySize = Utilities.ToUInt32LittleEndian(buffer, 0x08);
            _rawClustersPerIndexRecord = buffer[0x0C];
            return 16;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            Utilities.WriteBytesLittleEndian(_attrType, buffer, 0);
            Utilities.WriteBytesLittleEndian((uint)_collationRule, buffer, 0x04);
            Utilities.WriteBytesLittleEndian(_indexAllocationEntrySize, buffer, 0x08);
            Utilities.WriteBytesLittleEndian(_rawClustersPerIndexRecord, buffer, 0x0C);
        }

        public int Size
        {
            get { return 16; }
        }

        #endregion

        #region IDiagnosticTracer Members

        public void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "                Attr Type: " + _attrType);
            writer.WriteLine(indent + "           Collation Rule: " + _collationRule);
            writer.WriteLine(indent + "         Index Alloc Size: " + _indexAllocationEntrySize);
            writer.WriteLine(indent + "  Raw Clusters Per Record: " + _rawClustersPerIndexRecord);
        }

        #endregion

        #region Collators
        private sealed class SecurityHashComparer : IComparer<byte[]>
        {
            public int Compare(byte[] x, byte[] y)
            {
                if (x == null && y == null)
                {
                    return 0;
                }
                else if (y == null)
                {
                    return -1;
                }
                else if (x == null)
                {
                    return 1;
                }


                uint xHash = Utilities.ToUInt32LittleEndian(x, 0);
                uint yHash = Utilities.ToUInt32LittleEndian(y, 0);

                if (xHash < yHash)
                {
                    return -1;
                }
                else if (xHash > yHash)
                {
                    return 1;
                }

                uint xId = Utilities.ToUInt32LittleEndian(x, 4);
                uint yId = Utilities.ToUInt32LittleEndian(y, 4);
                if (xId < yId)
                {
                    return -1;
                }
                else if (xId > yId)
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
        }

        private sealed class UnsignedLongComparer : IComparer<byte[]>
        {
            public int Compare(byte[] x, byte[] y)
            {
                if (x == null && y == null)
                {
                    return 0;
                }
                else if (y == null)
                {
                    return -1;
                }
                else if (x == null)
                {
                    return 1;
                }

                uint xVal = Utilities.ToUInt32LittleEndian(x, 0);
                uint yVal = Utilities.ToUInt32LittleEndian(y, 0);

                if (xVal < yVal)
                {
                    return -1;
                }
                else if (xVal > yVal)
                {
                    return 1;
                }
                return 0;
            }
        }

        private sealed class MultipleUnsignedLongComparer : IComparer<byte[]>
        {
            public int Compare(byte[] x, byte[] y)
            {
                for (int i = 0; i < x.Length / 4; ++i)
                {
                    if (x == null && y == null)
                    {
                        return 0;
                    }
                    else if (y == null)
                    {
                        return -1;
                    }
                    else if (x == null)
                    {
                        return 1;
                    }

                    uint xVal = Utilities.ToUInt32LittleEndian(x, i * 4);
                    uint yVal = Utilities.ToUInt32LittleEndian(y, i * 4);

                    if (xVal < yVal)
                    {
                        return -1;
                    }
                    else if (xVal > yVal)
                    {
                        return 1;
                    }
                }
                return 0;
            }
        }

        private sealed class FileNameComparer : IComparer<byte[]>
        {
            private UpperCase _stringComparer;

            public FileNameComparer(UpperCase upCase)
            {
                _stringComparer = upCase;
            }

            public int Compare(byte[] x, byte[] y)
            {
                if (x == null && y == null)
                {
                    return 0;
                }
                else if (y == null)
                {
                    return -1;
                }
                else if (x == null)
                {
                    return 1;
                }

                byte xFnLen = x[0x40];
                byte yFnLen = y[0x40];

                return _stringComparer.Compare(x, 0x42, xFnLen * 2, y, 0x42, yFnLen * 2);
            }
        }

        private sealed class SidComparer : IComparer<byte[]>
        {
            public int Compare(byte[] x, byte[] y)
            {
                if (x == null && y == null)
                {
                    return 0;
                }
                else if (y == null)
                {
                    return -1;
                }
                else if (x == null)
                {
                    return 1;
                }

                int toComp = Math.Min(x.Length, y.Length);
                for (int i = 0; i < toComp; ++i)
                {
                    int val = ((int)x[i]) - ((int)y[i]);
                    if (val != 0)
                    {
                        return val;
                    }
                }

                if (x.Length < y.Length)
                {
                    return -1;
                }
                else if (x.Length > y.Length)
                {
                    return 1;
                }
                return 0;
            }
        }

        #endregion
    }
}
