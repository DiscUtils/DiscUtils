// SPDX-License-Identifier: MIT
using System;
using System.IO;
using System.Linq;
using System.Text;
using DiscUtils.Streams;
using DiscUtils.Udf;
using Xunit;

namespace LibraryTests.Udf
{
    public class UdfBuilderTest
    {
        [Fact]
        public void BuildPreservesTreeNamesAndBytes()
        {
            var builder = new UdfBuilder { VolumeIdentifier = "TEST" };
            builder.AddDirectory("BDMV/EMPTY");
            builder.AddFile("BDMV/index.bdmv", new byte[] { 1, 2, 3, 4 });
            builder.AddFile("字幕/繁體中文.txt", Encoding.UTF8.GetBytes("字幕"));
            builder.AddFile("empty", new byte[0]);
            using (SparseStream image = builder.Build())
            using (var reader = new UdfReader(image))
            {
                Assert.Equal("TEST", reader.VolumeLabel);
                Assert.True(reader.DirectoryExists("BDMV/EMPTY".Replace('/', '\\')));
                using (
                    Stream file = reader.OpenFile(
                        "BDMV/index.bdmv".Replace('/', '\\'),
                        FileMode.Open
                    )
                )
                {
                    Assert.Equal(new byte[] { 1, 2, 3, 4 }, ReadAll(file));
                }

                using (
                    Stream file = reader.OpenFile(
                        "字幕/繁體中文.txt".Replace('/', '\\'),
                        FileMode.Open
                    )
                )
                {
                    Assert.Equal("字幕", Encoding.UTF8.GetString(ReadAll(file)));
                }

                Assert.Equal(0, reader.GetFileLength("empty"));
            }
        }

        [Fact]
        public void BuildDoesNotOpenPayloadAndSupportsLargeFileSeeks()
        {
            int opens = 0;
            long length = (81L << 30) + 123;
            var builder = new UdfBuilder();
            builder.AddFile(
                "BDMV/STREAM/00001.m2ts",
                length,
                () =>
                {
                    opens++;
                    return new PatternStream(length);
                }
            );
            using (SparseStream image = builder.Build())
            {
                Assert.Equal(0, opens);
                Assert.True(image.Length > length);
                using (var reader = new UdfReader(image))
                {
                    string path = "BDMV/STREAM/00001.m2ts".Replace('/', '\\');
                    Assert.Equal(length, reader.GetFileLength(path));
                    Assert.Equal(0, opens);
                    using (Stream file = reader.OpenFile(path, FileMode.Open))
                    {
                        foreach (
                            long offset in new long[] { 0, (1L << 30) - 2050, 5L << 30, length - 7 }
                        )
                        {
                            file.Position = offset;
                            var bytes = new byte[(int)Math.Min(4096, length - offset)];
                            Assert.Equal(bytes.Length, file.Read(bytes, 0, bytes.Length));
                            Assert.Equal(
                                Enumerable
                                    .Range(0, bytes.Length)
                                    .Select(i => PatternStream.At(offset + i)),
                                bytes
                            );
                        }

                        Assert.Equal(-1, file.ReadByte());
                    }
                }
            }
        }

        [Fact]
        public void DirectorySpansMultipleBlocks()
        {
            var builder = new UdfBuilder();
            for (int i = 0; i < 200; i++)
            {
                builder.AddFile(
                    "BDMV/PLAYLIST/" + i.ToString("00000") + ".mpls",
                    new byte[] { (byte)i }
                );
            }

            using (SparseStream image = builder.Build())
            using (var reader = new UdfReader(image))
            {
                string directory = "BDMV/PLAYLIST".Replace('/', '\\');
                Assert.Equal(200, reader.GetFiles(directory).Length);
                using (
                    Stream file = reader.OpenFile(directory + "\\" + "00199.mpls", FileMode.Open)
                )
                {
                    Assert.Equal(199, file.ReadByte());
                }
            }
        }

        [Fact]
        public void BuildsAreIndependentSnapshots()
        {
            var builder = new UdfBuilder();
            builder.AddFile("first", new byte[] { 1 });
            using (SparseStream first = builder.Build())
            {
                builder.AddFile("second", new byte[] { 2 });
                using (SparseStream second = builder.Build())
                using (var firstReader = new UdfReader(first))
                using (var secondReader = new UdfReader(second))
                {
                    Assert.False(firstReader.FileExists("second"));
                    Assert.True(secondReader.FileExists("second"));
                    using (Stream file = firstReader.OpenFile("first", FileMode.Open))
                    {
                        Assert.Equal(1, file.ReadByte());
                    }
                }
            }
        }

        [Fact]
        public void RejectsInvalidPathsAndUnsupportedLengths()
        {
            var builder = new UdfBuilder();
            foreach (
                string path in new[] { "", "/absolute", "../escape", "a/../b", "a//b", "a\0b" }
            )
            {
                Assert.Throws<ArgumentException>(() => builder.AddFile(path, new byte[0]));
            }

            Assert.Throws<ArgumentException>(() =>
                builder.AddFile(new string('a', 255), new byte[0])
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                builder.AddFile("large", 234 * ((1L << 30) - 2048) + 1, () => Stream.Null)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                builder.AddFile("negative", -1, () => Stream.Null)
            );
            builder.AddFile("existing", new byte[0]);
            Assert.Throws<IOException>(() => builder.AddDirectory("existing/child"));
            Assert.Throws<IOException>(() => builder.AddFile("existing", new byte[0]));
        }

        [Fact]
        public void RejectsChangedSourceLength()
        {
            var builder = new UdfBuilder();
            builder.AddFile("file", 10, () => new MemoryStream(new byte[9]));
            using (SparseStream image = builder.Build())
            using (var reader = new UdfReader(image))
            using (Stream file = reader.OpenFile("file", FileMode.Open))
            {
                Assert.Throws<IOException>(() => file.ReadByte());
            }
        }

        [Fact]
        public void SourceGrowthCannotOverwriteImagePadding()
        {
            var payload = new MemoryStream();
            payload.Write(new byte[] { 1, 2, 3, 4 }, 0, 4);
            var builder = new UdfBuilder();
            builder.AddFile("file", 4, () => payload);
            using (SparseStream image = builder.Build())
            using (var reader = new UdfReader(image))
            using (Stream file = reader.OpenFile("file", FileMode.Open))
            {
                Assert.Equal(1, file.ReadByte());
                long start = image.Position - 1;
                payload.Position = 4;
                payload.Write(new byte[] { 5, 6, 7, 8 }, 0, 4);
                image.Position = start;
                var bytes = new byte[8];
                Assert.Equal(8, image.Read(bytes, 0, bytes.Length));
                Assert.Equal(new byte[] { 1, 2, 3, 4, 0, 0, 0, 0 }, bytes);
            }
        }

        private static byte[] ReadAll(Stream stream)
        {
            using (var output = new MemoryStream())
            {
                stream.CopyTo(output);
                return output.ToArray();
            }
        }

        private sealed class PatternStream : Stream
        {
            private readonly long _length;

            public PatternStream(long length)
            {
                _length = length;
            }

            public static byte At(long position)
            {
                return (byte)((position >> 8) ^ position);
            }

            public override bool CanRead
            {
                get { return true; }
            }
            public override bool CanSeek
            {
                get { return true; }
            }
            public override bool CanWrite
            {
                get { return false; }
            }
            public override long Length
            {
                get { return _length; }
            }
            public override long Position { get; set; }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int available = (int)Math.Min(count, Length - Position);
                for (int i = 0; i < available; i++)
                {
                    buffer[offset + i] = At(Position++);
                }
                return available;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                Position =
                    (
                        origin == SeekOrigin.Begin ? 0
                        : origin == SeekOrigin.Current ? Position
                        : Length
                    ) + offset;
                return Position;
            }

            public override void Flush() { }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }
    }
}
