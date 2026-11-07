using System;
using System.IO;
using System.Linq;
using DiscUtils;
using DiscUtils.Ntfs;
using DiscUtils.Setup;

namespace LibraryTests.Ntfs.Journal;

/// <summary>
/// Choosing the journal mode without calling the <see cref="NtfsFileSystem"/> constructor: the "NtfsJournalMode"
/// <see cref="FileSystemParameters"/> entry for one volume, <see cref="NtfsFileSystem.DefaultJournalMode"/> for all.
/// </summary>
[Collection("NTFS journal default mode")]
public class NtfsJournalOpenModeTest
{
    private static readonly Lazy<JournalScenario> Scenario = new(() => JournalScenario.Create(ntfs => ntfs.CreateDirectory(@"dir\sub")));

    /// <summary>A volume whose journal holds the creation of "dir", which is not on disk yet.</summary>
    private static MemoryStream CrashedVolume()
    {
        var volume = JournalScenario.Copy(Scenario.Value.Before);
        Scenario.Value.WriteJournal(volume, committed: true);
        return volume;
    }

    // mode null: no parameters at all; "": parameters without the entry.
    private static DiscFileSystem OpenThroughTheManager(Stream volume, string? mode)
    {
        SetupHelper.RegisterAssembly(typeof(NtfsFileSystem).Assembly);
        var info = FileSystemManager.DetectFileSystems(volume).Single(f => f.Name == "NTFS");
        if (mode == null)
        {
            return info.Open(volume);
        }

        var parameters = new FileSystemParameters();
        if (mode.Length > 0)
        {
            parameters.Parameters["NtfsJournalMode"] = mode;
        }

        return info.Open(volume, parameters);
    }

    [Theory]
    [InlineData(null, NtfsJournalMode.Replay)]
    [InlineData("", NtfsJournalMode.Replay)]
    [InlineData("no such mode", NtfsJournalMode.Replay)]
    [InlineData("Refuse", NtfsJournalMode.Refuse)]
    [InlineData("ignore", NtfsJournalMode.Ignore)]
    public void TheNtfsJournalModeParameter_ChoosesTheModeForOneVolume(string? mode, NtfsJournalMode expected)
    {
        using var volume = CrashedVolume();
        var image = volume.ToArray();

        if (expected == NtfsJournalMode.Refuse)
        {
            Assert.Throws<IOException>(() => OpenThroughTheManager(volume, mode));
            Assert.True(image.AsSpan().SequenceEqual(volume.ToArray()));
            return;
        }

        using var ntfs = (NtfsFileSystem)OpenThroughTheManager(volume, mode);
        if (expected == NtfsJournalMode.Ignore)
        {
            Assert.Null(ntfs.JournalInfo);
            Assert.False(ntfs.DirectoryExists("dir"));
        }
        else
        {
            Assert.True(ntfs.JournalInfo.Replayed);
            Assert.True(ntfs.DirectoryExists(@"dir\sub"));
        }
    }

    [Fact]
    public void DefaultJournalMode_AppliesToEveryOpenWithoutAMode_AndAParameterOverridesIt()
    {
        var previous = NtfsFileSystem.DefaultJournalMode;
        try
        {
            NtfsFileSystem.DefaultJournalMode = NtfsJournalMode.Refuse;
            using var volume = CrashedVolume();
            var image = volume.ToArray();

            Assert.Throws<IOException>(() => new NtfsFileSystem(volume));
            Assert.Throws<IOException>(() => OpenThroughTheManager(volume, null));
            Assert.True(image.AsSpan().SequenceEqual(volume.ToArray()));

            using var ntfs = (NtfsFileSystem)OpenThroughTheManager(volume, "Replay");
            Assert.True(ntfs.JournalInfo.Replayed);
            Assert.True(ntfs.DirectoryExists(@"dir\sub"));
        }
        finally
        {
            NtfsFileSystem.DefaultJournalMode = previous;
        }
    }
}

// DefaultJournalMode is process-wide: while one test changes it, no other test may open an NTFS volume.
[CollectionDefinition("NTFS journal default mode", DisableParallelization = true)]
public class NtfsJournalDefaultModeCollection
{
}
