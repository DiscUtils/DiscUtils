using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DiscUtils.Ntfs;

namespace LibraryTests.Ntfs.Journal;

/// <summary>
/// A compact description of what Windows' own journal replay made of a crashed volume, small enough to check in
/// next to the crashed volume: hashes of the used part of every MFT record and index buffer (LSN and update
/// sequence number left out, which every writer sets for itself), both bitmaps, and the file listing. Pages Windows
/// changed after its replay (mount-time housekeeping such as purging $Extend\$Deleted, which carries an LSN past
/// the crash's last record) are left out, with the bitmap bits they own.
/// </summary>
/// <remarks>Also compiled into the lab tool that produces the samples, so both compute the same lines.</remarks>
public static class WindowsReplayDigest
{
    /// <summary>
    /// The digest of <paramref name="windowsVolume"/> (Windows' replay of the crash), leaving out what Windows changed
    /// after its replay. <paramref name="replayedVolume"/> (DiscUtils' replay) adds the clusters those records own there.
    /// </summary>
    public static List<string> Create(Stream windowsVolume, Stream replayedVolume, ulong lastLsn)
    {
        var windows = new Volume(windowsVolume);
        var ours = new Volume(replayedVolume);

        // $Extend\$RmMetadata\$TxfLog (record 30) and System Volume Information: Windows writes into them at mount
        // and refreshes their timestamps without logging.
        var excludedRecords = new SortedSet<long> { 30 };
        // $Extend\$Quota too: Windows charges the files it creates at mount to their owner without logging.
        foreach (var path in new[] { @"\System Volume Information", @"\$Extend\$Quota" })
        {
            if (windows.RecordOf(path) is { } record)
            {
                excludedRecords.Add(record);
            }
        }

        for (long n = 0; n < windows.RecordCount; n++)
        {
            if (windows.RecordLsn(n) > lastLsn)
            {
                excludedRecords.Add(n);
            }
        }

        var excludedClusters = new SortedSet<long>();
        foreach (var volume in new[] { windows, ours })
        {
            foreach (var pair in volume.ClusterOwners())
            {
                if (excludedRecords.Contains(pair.Value))
                {
                    excludedClusters.Add(pair.Key);
                }
            }
        }

        var lines = new List<string>
        {
            $"excluded-records {string.Join(",", excludedRecords)}",
            $"excluded-clusters {string.Join(",", excludedClusters)}",
        };

        for (long n = 0; n < windows.RecordCount; n++)
        {
            if (!excludedRecords.Contains(n))
            {
                lines.Add($"mft {n} {windows.RecordHash(n)}");
            }
        }

        lines.Add($"mft-bitmap {windows.MftBitmapHash(excludedRecords)}");
        lines.Add($"cluster-bitmap {windows.ClusterBitmapHash(excludedClusters)}");
        foreach (var (directory, index, hash, lsn) in windows.IndexBuffers(excludedRecords))
        {
            if (lsn <= lastLsn)
            {
                lines.Add($"indx {directory}|{index} {hash}");
            }
        }

        lines.AddRange(windows.Listing().Select(l => "file " + l));
        return lines;
    }

    /// <summary>The lines of <paramref name="digest"/> that <paramref name="replayedVolume"/> does not match.</summary>
    public static List<string> Check(Stream replayedVolume, IReadOnlyList<string> digest)
    {
        var ours = new Volume(replayedVolume);
        var excludedRecords = Numbers(digest.First(l => l.StartsWith("excluded-records ", StringComparison.Ordinal)));
        var excludedClusters = Numbers(digest.First(l => l.StartsWith("excluded-clusters ", StringComparison.Ordinal)));
        var indx = ours.IndexBuffers(excludedRecords).ToDictionary(b => $"{b.Directory}|{b.Index}", b => b.Hash);
        var listing = new HashSet<string>(ours.Listing().Select(l => "file " + l));
        var expectedFiles = 0;
        var mismatches = new List<string>();
        foreach (var line in digest)
        {
            var parts = line.Split(' ');
            switch (parts[0])
            {
                case "mft":
                    var n = long.Parse(parts[1]);
                    if (n >= ours.RecordCount || ours.RecordHash(n) != parts[2])
                    {
                        mismatches.Add(line);
                    }

                    break;
                case "mft-bitmap":
                    if (ours.MftBitmapHash(excludedRecords) != parts[1])
                    {
                        mismatches.Add(line);
                    }

                    break;
                case "cluster-bitmap":
                    if (ours.ClusterBitmapHash(excludedClusters) != parts[1])
                    {
                        mismatches.Add(line);
                    }

                    break;
                case "indx":
                    var key = line.Substring(5, line.LastIndexOf(' ') - 5);
                    if (!indx.TryGetValue(key, out var hash) || hash != parts[parts.Length - 1])
                    {
                        mismatches.Add(line);
                    }

                    break;
                case "file":
                    expectedFiles++;
                    if (!listing.Contains(line))
                    {
                        mismatches.Add(line);
                    }

                    break;
            }
        }

        if (listing.Count != expectedFiles)
        {
            mismatches.Add($"listing has {listing.Count} entries, Windows {expectedFiles}");
        }

        return mismatches;
    }

    private static SortedSet<long> Numbers(string line)
    {
        var list = line.Substring(line.IndexOf(' ') + 1);
        return new SortedSet<long>(list.Length == 0 ? Enumerable.Empty<long>() : list.Split(',').Select(long.Parse));
    }

    private static string Hash(byte[] bytes, int offset, int count)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(bytes, offset, count)).Replace("-", "").Substring(0, 16);
    }

    /// <summary>A clean NTFS volume read through DiscUtils' public API (journal ignored).</summary>
    private sealed class Volume
    {
        private readonly NtfsFileSystem _fs;
        private readonly byte[] _mft;
        private readonly int _recordSize;
        private readonly int _indexSize;

        public Volume(Stream source)
        {
            // Read-only: DiscUtils updates access times as it reads, which would change what is being measured.
            source.Position = 0;
            var copy = new MemoryStream();
            source.CopyTo(copy);
            var stream = new MemoryStream(copy.ToArray(), writable: false);
            var boot = new byte[512];
            for (var done = 0; done < boot.Length;)
            {
                done += stream.Read(boot, done, boot.Length - done);
            }

            var cluster = BitConverter.ToUInt16(boot, 0x0B) * boot[0x0D];
            _recordSize = Size((sbyte)boot[0x40], cluster);
            _indexSize = Size((sbyte)boot[0x44], cluster);
            _fs = new NtfsFileSystem(stream, NtfsJournalMode.Ignore);
            _fs.NtfsOptions.HideHiddenFiles = false;
            _fs.NtfsOptions.HideSystemFiles = false;
            _mft = ReadAll("$MFT");
        }

        public long RecordCount => _mft.Length / _recordSize;

        public long? RecordOf(string path)
        {
            // Metafiles ($Extend\...) are hidden by default.
            _fs.NtfsOptions.HideMetafiles = false;
            try
            {
                return _fs.GetFileId(path) & 0x0000FFFFFFFFFFFF;
            }
            catch (IOException)
            {
                return null;
            }
            finally
            {
                _fs.NtfsOptions.HideMetafiles = true;
            }
        }

        public ulong RecordLsn(long n) => BitConverter.ToUInt64(_mft, (int)(n * _recordSize) + 8);

        public string RecordHash(long n) => UsedHash(_mft, (int)(n * _recordSize), _recordSize, isIndex: false).Hash;

        /// <remarks>
        /// The duplicated information (times, sizes) in entries of <paramref name="excludedRecords"/> is left out:
        /// Windows refreshes it at mount without logging when it writes into those files.
        /// </remarks>
        public IEnumerable<(string Directory, int Index, string Hash, ulong Lsn)> IndexBuffers(ISet<long> excludedRecords)
        {
            var directories = new[] { @"\" }.Concat(_fs.GetDirectories(@"\", "*", SearchOption.AllDirectories)).ToList();
            foreach (var directory in directories.Where(d => !d.Contains("System Volume Information")))
            {
                var name = (directory == @"\" ? @"\" : directory) + ":$I30:$INDEX_ALLOCATION";
                if (!_fs.FileExists(name))
                {
                    continue;
                }

                var bytes = ReadAll(name);
                for (var i = 0; (i + 1) * _indexSize <= bytes.Length; i++)
                {
                    var (hash, lsn) = UsedHash(bytes, i * _indexSize, _indexSize, isIndex: true, excludedRecords);
                    yield return (directory, i, hash, lsn);
                }
            }
        }

        public string MftBitmapHash(ISet<long> excludedRecords) => MaskedHash(ReadAll("$MFT::$BITMAP"), excludedRecords);

        public string ClusterBitmapHash(ISet<long> excludedClusters) => MaskedHash(ReadAll("$Bitmap"), excludedClusters);

        public Dictionary<long, long> ClusterOwners()
        {
            var owners = new Dictionary<long, long>();
            for (long n = 0; n < RecordCount; n++)
            {
                var r = Logical(_mft, (int)(n * _recordSize), _recordSize);
                if (BitConverter.ToUInt32(r, 0) != 0x454C4946 || (BitConverter.ToUInt16(r, 0x16) & 1) == 0)
                {
                    continue;
                }

                int a = BitConverter.ToUInt16(r, 0x14);
                while (a + 8 < r.Length && BitConverter.ToUInt32(r, a) != 0xFFFFFFFF)
                {
                    var length = (int)BitConverter.ToUInt32(r, a + 4);
                    if (length < 0x18 || a + length > r.Length)
                    {
                        break;
                    }

                    if (r[a + 8] != 0)
                    {
                        var p = a + BitConverter.ToUInt16(r, a + 0x20);
                        long lcn = 0;
                        while (p < a + length && r[p] != 0)
                        {
                            var lengthSize = r[p] & 15;
                            var offsetSize = r[p] >> 4;
                            long runLength = 0;
                            for (var i = lengthSize - 1; i >= 0; i--)
                            {
                                runLength = (runLength << 8) | r[p + 1 + i];
                            }

                            if (offsetSize > 0)
                            {
                                long delta = 0;
                                for (var i = offsetSize - 1; i >= 0; i--)
                                {
                                    delta = (delta << 8) | r[p + 1 + lengthSize + i];
                                }

                                if (offsetSize < 8 && (r[p + lengthSize + offsetSize] & 0x80) != 0)
                                {
                                    delta |= -1L << (offsetSize * 8);
                                }

                                lcn += delta;
                                for (long k = 0; k < runLength; k++)
                                {
                                    owners[lcn + k] = n;
                                }
                            }

                            p += 1 + lengthSize + offsetSize;
                        }
                    }

                    a += length;
                }
            }

            return owners;
        }

        /// <summary>Path, size, attributes, times, content hash and alternate streams of every file and directory.</summary>
        public List<string> Listing()
        {
            var lines = new List<string>();
            foreach (var entry in _fs.GetFileSystemEntries(@"\", "*", SearchOption.AllDirectories).ToList().OrderBy(e => e, StringComparer.Ordinal))
            {
                if (entry.Contains("System Volume Information"))
                {
                    continue;
                }

                var info = _fs.GetFileSystemInfo(entry);
                var isDirectory = _fs.DirectoryExists(entry);
                var line = $"{entry}|{(isDirectory ? "dir" : _fs.GetFileLength(entry).ToString())}|{(int)info.Attributes:x}|{info.CreationTimeUtc.Ticks}|{info.LastWriteTimeUtc.Ticks}";
                if (!isDirectory)
                {
                    line += "|" + ContentHash(entry);
                    foreach (var stream in _fs.GetAlternateDataStreams(entry).OrderBy(s => s, StringComparer.Ordinal))
                    {
                        line += $"|:{stream}={ContentHash(entry + ":" + stream)}";
                    }
                }

                lines.Add(line.Replace(' ', '_'));
            }

            return lines;
        }

        private string ContentHash(string path)
        {
            try
            {
                var bytes = ReadAll(path);
                return bytes.Length + "/" + Hash(bytes, 0, bytes.Length);
            }
            catch (NotImplementedException)
            {
                // DiscUtils cannot read every kind of file (e.g. compressed and sparse at once).
                return "unreadable";
            }
        }

        private byte[] ReadAll(string path)
        {
            using var s = _fs.OpenFile(path, FileMode.Open, FileAccess.Read);
            var ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        }

        private static int Size(sbyte raw, int cluster) => raw < 0 ? 1 << -raw : raw * cluster;

        private static (string Hash, ulong Lsn) UsedHash(byte[] all, int offset, int length, bool isIndex, ISet<long> excludedRecords = null)
        {
            var b = Logical(all, offset, length);
            var signature = BitConverter.ToUInt32(b, 0);
            if (signature != (isIndex ? 0x58444E49u : 0x454C4946u))
            {
                return (Hash(b, 0, b.Length), 0);
            }

            var lsn = BitConverter.ToUInt64(b, 8);
            var used = isIndex ? 0x18 + (int)BitConverter.ToUInt32(b, 0x1C) : (int)BitConverter.ToUInt32(b, 0x18);
            var usaOffset = BitConverter.ToUInt16(b, 4);
            var usaCount = BitConverter.ToUInt16(b, 6);
            Array.Clear(b, 8, 8);
            Array.Clear(b, usaOffset, 2 * usaCount);
            if (isIndex && excludedRecords != null)
            {
                var entry = 0x18 + (int)BitConverter.ToUInt32(b, 0x18);
                while (entry + 0x10 <= used && entry + 0x10 <= length)
                {
                    var entryLength = BitConverter.ToUInt16(b, entry + 8);
                    if (entryLength < 0x10)
                    {
                        break;
                    }

                    var record = (long)(BitConverter.ToUInt64(b, entry) & 0xFFFFFFFFFFFF);
                    if ((BitConverter.ToUInt16(b, entry + 0x0C) & 2) == 0 && excludedRecords.Contains(record) && entry + 0x50 <= length)
                    {
                        Array.Clear(b, entry + 0x18, 0x38);
                    }

                    entry += entryLength;
                }
            }

            return (Hash(b, 0, Math.Min(Math.Max(used, 0), length)), lsn);
        }

        private static byte[] Logical(byte[] all, int offset, int length)
        {
            var b = new byte[length];
            Array.Copy(all, offset, b, 0, length);
            var usaOffset = BitConverter.ToUInt16(b, 4);
            var usaCount = BitConverter.ToUInt16(b, 6);
            if (usaCount >= 2 && usaOffset + (2 * usaCount) <= length && length % (usaCount - 1) == 0)
            {
                var stride = length / (usaCount - 1);
                for (var i = 1; i < usaCount; i++)
                {
                    b[(i * stride) - 2] = b[usaOffset + (2 * i)];
                    b[(i * stride) - 1] = b[usaOffset + (2 * i) + 1];
                }
            }

            return b;
        }

        private static string MaskedHash(byte[] bitmap, ISet<long> excludedBits)
        {
            var copy = (byte[])bitmap.Clone();
            foreach (var bit in excludedBits)
            {
                if (bit >> 3 < copy.Length)
                {
                    copy[bit >> 3] &= (byte)~(1 << (int)(bit & 7));
                }
            }

            return Hash(copy, 0, copy.Length);
        }
    }
}
