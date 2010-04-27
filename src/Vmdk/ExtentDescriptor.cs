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
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace DiscUtils.Vmdk
{
    internal enum ExtentAccess
    {
        None = 0,
        ReadOnly = 1,
        ReadWrite = 2
    }

    internal enum ExtentType
    {
        Flat = 0,
        Sparse = 1,
        Zero = 2,
        Vmfs = 3,
        VmfsSparse = 4,
        VmfsRdm = 5,
        VmfsRaw = 6
    }

    internal class ExtentDescriptor
    {
        private ExtentAccess _access;
        private long _sizeInSectors;
        private ExtentType _type;
        private string _fileName;
        private long _offset;

        public ExtentDescriptor()
        {
        }

        public ExtentDescriptor(ExtentAccess access, long size, ExtentType type, string fileName, long offset)
        {
            _access = access;
            _sizeInSectors = size;
            _type = type;
            _fileName = fileName;
            _offset = offset;
        }

        public ExtentAccess Access
        {
            get { return _access; }
        }

        public long SizeInSectors
        {
            get { return _sizeInSectors; }
        }

        public ExtentType Type
        {
            get { return _type; }
        }

        public string FileName
        {
            get { return _fileName; }
        }

        public long Offset
        {
            get { return _offset; }
        }

        public static ExtentDescriptor Parse(string descriptor)
        {
            string[] elems = SplitQuotedString(descriptor);
            if (elems.Length < 4)
            {
                throw new IOException(string.Format(CultureInfo.InvariantCulture, "Invalid extent descriptor: {0}", descriptor));
            }

            ExtentDescriptor result = new ExtentDescriptor();

            result._access = ParseAccess(elems[0]);
            result._sizeInSectors = long.Parse(elems[1], CultureInfo.InvariantCulture);
            result._type = ParseType(elems[2]);
            result._fileName = elems[3].Trim('\"');
            if (elems.Length > 4)
            {
                result._offset = long.Parse(elems[4], CultureInfo.InvariantCulture);
            }

            return result;
        }

        public static ExtentAccess ParseAccess(string access)
        {
            if (access == "NOACCESS")
            {
                return ExtentAccess.None;
            }
            else if (access == "RDONLY")
            {
                return ExtentAccess.ReadOnly;
            }
            else if (access == "RW")
            {
                return ExtentAccess.ReadWrite;
            }
            else
            {
                throw new ArgumentException("Unknown access type", "access");
            }
        }

        public static string FormatAccess(ExtentAccess access)
        {
            switch (access)
            {
                case ExtentAccess.None:
                    return "NOACCESS";
                case ExtentAccess.ReadOnly:
                    return "RDONLY";
                case ExtentAccess.ReadWrite:
                    return "RW";
                default:
                    throw new ArgumentException("Unknown access type", "access");
            }
        }

        public static ExtentType ParseType(string type)
        {
            if (type == "FLAT")
            {
                return ExtentType.Flat;
            }
            else if (type == "SPARSE")
            {
                return ExtentType.Sparse;
            }
            else if (type == "ZERO")
            {
                return ExtentType.Zero;
            }
            else if (type == "VMFS")
            {
                return ExtentType.Vmfs;
            }
            else if (type == "VMFSSPARSE")
            {
                return ExtentType.VmfsSparse;
            }
            else if (type == "VMFSRDM")
            {
                return ExtentType.VmfsRdm;
            }
            else if (type == "VMFSRAW")
            {
                return ExtentType.VmfsRaw;
            }
            else
            {
                throw new ArgumentException("Unknown extent type", "type");
            }
        }

        public static string FormatExtentType(ExtentType type)
        {
            switch (type)
            {
                case ExtentType.Flat:
                    return "FLAT";
                case ExtentType.Sparse:
                    return "SPARSE";
                case ExtentType.Zero:
                    return "ZERO";
                case ExtentType.Vmfs:
                    return "VMFS";
                case ExtentType.VmfsSparse:
                    return "VMFSSPARSE";
                case ExtentType.VmfsRdm:
                    return "VMFSRDM";
                case ExtentType.VmfsRaw:
                    return "VMFSRAW";
                default:
                    throw new ArgumentException("Unknown extent type", "type");
            }
        }

        private static string[] SplitQuotedString(string source)
        {
            List<string> result = new List<string>();

            int idx = 0;
            while (idx < source.Length)
            {
                // Skip spaces
                while (source[idx] == ' ' && idx < source.Length)
                {
                    idx++;
                }

                if (source[idx] == '"')
                {
                    // A quoted value, find end of quotes...
                    int start = idx;
                    idx++;
                    while (idx < source.Length && source[idx] != '"')
                    {
                        idx++;
                    }

                    result.Add(source.Substring(start, idx - start + 1));
                }
                else
                {
                    // An unquoted value, find end of value
                    int start = idx;
                    idx++;
                    while (idx < source.Length && source[idx] != ' ')
                    {
                        idx++;
                    }

                    result.Add(source.Substring(start, idx - start));
                }

                idx++;
            }

            return result.ToArray();
        }



        public override string ToString()
        {
            string basic = FormatAccess(_access) + " " + _sizeInSectors + " " + FormatExtentType(_type) + " \"" + _fileName + "\"";
            if (_type != ExtentType.Sparse && _type != ExtentType.VmfsSparse && _type != ExtentType.Zero)
            {
                return basic + " " + _offset;
            }
            return basic;
        }
    }
}
