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

using System.IO;

namespace DiscUtils
{
    /// <summary>
    /// Base class for reading binary data from a stream.
    /// </summary>
    internal abstract class DataReader
    {
        protected Stream _stream;

        public DataReader(Stream stream)
        {
            _stream = stream;
        }

        public long Position
        {
            get { return _stream.Position; }
        }

        public long Length
        {
            get { return _stream.Length; }
        }

        public void Skip(int bytes)
        {
            ReadBytes(bytes);
        }

        public abstract ushort ReadUInt16();
        public abstract int ReadInt32();
        public abstract uint ReadUInt32();
        public abstract long ReadInt64();
        public abstract ulong ReadUInt64();
        public abstract byte[] ReadBytes(int count);
    }

    /// <summary>
    /// Class for reading little-endian data from a stream.
    /// </summary>
    internal class LittleEndianDataReader : DataReader
    {
        public LittleEndianDataReader(Stream stream)
            : base(stream)
        { }

        public override ushort ReadUInt16()
        {
            return Utilities.ToUInt16LittleEndian(Utilities.ReadFully(_stream, 2), 0);
        }

        public override int ReadInt32()
        {
            return Utilities.ToInt32LittleEndian(Utilities.ReadFully(_stream, 4), 0);
        }

        public override uint ReadUInt32()
        {
            return Utilities.ToUInt32LittleEndian(Utilities.ReadFully(_stream, 4), 0);
        }

        public override long ReadInt64()
        {
            return Utilities.ToInt64LittleEndian(Utilities.ReadFully(_stream, 8), 0);
        }

        public override ulong ReadUInt64()
        {
            return Utilities.ToUInt64LittleEndian(Utilities.ReadFully(_stream, 8), 0);
        }

        public override byte[] ReadBytes(int count)
        {
            return Utilities.ReadFully(_stream, count);
        }
    }

    internal class BigEndianDataReader : DataReader
    {
        public BigEndianDataReader(Stream stream)
            : base(stream)
        { }

        public override ushort ReadUInt16()
        {
            return Utilities.ToUInt16BigEndian(Utilities.ReadFully(_stream, 2), 0);
        }

        public override int ReadInt32()
        {
            return Utilities.ToInt32BigEndian(Utilities.ReadFully(_stream, 4), 0);
        }

        public override uint ReadUInt32()
        {
            return Utilities.ToUInt32BigEndian(Utilities.ReadFully(_stream, 4), 0);
        }

        public override long ReadInt64()
        {
            return Utilities.ToInt64BigEndian(Utilities.ReadFully(_stream, 8), 0);
        }

        public override ulong ReadUInt64()
        {
            return Utilities.ToUInt64BigEndian(Utilities.ReadFully(_stream, 8), 0);
        }

        public override byte[] ReadBytes(int count)
        {
            return Utilities.ReadFully(_stream, count);
        }
    }
}
