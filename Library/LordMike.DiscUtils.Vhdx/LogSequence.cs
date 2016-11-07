//
// Copyright (c) 2008-2013, Kenneth Bell
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

using System.Collections.Generic;

namespace DiscUtils.Vhdx
{
    internal sealed class LogSequence : List<LogEntry>
    {
        public LogEntry Head
        {
            get { return Count > 0 ? this[Count - 1] : null; }
        }

        public LogEntry Tail
        {
            get { return Count > 0 ? this[0] : null; }
        }

        public bool Contains(long position)
        {
            if (Count <= 0)
            {
                return false;
            }

            if (Head.Position >= Tail.Position)
            {
                return position >= Tail.Position && position < Head.Position + LogEntry.LogSectorSize;
            }
            return position >= Tail.Position || position < Head.Position + LogEntry.LogSectorSize;
        }

        internal bool HigherSequenceThan(LogSequence otherSequence)
        {
            ulong other = otherSequence.Count > 0 ? otherSequence.Head.SequenceNumber : 0;
            ulong self = Count > 0 ? Head.SequenceNumber : 0;

            return self > other;
        }
    }
}