// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiscUtils.Streams;

namespace DiscUtils.Udf
{
    /// <summary>
    /// Builds a read-only UDF 2.01 image without copying file contents.
    /// </summary>
    /// <remarks>
    /// The image uses one physical partition and 2048-byte logical blocks.
    /// Files are limited to 234 allocation descriptors (approximately 234 GiB).
    /// Call <see cref="StreamBuilder.Build()"/> to obtain a seekable virtual image;
    /// the overloads taking an output file or stream materialize the entire image.
    /// </remarks>
    public sealed class UdfBuilder : StreamBuilder
    {
        private const int BlockSize = 2048;
        private const uint PartitionStart = 291;
        private const long MaximumExtentLength = (1L << 30) - BlockSize;
        private const long MaximumFileLength = 234 * MaximumExtentLength;
        private readonly Entry _root = new Entry(string.Empty, null, null, 0);
        private string _volumeIdentifier = "DISC";
        private string _volumeSetIdentifier = Guid.NewGuid().ToString("N");

        /// <summary>Gets or sets the volume identifier.</summary>
        public string VolumeIdentifier
        {
            get { return _volumeIdentifier; }
            set
            {
                UdfImageWriter.EncodeDString(value, 32);
                _volumeIdentifier = value;
            }
        }

        /// <summary>Gets or sets a volume-set identifier, unique to this image and stable across reads.</summary>
        /// <remarks>The first sixteen characters must be hexadecimal digits, as required by UDF 2.01.</remarks>
        public string VolumeSetIdentifier
        {
            get { return _volumeSetIdentifier; }
            set
            {
                UdfImageWriter.EncodeDString(value, 128);
                if (
                    value.Length < 16
                    || value.Take(16).Any(character => !Uri.IsHexDigit(character))
                )
                {
                    throw new ArgumentException(
                        "A volume-set identifier must begin with sixteen hexadecimal digits.",
                        nameof(value)
                    );
                }

                _volumeSetIdentifier = value;
            }
        }

        /// <summary>Gets or sets the UTC timestamp used for the image metadata.</summary>
        public DateTime RecordingTime { get; set; } = DateTime.UtcNow;

        /// <summary>Adds an empty directory, creating its parents as necessary.</summary>
        /// <param name="name">The relative path inside the image.</param>
        public void AddDirectory(string name)
        {
            GetDirectory(SplitPath(name));
        }

        /// <summary>Adds a file whose contents are opened only when the image is read.</summary>
        /// <param name="name">The relative path inside the image.</param>
        /// <param name="sourcePath">The existing file to read.</param>
        public void AddFile(string name, string sourcePath)
        {
            var file = new FileInfo(sourcePath);
            long length = file.Length;
            DateTime lastWrite = file.LastWriteTimeUtc;
            AddFile(
                name,
                length,
                () =>
                {
                    var current = new FileInfo(file.FullName);
                    if (current.Length != length || current.LastWriteTimeUtc != lastWrite)
                    {
                        throw new IOException(
                            "A source file has changed since the image was built."
                        );
                    }

                    return new FileStream(
                        file.FullName,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read
                    );
                }
            );
        }

        /// <summary>Adds a byte array as a file.</summary>
        /// <param name="name">The relative path inside the image.</param>
        /// <param name="content">The file contents, retained by reference.</param>
        public void AddFile(string name, byte[] content)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            AddFile(name, content.LongLength, () => new MemoryStream(content, false));
        }

        /// <summary>Adds a lazily opened file with a known length.</summary>
        /// <param name="name">The relative path inside the image.</param>
        /// <param name="length">The length of the source in bytes.</param>
        /// <param name="openStream">Creates a new readable, seekable stream. The image owns each returned stream.</param>
        public void AddFile(string name, long length, Func<Stream> openStream)
        {
            if (openStream == null)
            {
                throw new ArgumentNullException(nameof(openStream));
            }

            if (length < 0 || length > MaximumFileLength)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            string[] parts = SplitPath(name);
            Entry parent = GetDirectory(parts.Take(parts.Length - 1));
            string leaf = parts[parts.Length - 1];
            if (parent.Children.ContainsKey(leaf))
            {
                throw new IOException("An entry already exists at this path.");
            }

            parent.Children.Add(leaf, new Entry(leaf, parent, openStream, length));
        }

        /// <inheritdoc/>
        protected override List<BuilderExtent> FixExtents(out long totalLength)
        {
            var entries = new List<Entry>();
            Flatten(_root, entries);
            uint nextBlock = 2; // File set descriptor and its terminator.
            ulong nextId = 16;
            foreach (Entry entry in entries)
            {
                entry.Icb = nextBlock++;
                entry.UniqueId = entry == _root ? 0 : nextId++;
                if (entry.IsDirectory)
                {
                    entry.Length =
                        40
                        + entry.Children.Values.Sum(child =>
                            (38 + UdfImageWriter.EncodeName(child.Name).Length + 3) / 4 * 4
                        );
                }
            }

            foreach (
                Entry entry in entries
                    .Where(entry => entry.IsDirectory)
                    .Concat(entries.Where(entry => !entry.IsDirectory))
            )
            {
                entry.DataBlock = nextBlock;
                nextBlock = checked(nextBlock + Blocks(entry.Length));
            }

            uint partitionLength = nextBlock;
            uint lastBlock = checked(PartitionStart + partitionLength + 256);
            totalLength = ((long)lastBlock + 1) * BlockSize;
            var result = new List<BuilderExtent>();
            var writer = new UdfImageWriter(_volumeIdentifier, _volumeSetIdentifier, RecordingTime);

            AddBytes(result, 16, UdfImageWriter.VolumeRecognition());
            AddBytes(result, 256, writer.Anchor(256));
            for (uint sequenceStart = 257; sequenceStart <= 273; sequenceStart += 16)
            {
                AddBytes(result, sequenceStart, writer.PrimaryVolume(sequenceStart));
                AddBytes(result, sequenceStart + 1, writer.ImplementationVolume(sequenceStart + 1));
                AddBytes(
                    result,
                    sequenceStart + 2,
                    writer.Partition(sequenceStart + 2, PartitionStart, partitionLength)
                );
                AddBytes(result, sequenceStart + 3, writer.LogicalVolume(sequenceStart + 3));
                AddBytes(result, sequenceStart + 4, writer.UnallocatedSpace(sequenceStart + 4));
                AddBytes(result, sequenceStart + 5, UdfImageWriter.Terminator(sequenceStart + 5));
            }

            AddBytes(
                result,
                289,
                writer.Integrity(
                    partitionLength,
                    nextId,
                    (uint)entries.Count(entry => !entry.IsDirectory),
                    (uint)entries.Count(entry => entry.IsDirectory)
                )
            );
            AddBytes(result, 290, UdfImageWriter.Terminator(290));
            AddBytes(result, PartitionStart, writer.FileSet(_root.Icb));
            AddBytes(result, PartitionStart + 1, UdfImageWriter.Terminator(1));

            foreach (Entry entry in entries)
            {
                ushort links = entry.IsDirectory
                    ? checked((ushort)(1 + entry.Children.Values.Count(child => child.IsDirectory)))
                    : (ushort)1;
                AddBytes(
                    result,
                    PartitionStart + entry.Icb,
                    writer.FileEntry(
                        entry.Icb,
                        entry.DataBlock,
                        entry.Length,
                        entry.IsDirectory,
                        links,
                        entry.UniqueId
                    )
                );
                if (entry.IsDirectory)
                {
                    var bytes = new byte[checked((int)entry.Length)];
                    int position = writer.FileIdentifier(
                        bytes,
                        0,
                        entry.DataBlock,
                        string.Empty,
                        (entry.Parent ?? entry).Icb,
                        (entry.Parent ?? entry).UniqueId,
                        true,
                        true
                    );
                    foreach (Entry child in entry.Children.Values)
                    {
                        position += writer.FileIdentifier(
                            bytes,
                            position,
                            entry.DataBlock + (uint)(position / BlockSize),
                            child.Name,
                            child.Icb,
                            child.UniqueId,
                            child.IsDirectory,
                            false
                        );
                    }

                    AddBytes(result, PartitionStart + entry.DataBlock, bytes);
                }
                else if (entry.Length > 0)
                {
                    result.Add(
                        new SourceExtent(
                            ((long)PartitionStart + entry.DataBlock) * BlockSize,
                            entry.Length,
                            entry.OpenStream
                        )
                    );
                }
            }

            AddBytes(result, lastBlock - 256, writer.Anchor(lastBlock - 256));
            AddBytes(result, lastBlock, writer.Anchor(lastBlock));
            return result;
        }

        private static uint Blocks(long length)
        {
            return checked((uint)((length + BlockSize - 1) / BlockSize));
        }

        private static void AddBytes(List<BuilderExtent> extents, uint block, byte[] bytes)
        {
            extents.Add(new BuilderBytesExtent((long)block * BlockSize, bytes));
        }

        private static string[] SplitPath(string name)
        {
            if (string.IsNullOrEmpty(name) || name[0] == '/' || name[0] == '\\')
            {
                throw new ArgumentException(
                    "An image path must be nonempty and relative.",
                    nameof(name)
                );
            }

            string[] parts = name.Replace('\\', '/').Split('/');
            foreach (string part in parts)
            {
                if (part.Length == 0 || part == "." || part == ".." || part.IndexOf('\0') >= 0)
                {
                    throw new ArgumentException("Invalid image path component.", nameof(name));
                }

                UdfImageWriter.EncodeName(part);
            }

            return parts;
        }

        private Entry GetDirectory(IEnumerable<string> parts)
        {
            Entry parent = _root;
            foreach (string part in parts)
            {
                Entry child;
                if (!parent.Children.TryGetValue(part, out child))
                {
                    child = new Entry(part, parent, null, 0);
                    parent.Children.Add(part, child);
                }

                if (!child.IsDirectory)
                {
                    throw new IOException("A file already exists at this path.");
                }

                parent = child;
            }

            return parent;
        }

        private static void Flatten(Entry entry, List<Entry> entries)
        {
            entries.Add(entry);
            foreach (Entry child in entry.Children.Values)
            {
                Flatten(child, entries);
            }
        }

        private sealed class Entry
        {
            public Entry(string name, Entry parent, Func<Stream> openStream, long length)
            {
                Name = name;
                Parent = parent;
                OpenStream = openStream;
                Length = length;
            }

            public string Name { get; }
            public Entry Parent { get; }
            public Func<Stream> OpenStream { get; }
            public bool IsDirectory
            {
                get { return OpenStream == null; }
            }
            public SortedDictionary<string, Entry> Children { get; } =
                new SortedDictionary<string, Entry>(StringComparer.Ordinal);
            public long Length { get; set; }
            public uint Icb { get; set; }
            public uint DataBlock { get; set; }
            public ulong UniqueId { get; set; }
        }

        private sealed class SourceExtent : BuilderExtent
        {
            private readonly Func<Stream> _open;
            private Stream _stream;

            public SourceExtent(long start, long length, Func<Stream> open)
                : base(start, length)
            {
                _open = open;
            }

            public override void PrepareForRead()
            {
                _stream = _open();
                if (!_stream.CanRead || !_stream.CanSeek || _stream.Length != Length)
                {
                    DisposeReadState();
                    throw new IOException(
                        "The source must be readable, seekable, and retain its declared length."
                    );
                }
            }

            public override int Read(long diskOffset, byte[] block, int offset, int count)
            {
                _stream.Position = diskOffset - Start;
                count = (int)Math.Min(count, Length - _stream.Position);
                int read = _stream.Read(block, offset, count);
                if (read == 0 && count != 0)
                {
                    throw new EndOfStreamException(
                        "A source file was truncated while reading the image."
                    );
                }

                return read;
            }

            public override void DisposeReadState()
            {
                if (_stream != null)
                {
                    _stream.Dispose();
                    _stream = null;
                }
            }

            public override void Dispose()
            {
                DisposeReadState();
            }
        }
    }
}
