using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using DiscUtils.Ntfs;

namespace LibraryTests.Ntfs.Journal;

/// <summary>
/// Volumes Windows really crashed on, checked in as test data, each with a digest of Windows' own replay of the
/// same crash (see <see cref="WindowsReplayDigest"/>).
/// </summary>
/// <remarks>
/// Made in a Hyper-V VM (Windows 11 Enterprise LTSC 2024, build 26100): a small data disk formatted NTFS in the
/// guest, several jobs creating, writing, renaming and deleting files, directories, hard links and alternate data
/// streams, and the VM powered off at a random moment. Windows' replay is a copy of the crashed disk attached to the
/// same VM, brought online (which replays the journal) and taken offline again without touching any file.
/// </remarks>
public class NtfsJournalWindowsSampleTest
{
    /// <summary>Sample, what the journal needs, and the cluster size.</summary>
    public static IEnumerable<object[]> Samples =>
    [
        ["s4k-c", NtfsJournalStatus.NothingToReplay, 4096],
        ["s1k", NtfsJournalStatus.NothingToReplay, 1024],
    ];

    internal static MemoryStream LoadVolume(string name) => Load($"journal-{name}.ntfs.gz");

    internal static List<string> LoadDigest(string name)
    {
        using var stream = Load($"journal-{name}.windows.txt.gz");
        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return lines;
    }

    private static MemoryStream Load(string file)
    {
        // The data lives next to the sources; look for it upwards from wherever the tests run.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            foreach (var candidate in new[]
                     {
                         Path.Combine(directory.FullName, "LibraryTests", "Ntfs", "Data", file),
                         Path.Combine(directory.FullName, "Tests", "LibraryTests", "Ntfs", "Data", file),
                     })
            {
                if (System.IO.File.Exists(candidate))
                {
                    using var gz = new GZipStream(System.IO.File.OpenRead(candidate), CompressionMode.Decompress);
                    var volume = new MemoryStream();
                    gz.CopyTo(volume);
                    volume.Position = 0;
                    return volume;
                }
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Test data {file} not found above {AppContext.BaseDirectory}");
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Analyze_ClassifiesTheCrashLikeWindows(string name, NtfsJournalStatus expected, int clusterSize)
    {
        using var volume = LoadVolume(name);
        var info = NtfsJournal.Analyze(volume);
        Assert.True(info.Status == expected, $"{name}: expected {expected}, got {info}");
        Assert.Equal("2.0", info.LogVersion);
        Assert.True(clusterSize > 0);
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Replay_GivesWhatWindowsReplayGives(string name, NtfsJournalStatus expected, int clusterSize)
    {
        using var volume = LoadVolume(name);
        var info = NtfsJournal.Replay(volume);
        Assert.True(info.Status == expected, $"{name}: expected {expected}, got {info}");
        Assert.True(info.Replayed);

        var mismatches = WindowsReplayDigest.Check(volume, LoadDigest(name));
        Assert.True(mismatches.Count == 0, $"{name}: {mismatches.Count} differences from Windows' replay, first: {mismatches.FirstOrDefault()}");
        Assert.Equal(NtfsJournalStatus.Clean, NtfsJournal.Analyze(volume).Status);
        Assert.True(clusterSize > 0);
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void ReadOnlyOpen_ShowsWhatWindowsShows_AndWritesNothing(string name, NtfsJournalStatus expected, int clusterSize)
    {
        using var volume = LoadVolume(name);
        var image = volume.ToArray();
        using var readOnly = new MemoryStream(image, writable: false);
        var info = NtfsJournal.ReplayInMemory(readOnly, out var replayed);
        Assert.Equal(expected, info.Status);
        using (replayed)
        {
            var mismatches = WindowsReplayDigest.Check(replayed ?? (Stream)readOnly, LoadDigest(name));
            Assert.True(mismatches.Count == 0, $"{name}: {mismatches.Count} differences, first: {mismatches.FirstOrDefault()}");
        }

        using (var ntfs = new NtfsFileSystem(new MemoryStream(image, writable: false)))
        {
            Assert.Equal(expected, ntfs.JournalInfo.Status);
            Assert.NotEmpty(ntfs.GetDirectories(@"\"));
        }

        Assert.True(clusterSize > 0);
    }
}
