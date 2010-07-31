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

namespace DiscUtils
{
    /// <summary>
    /// Represents a range of bytes in a stream.
    /// </summary>
    /// <remarks>This is normally used to represent regions of a SparseStream that
    /// are actually stored in the underlying storage medium (rather than implied
    /// zero bytes).  Extents are stored as a zero-based byte offset (from the
    /// beginning of the stream), and a byte length</remarks>
    public sealed class StreamExtent : IEquatable<StreamExtent>
    {
        private long _start;
        private long _length;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="start">The start of the extent</param>
        /// <param name="length">The length of the extent</param>
        public StreamExtent(long start, long length)
        {
            _start = start;
            _length = length;
        }

        /// <summary>
        /// Gets the start of the extent (in bytes).
        /// </summary>
        public long Start
        {
            get { return _start; }
        }

        /// <summary>
        /// Gets the start of the extent (in bytes).
        /// </summary>
        public long Length
        {
            get { return _length; }
        }

        /// <summary>
        /// Calculates the union of a list of extents with another extent.
        /// </summary>
        /// <param name="extents">The list of extents</param>
        /// <param name="other">The other extent</param>
        /// <returns>The union of the extents.</returns>
        public static IEnumerable<StreamExtent> Union(IEnumerable<StreamExtent> extents, StreamExtent other)
        {
            List<StreamExtent> otherList = new List<StreamExtent>();
            otherList.Add(other);
            return Union(extents, otherList);
        }

        /// <summary>
        /// Calculates the union of the extents of multiple streams.
        /// </summary>
        /// <param name="streams">The stream extents</param>
        /// <returns>The union of the extents from multiple streams.</returns>
        /// <remarks>A typical use of this method is to calculate the combined set of
        /// stored extents from a number of overlayed sparse streams.</remarks>
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
                bool foundIntersection;
                do
                {
                    foundIntersection = false;
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

        /// <summary>
        /// Calculates the intersection of the extents of a stream with another extent.
        /// </summary>
        /// <param name="extents">The stream extents</param>
        /// <param name="other">The extent to intersect</param>
        /// <returns>The intersection of the extents.</returns>
        public static IEnumerable<StreamExtent> Intersect(IEnumerable<StreamExtent> extents, StreamExtent other)
        {
            List<StreamExtent> otherList = new List<StreamExtent>(1);
            otherList.Add(other);
            return Intersect(extents, otherList);
        }

        /// <summary>
        /// Calculates the intersection of the extents of multiple streams.
        /// </summary>
        /// <param name="streams">The stream extents</param>
        /// <returns>The intersection of the extents from multiple streams.</returns>
        /// <remarks>A typical use of this method is to calculate the extents in a
        /// region of a stream..</remarks>
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
                        extentEnd = long.MaxValue;
                        overlapsFound = 0;
                    }
                }
            }
        }

        /// <summary>
        /// Offsets the extents of a stream.
        /// </summary>
        /// <param name="stream">The stream extents</param>
        /// <param name="delta">The amount to offset the extents by</param>
        /// <returns>The stream extents, offset by delta.</returns>
        public static IEnumerable<StreamExtent> Offset(IEnumerable<StreamExtent> stream, long delta)
        {
            foreach (StreamExtent extent in stream)
            {
                yield return new StreamExtent(extent.Start + delta, extent.Length);
            }
        }

        /// <summary>
        /// Returns the number of blocks containing stream data.
        /// </summary>
        /// <param name="stream">The stream extents</param>
        /// <param name="blockSize">The size of each block</param>
        /// <returns>The number of blocks containing stream data</returns>
        /// <remarks>This method logically divides the stream into blocks of a specified
        /// size, then indicates how many of those blocks contain actual stream data.</remarks>
        public static long BlockCount(IEnumerable<StreamExtent> stream, long blockSize)
        {
            long totalBlocks = 0;
            long lastBlock = -1;

            foreach (var extent in stream)
            {
                if (extent.Length > 0)
                {
                    long extentStartBlock = extent.Start / blockSize;
                    long extentNextBlock = Utilities.Ceil(extent.Start + extent.Length, blockSize);

                    long extentNumBlocks = extentNextBlock - extentStartBlock;
                    if (extentStartBlock == lastBlock)
                    {
                        extentNumBlocks--;
                    }
                    lastBlock = extentNextBlock - 1;

                    totalBlocks += extentNumBlocks;
                }
            }

            return totalBlocks;
        }

        /// <summary>
        /// Returns all of the blocks containing stream data.
        /// </summary>
        /// <param name="stream">The stream extents</param>
        /// <param name="blockSize">The size of each block</param>
        /// <returns>Ranges of blocks, as block indexes</returns>
        /// <remarks>This method logically divides the stream into blocks of a specified
        /// size, then indicates ranges of blocks that contain stream data.</remarks>
        public static IEnumerable<Range<long, long>> Blocks(IEnumerable<StreamExtent> stream, long blockSize)
        {
            long? rangeStart = null;
            long rangeLength = 0;

            foreach (var extent in stream)
            {
                if (extent.Length > 0)
                {
                    long extentStartBlock = extent.Start / blockSize;
                    long extentNextBlock = Utilities.Ceil(extent.Start + extent.Length, blockSize);

                    if (rangeStart != null && extentStartBlock > rangeStart + rangeLength)
                    {
                        // This extent is non-contiguous (in terms of blocks), so write out the last range and start new
                        yield return new Range<long, long>((long)rangeStart, rangeLength);
                        rangeStart = extentStartBlock;
                    }
                    else if (rangeStart == null)
                    {
                        // First extent, so start first range
                        rangeStart = extentStartBlock;
                    }

                    // Set the length of the current range, based on the end of this extent
                    rangeLength = extentNextBlock - (long)rangeStart;
                }
            }

            // Final range (if any ranges at all) hasn't been returned yet, so do that now
            if (rangeStart != null)
            {
                yield return new Range<long, long>((long)rangeStart, rangeLength);
            }
        }

        /// <summary>
        /// Indicates if this StreamExtent is equal to another.
        /// </summary>
        /// <param name="other">The extent to compare</param>
        /// <returns><c>true</c> if the extents are equal, else <c>false</c></returns>
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

        /// <summary>
        /// Returns a string representation of the extent as [start:+length].
        /// </summary>
        /// <returns>The string representation</returns>
        public override string ToString()
        {
            return "[" + _start + ":+" + _length + "]";
        }

        /// <summary>
        /// Indicates if this stream extent is equal to another object.
        /// </summary>
        /// <param name="obj">The object to test</param>
        /// <returns><c>true</c> if <c>obj</c> is equivalent, else <c>false</c></returns>
        public override bool Equals(object obj)
        {
            return Equals(obj as StreamExtent);
        }

        /// <summary>
        /// Gets a hash code for this extent.
        /// </summary>
        /// <returns>The extent's hash code.</returns>
        public override int GetHashCode()
        {
            return _start.GetHashCode() ^ _length.GetHashCode();
        }

        /// <summary>
        /// The equality operator.
        /// </summary>
        /// <param name="a">The first extent to compare</param>
        /// <param name="b">The second extent to compare</param>
        /// <returns>Whether the two extents are equal</returns>
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

        /// <summary>
        /// The inequality operator.
        /// </summary>
        /// <param name="a">The first extent to compare</param>
        /// <param name="b">The second extent to compare</param>
        /// <returns>Whether the two extents are different</returns>
        public static bool operator !=(StreamExtent a, StreamExtent b)
        {
            return !(a == b);
        }
    }
}
