using System;
using System.IO;
using System.Linq;
using DiscUtils;
using DiscUtils.Ntfs;
using DiscUtils.Ntfs.Journal;
using DiscUtils.Setup;

namespace LibraryTests.Ntfs.Journal;

/// <summary>
/// Default and explicit journal modes through the NTFS constructor and the file system manager.
/// </summary>
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

    private static DiscFileSystem OpenThroughTheManager(Stream volume, FileSystemParameters? parameters = null)
    {
        SetupHelper.RegisterAssembly(typeof(NtfsFileSystem).Assembly);
        var info = FileSystemManager.DetectFileSystems(volume).Single(f => f.Name == "NTFS");
        return parameters == null ? info.Open(volume) : info.Open(volume, parameters);
    }

    [Theory]
    [InlineData(" rEpLaY ", NtfsJournalMode.Replay)]
    [InlineData("Refuse", NtfsJournalMode.Refuse)]
    [InlineData("ignore", NtfsJournalMode.Ignore)]
    public void TheNtfsJournalModeParameter_ChoosesTheModeForOneVolume(string mode, NtfsJournalMode expected)
    {
        using var volume = CrashedVolume();
        var image = volume.ToArray();
        var parameters = new FileSystemParameters { Parameters = { { "NtfsJournalMode", mode } } };

        if (expected == NtfsJournalMode.Refuse)
        {
            Assert.Throws<IOException>(() => OpenThroughTheManager(volume, parameters));
            Assert.True(image.AsSpan().SequenceEqual(volume.ToArray()));
            return;
        }

        using var ntfs = (NtfsFileSystem)OpenThroughTheManager(volume, parameters);
        if (expected == NtfsJournalMode.Ignore)
        {
            Assert.Null(ntfs.JournalInfo);
            Assert.False(ntfs.DirectoryExists("dir"));
            Assert.True(image.AsSpan().SequenceEqual(volume.ToArray()));
        }
        else
        {
            Assert.True(ntfs.JournalInfo.Replayed);
            Assert.False(ntfs.JournalInfo.ReplayedInMemory);
            Assert.True(ntfs.DirectoryExists(@"dir\sub"));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefaultWritableOpen_RequiresACleanJournal(bool alreadyApplied)
    {
        var scenario = Scenario.Value;
        using var volume = JournalScenario.Copy(scenario.Before);
        if (alreadyApplied)
        {
            var writer = new TestJournalWriter(volume);
            var restart = writer.Checkpoint([]);
            writer.Finish(restart);
        }
        else
        {
            scenario.WriteJournal(volume, committed: true);
        }
        var image = volume.ToArray();
        var expected = alreadyApplied ? NtfsJournalStatus.NothingToReplay : NtfsJournalStatus.NeedsReplay;
        Assert.Equal(expected, NtfsJournal.Analyze(volume).Status);

        Assert.Throws<IOException>(() => new NtfsFileSystem(volume));
        Assert.Throws<IOException>(() => OpenThroughTheManager(volume));
        Assert.Throws<IOException>(() => OpenThroughTheManager(volume, new FileSystemParameters()));
        Assert.True(image.AsSpan().SequenceEqual(volume.ToArray()));
    }

    [Fact]
    public void DefaultWritableOpen_LeavesACleanVolumeUnchanged()
    {
        using var volume = JournalScenario.Copy(Scenario.Value.Before);
        using (var fs = (NtfsFileSystem)OpenThroughTheManager(volume))
        {
            Assert.Equal(NtfsJournalStatus.Clean, fs.JournalInfo.Status);
            Assert.True(fs.CanWrite);
            Assert.False(fs.JournalInfo.Replayed);
            Assert.False(fs.JournalInfo.ReplayedInMemory);
        }

        Assert.True(Scenario.Value.Before.AsSpan().SequenceEqual(volume.ToArray()));
    }

    [Fact]
    public void DefaultReadOnlyOpenThroughTheManager_UsesAnOverlay()
    {
        using var writable = CrashedVolume();
        var image = writable.ToArray();
        using var volume = new MemoryStream(image, writable: false);
        using var fs = (NtfsFileSystem)OpenThroughTheManager(volume);
        Assert.Equal(NtfsJournalStatus.NeedsReplay, fs.JournalInfo.Status);
        Assert.False(fs.JournalInfo.Replayed);
        Assert.True(fs.JournalInfo.ReplayedInMemory);
        Assert.False(fs.CanWrite);
        Assert.True(fs.DirectoryExists(@"dir\sub"));
        Assert.True(image.AsSpan().SequenceEqual(writable.ToArray()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no such mode")]
    [InlineData("0")]
    [InlineData("99")]
    [InlineData("Replay, Refuse")]
    public void InvalidModeParameter_IsRejectedBeforeWriting(string? mode)
    {
        using var volume = CrashedVolume();
        var image = volume.ToArray();
        var parameters = new FileSystemParameters { Parameters = { { "NtfsJournalMode", mode } } };
        Assert.Throws<ArgumentException>(() => OpenThroughTheManager(volume, parameters));
        Assert.True(image.AsSpan().SequenceEqual(volume.ToArray()));
    }

    [Fact]
    public void InvalidEnumMode_IsRejectedBeforeWriting()
    {
        using var volume = CrashedVolume();
        var image = volume.ToArray();
        Assert.Throws<ArgumentOutOfRangeException>(() => new NtfsFileSystem(volume, (NtfsJournalMode)99));
        Assert.True(image.AsSpan().SequenceEqual(volume.ToArray()));
    }

    [Fact]
    public void JournalApis_RejectInvalidStreamArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new NtfsFileSystem(null!));
        Assert.Throws<ArgumentNullException>(() => NtfsJournal.Analyze(null!));
        Assert.Throws<ArgumentNullException>(() => NtfsJournal.Replay(null!));

        using var disposed = new MemoryStream();
        disposed.Dispose();
        Assert.Throws<ArgumentException>(() => NtfsJournal.Analyze(disposed));
        using var readOnly = new MemoryStream(Scenario.Value.Before, writable: false);
        Assert.Throws<IOException>(() => NtfsJournal.Replay(readOnly));
    }
}
