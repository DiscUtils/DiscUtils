//
// Copyright (c) 2008-2009, Kenneth Bell
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

namespace DiscUtils
{
    public class StreamExtent : IEquatable<StreamExtent>
    {
        private long _start;
        private long _length;

        public StreamExtent(long start, long length)
        {
            _start = start;
            _length = length;
        }

        public long Start
        {
            get { return _start; }
        }

        public long Length
        {
            get { return _length; }
        }

        public static IEnumerable<StreamExtent> Union(params IEnumerable<StreamExtent>[] streams)
        {
            long extentStart = long.MaxValue;
            long extentEnd = 0;

            // Initialize enumerations and find first stored byte position
            IEnumerator<StreamExtent>[] enums = new IEnumerator<StreamExtent>[streams.Length];
            bool[] streamsValid = new bool[streams.Length];
            int validStreamsRemaining = 0;
            for (int i = 0; i < streams.Length; ++i)
            {
                enums[i] = streams[i].GetEnumerator();
                streamsValid[i] = enums[i].MoveNext();
                if (streamsValid[i])
                {
                    ++validStreamsRemaining;
                    if (enums[i].Current.Start < extentStart)
                    {
                        extentStart = enums[i].Current.Start;
                        extentEnd = enums[i].Current.Start + enums[i].Current.Length;
                    }
                }
            }


            while (validStreamsRemaining > 0)
            {
                // Find the end of this extent
                bool foundIntersection = false;
                do
                {
                    validStreamsRemaining = 0;
                    for (int i = 0; i < streams.Length; ++i)
                    {
                        while (streamsValid[i] && enums[i].Current.Start + enums[i].Current.Length <= extentEnd)
                        {
                            streamsValid[i] = enums[i].MoveNext();
                        }

                        if (streamsValid[i])
                        {
                            ++validStreamsRemaining;
                        }

                        if (streamsValid[i] && enums[i].Current.Start <= extentEnd)
                        {
                            extentEnd = enums[i].Current.Start + enums[i].Current.Length;
                            foundIntersection = true;
                            streamsValid[i] = enums[i].MoveNext();
                        }
                    }
                } while (foundIntersection && validStreamsRemaining > 0);

                // Return the discovered extent
                yield return new StreamExtent(extentStart, extentEnd - extentStart);

                // Find the next extent start point
                extentStart = long.MaxValue;
                validStreamsRemaining = 0;
                for (int i = 0; i < streams.Length; ++i)
                {
                    if (streamsValid[i])
                    {
                        ++validStreamsRemaining;
                        if (enums[i].Current.Start < extentStart)
                        {
                            extentStart = enums[i].Current.Start;
                            extentEnd = enums[i].Current.Start + enums[i].Current.Length;
                        }
                    }
                }
            }
        }

        public static IEnumerable<StreamExtent> Intersect(params IEnumerable<StreamExtent>[] streams)
        {
            long extentStart = long.MinValue;
            long extentEnd = long.MaxValue;

            IEnumerator<StreamExtent>[] enums = new IEnumerator<StreamExtent>[streams.Length];
            bool[] streamsValid = new bool[streams.Length];
            for (int i = 0; i < streams.Length; ++i)
            {
                enums[i] = streams[i].GetEnumerator();
                if (!enums[i].MoveNext())
                {
                    // Gone past end of one stream (in practice was empty), so no intersections
                    yield break;
                }
            }

            int overlapsFound = 0;
            while (true)
            {
                // We keep cycling round the streams, until we get streams.Length continuous overlaps
                for (int i = 0; i < streams.Length; ++i)
                {
                    // Move stream on past all extents that are earlier than our candidate start point
                    while (enums[i].Current.Start + enums[i].Current.Length <= extentStart)
                    {
                        if (!enums[i].MoveNext())
                        {
                            // Gone past end of this stream, no more intersections possible
                            yield break;
                        }
                    }

                    // If this stream has an extent that spans over the candidate start point
                    if (enums[i].Current.Start <= extentStart)
                    {
                        extentEnd = Math.Min(extentEnd, enums[i].Current.Start + enums[i].Current.Length);
                        overlapsFound++;
                    }
                    else
                    {
                        extentStart = enums[i].Current.Start;
                        extentEnd = extentStart + enums[i].Current.Length;
                        overlapsFound = 1;
                    }

                    // We've just done a complete loop of all streams, they overlapped this start position
                    // and we've cut the extent's end down to the shortest run.
                    if (overlapsFound == streams.Length)
                    {
                        yield return new StreamExtent(extentStart, extentEnd - extentStart);
                        extentStart = extentEnd;
                    }
                }
            }
        }

        public bool Equals(StreamExtent other)
        {
            if (other == null)
            {
                return false;
            }
            else
            {
                return _start == other._start && _length == other._length;
            }
        }

        public override string ToString()
        {
            return "[" + _start + ":+" + _length + "]";
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StreamExtent);
        }

        public override int GetHashCode()
        {
            return _start.GetHashCode() ^ _length.GetHashCode();
        }

        public static bool operator ==(StreamExtent a, StreamExtent b)
        {
            if (Object.ReferenceEquals(a, null))
            {
                return Object.ReferenceEquals(b, null);
            }
            else
            {
                return a.Equals(b);
            }
        }

        public static bool operator !=(StreamExtent a, StreamExtent b)
        {
            return !(a == b);
        }
    }
}
