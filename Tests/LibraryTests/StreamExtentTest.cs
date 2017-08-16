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
using System.Collections.Generic;
using DiscUtils;
using DiscUtils.Streams;
using Xunit;

namespace LibraryTests
{
    public class StreamExtentTest
    {
        [Fact]
        public void TestIntersect1()
        {
            StreamExtent[] s1 = new StreamExtent[] {
                new StreamExtent(0,4)};
            StreamExtent[] s2 = new StreamExtent[] {
                new StreamExtent(4,8)};
            StreamExtent[] r = new StreamExtent[] { };

            Compare(r, StreamExtent.Intersect(s1, s2));
        }

        [Fact]
        public void TestIntersect2()
        {
            StreamExtent[] s1 = new StreamExtent[] {
                new StreamExtent(0,4)};
            StreamExtent[] s2 = new StreamExtent[] {
                new StreamExtent(3,8)};
            StreamExtent[] r = new StreamExtent[] {
                new StreamExtent(3,1)};

            Compare(r, StreamExtent.Intersect(s1, s2));
        }

        [Fact]
        public void TestIntersect3()
        {
            StreamExtent[] s1 = new StreamExtent[] {
                new StreamExtent(0,4),
                new StreamExtent(10, 10)};
            StreamExtent[] s2 = new StreamExtent[] {
                new StreamExtent(3,8)};
            StreamExtent[] r = new StreamExtent[] {
                new StreamExtent(3,1),
                new StreamExtent(10,1)};

            Compare(r, StreamExtent.Intersect(s1, s2));
        }

        [Fact]
        public void TestIntersect4()
        {
            StreamExtent[] s1 = new StreamExtent[] {
                new StreamExtent(0,4)};
            StreamExtent[] s2 = new StreamExtent[] {
                new StreamExtent(3,8)};
            StreamExtent[] s3 = new StreamExtent[] {
                new StreamExtent(10,10)};
            StreamExtent[] r = new StreamExtent[] {
                };

            Compare(r, StreamExtent.Intersect(s1, s2, s3));
        }

        [Fact]
        public void TestIntersect5()
        {
            StreamExtent[] s1 = new StreamExtent[] {
                new StreamExtent(0,10)};
            StreamExtent[] s2 = new StreamExtent[] {
                new StreamExtent(3,5)};
            StreamExtent[] r = new StreamExtent[] {
                new StreamExtent(3,5)};

            Compare(r, StreamExtent.Intersect(s1, s2));
        }

        [Fact]
        public void TestUnion1()
        {
            StreamExtent[] s1 = new StreamExtent[] {
                new StreamExtent(0,4)};
            StreamExtent[] s2 = new StreamExtent[] {
                new StreamExtent(4,8)};
            StreamExtent[] r = new StreamExtent[] {
                new StreamExtent(0,12)};

            Compare(r, StreamExtent.Union(s1, s2));
        }

        [Fact]
        public void TestUnion2()
        {
            StreamExtent[] s1 = new StreamExtent[] {
                new StreamExtent(0,4)};
            StreamExtent[] s2 = new StreamExtent[] {
                new StreamExtent(5,8)};
            StreamExtent[] r = new StreamExtent[] {
                new StreamExtent(0,4),
                new StreamExtent(5,8)};

            Compare(r, StreamExtent.Union(s1, s2));
        }

        [Fact]
        public void TestUnion3()
        {
            StreamExtent[] s1 = new StreamExtent[] {
                new StreamExtent(0,4)};
            StreamExtent[] s2 = new StreamExtent[] {
                new StreamExtent(2,8)};
            StreamExtent[] r = new StreamExtent[] {
                new StreamExtent(0,10)};

            Compare(r, StreamExtent.Union(s1, s2));
        }

        [Fact]
        public void TestUnion4()
        {
            StreamExtent[] s1 = new StreamExtent[] {
                new StreamExtent(0,4),
                new StreamExtent(4,4)};
            StreamExtent[] r = new StreamExtent[] {
                new StreamExtent(0,8)};

            Compare(r, StreamExtent.Union(s1));
        }

        [Fact]
        public void TestUnion5()
        {
            StreamExtent[] r = new StreamExtent[] { };

            Compare(r, StreamExtent.Union());
        }

        [Fact]
        public void TestBlockCount()
        {
            StreamExtent[] s = new StreamExtent[] {
                new StreamExtent(0,8),
                new StreamExtent(11, 4)
            };

            Assert.Equal(2, StreamExtent.BlockCount(s, 10));

            s = new StreamExtent[] {
                new StreamExtent(0,8),
                new StreamExtent(9, 8)
            };

            Assert.Equal(2, StreamExtent.BlockCount(s, 10));

            s = new StreamExtent[] {
                new StreamExtent(3, 4),
                new StreamExtent(19, 4),
                new StreamExtent(44, 4)
            };

            Assert.Equal(4, StreamExtent.BlockCount(s, 10));
        }

        [Fact]
        public void TestBlocks()
        {
            StreamExtent[] s = new StreamExtent[] {
                new StreamExtent(0,8),
                new StreamExtent(11, 4)
            };

            List<Range<long,long>> ranges = new List<Range<long,long>>(StreamExtent.Blocks(s, 10));

            Assert.Equal(1, ranges.Count);
            Assert.Equal(0, ranges[0].Offset);
            Assert.Equal(2, ranges[0].Count);

            s = new StreamExtent[] {
                new StreamExtent(0,8),
                new StreamExtent(9, 8)
            };

            ranges = new List<Range<long, long>>(StreamExtent.Blocks(s, 10));

            Assert.Equal(1, ranges.Count);
            Assert.Equal(0, ranges[0].Offset);
            Assert.Equal(2, ranges[0].Count);

            s = new StreamExtent[] {
                new StreamExtent(3, 4),
                new StreamExtent(19, 4),
                new StreamExtent(44, 4)
            };

            ranges = new List<Range<long, long>>(StreamExtent.Blocks(s, 10));

            Assert.Equal(2, ranges.Count);
            Assert.Equal(0, ranges[0].Offset);
            Assert.Equal(3, ranges[0].Count);
            Assert.Equal(4, ranges[1].Offset);
            Assert.Equal(1, ranges[1].Count);
        }


        public void Compare(IEnumerable<StreamExtent> expected, IEnumerable<StreamExtent> actual)
        {
            List<StreamExtent> eList = new List<StreamExtent>(expected);
            List<StreamExtent> aList = new List<StreamExtent>(actual);

            bool failed = false;
            int failedIndex = -1;
            if (eList.Count == aList.Count)
            {
                for (int i = 0; i < eList.Count; ++i)
                {
                    if (eList[i] != aList[i])
                    {
                        failed = true;
                        failedIndex = i;
                        break;
                    }
                }
            }
            else
            {
                failed = true;
            }

            if (failed)
            {
                string str = "Expected " + eList.Count + "(<";
                for (int i = 0; i < Math.Min(4, eList.Count); ++i)
                {
                    str += eList[i].ToString() + ",";
                }
                if (eList.Count > 4)
                {
                    str += "...";
                }
                str += ">)";

                str += ", actual " + aList.Count + "(<";
                for (int i = 0; i < Math.Min(4, aList.Count); ++i)
                {
                    str += aList[i].ToString() + ",";
                }
                if (aList.Count > 4)
                {
                    str += "...";
                }
                str += ">)";

                if (failedIndex != -1)
                {
                    str += " - different at index " + failedIndex;
                }

                Assert.True(false, str);
            }
        }
    }
}
