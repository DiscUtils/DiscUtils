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

namespace DiscUtils.Nfs
{
    internal class Nfs3FileTime
    {
        private readonly DateTime NfsEpoch = new DateTime(1970, 1, 1);
        private const long TicksPerSec = 10 * 1000 * 1000;  //10 million ticks per sec
        private const long TicksPerNanoSec = 100; //1 tick = 100 ns

        private uint _seconds;
        private uint _nseconds;

        public Nfs3FileTime(XdrDataReader reader)
        {
            _seconds = reader.ReadUInt32();
            _nseconds = reader.ReadUInt32();
        }

        public Nfs3FileTime(DateTime time)
        {
            long ticks = time.Ticks - NfsEpoch.Ticks;
            _seconds = (uint)(ticks / TicksPerSec);
            _nseconds = (uint)((ticks % TicksPerSec) * TicksPerNanoSec);
        }

        //public Nfs3FileTime(TimeSpan timeSpan)
        //{
        //    long ticks = timeSpan.Ticks;
        //    _seconds = (uint)(ticks / TicksPerSec);
        //    _nseconds = (uint)((ticks % TicksPerSec) * TicksPerNanoSec);
        //}

        public DateTime ToDateTime()
        {
            return new DateTime((_seconds * TicksPerSec + (_nseconds / TicksPerNanoSec)) + NfsEpoch.Ticks);
        }

        //public TimeSpan ToTimeSpan()
        //{
        //    return new TimeSpan(_seconds * TicksPerSec + (_nseconds / TicksPerNanoSec));
        //}

        public void Write(XdrDataWriter writer)
        {
            writer.Write(_seconds);
            writer.Write(_nseconds);
        }
    }
}
