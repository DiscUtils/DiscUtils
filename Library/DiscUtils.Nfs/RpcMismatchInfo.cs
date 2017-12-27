//
// Copyright (c) 2008-2011, Kenneth Bell
// Copyright (c) 2017, Quamotion
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

namespace DiscUtils.Nfs
{
    public class RpcMismatchInfo
    {
        public uint High;
        public uint Low;

        public RpcMismatchInfo()
        {
        }

        public RpcMismatchInfo(XdrDataReader reader)
        {
            Low = reader.ReadUInt32();
            High = reader.ReadUInt32();
        }

        public void Write(XdrDataWriter writer)
        {
            writer.Write(Low);
            writer.Write(High);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RpcMismatchInfo);
        }

        public bool Equals(RpcMismatchInfo other)
        {
            if (other == null)
            {
                return false;
            }

            return other.High == High
                && other.Low == Low;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(High, Low);
        }
    }
}