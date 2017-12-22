//
// Copyright (c) 2008-2011, Kenneth Bell
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
    public sealed class Nfs3FileTime
    {
        private const long TicksPerSec = 10 * 1000 * 1000; // 10 million ticks per sec
        private const long TicksPerNanoSec = 100; // 1 tick = 100 ns

        private readonly DateTime nfsEpoch = new DateTime(1970, 1, 1);
        private readonly uint _nseconds;

        private readonly uint _seconds;

        internal Nfs3FileTime(XdrDataReader reader)
        {
            _seconds = reader.ReadUInt32();
            _nseconds = reader.ReadUInt32();
        }

        public Nfs3FileTime(DateTime time)
        {
            long ticks = time.Ticks - nfsEpoch.Ticks;
            _seconds = (uint)(ticks / TicksPerSec);
            _nseconds = (uint)(ticks % TicksPerSec * TicksPerNanoSec);
        }

        public Nfs3FileTime(uint seconds, uint nseconds)
        {
            _seconds = seconds;
            _nseconds = nseconds;
        }

        public DateTime ToDateTime()
        {
            return new DateTime(_seconds * TicksPerSec + _nseconds / TicksPerNanoSec + nfsEpoch.Ticks);
        }

        internal void Write(XdrDataWriter writer)
        {
            writer.Write(_seconds);
            writer.Write(_nseconds);
        }

        public static Nfs3FileTime Precision
        {
            get
            {
                return new Nfs3FileTime(0, 1);
            }
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Nfs3FileTime);
        }

        public bool Equals(Nfs3FileTime other)
        {
            if (other == null)
            {
                return false;
            }

            return other._seconds == _seconds
                && other._nseconds == _nseconds;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_seconds, _nseconds);
        }

        public override string ToString()
        {
            return ToDateTime().ToString();
        }
    }
}