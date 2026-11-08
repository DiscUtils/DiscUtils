using System;
using System.IO;
using System.Linq;
using System.Text;
using DiscUtils;
using DiscUtils.Ntfs;
using DiscUtils.Ntfs.Journal;
using DiscUtils.Setup;
using DiscUtils.Streams;

namespace LibraryTests.Ntfs.Journal;

public partial class NtfsJournalEdgeCaseTest
{
    private const string HibernationFile = @"\HIBERFIL.SYS";
    private const string ActiveHibernation = "active Windows hibernation signature";
    private const string IndeterminateHibernation = "hibernation state is indeterminate";

    [Theory]
    [InlineData("hibr")]
    [InlineData("HIBR")]
    [InlineData("HORM")]
    public void ActiveHibernationBlocksEveryPersistentReplayEntryPoint(string signature)
    {
        using var dirty = Volume();
        PutHibernationFile(dirty, HibernationHeader(signature));
        WriteRecoverableJournal(dirty);
        using var volume = new ReplayTrackingStream(dirty.ToArray(), failFlush: 0);
        var before = volume.ToArray();

        AssertReplayRefused(volume, ActiveHibernation);
        Assert.Contains(ActiveHibernation, Assert.Throws<IOException>(() => new NtfsFileSystem(volume, NtfsJournalMode.Replay)).Message);

        SetupHelper.RegisterAssembly(typeof(NtfsFileSystem).Assembly);
        var factory = FileSystemManager.DetectFileSystems(volume).Single(f => f.Name == "NTFS");
        var parameters = new FileSystemParameters { Parameters = { { "NtfsJournalMode", "Replay" } } };
        Assert.Contains(ActiveHibernation, Assert.Throws<IOException>(() => factory.Open(volume, parameters)).Message);

        Assert.Throws<IOException>(() => new NtfsFileSystem(volume, NtfsJournalMode.Refuse));
        Assert.Throws<IOException>(() => new NtfsFileSystem(volume));
        Assert.Empty(volume.Events);
        Assert.True(before.AsSpan().SequenceEqual(volume.ToArray()));
    }

    [Fact]
    public void ActiveHibernationAlsoBlocksAResetWithoutMetadataChanges()
    {
        using var volume = Volume();
        PutHibernationFile(volume, HibernationHeader("HIBR"));
        var writer = new TestJournalWriter(volume);
        writer.Finish(writer.Checkpoint([]));

        AssertReplayRefused(volume, ActiveHibernation);
    }

    [Fact]
    public void HibernationSuppressesTheOverlayAndAllowsRawReadOnlyInspection()
    {
        using var volume = Volume();
        PutHibernationFile(volume, HibernationHeader("HIBR"));
        var target = WriteRecoverableJournal(volume);
        var before = volume.ToArray();
        using var readOnly = new MemoryStream(before, writable: false);

        var info = NtfsJournal.ReplayInMemory(readOnly, out var overlay);
        Assert.Equal(NtfsJournalStatus.Unsupported, info.Status);
        Assert.Contains(ActiveHibernation, info.Detail);
        Assert.Null(overlay);

        using var fs = new NtfsFileSystem(readOnly);
        Assert.False(fs.CanWrite);
        Assert.Equal(NtfsJournalStatus.Unsupported, fs.JournalInfo.Status);
        Assert.False(fs.JournalInfo.ReplayedInMemory);
        Assert.True(fs.FileExists(HibernationFile));
        Assert.Equal(0, Read(fs.RawStream, target * 4096, 1)[0]);
        Assert.Throws<IOException>(() => new NtfsFileSystem(readOnly, NtfsJournalMode.Refuse));

        using var ignored = new NtfsFileSystem(readOnly, NtfsJournalMode.Ignore);
        Assert.Null(ignored.JournalInfo);
        Assert.False(ignored.CanWrite);
        Assert.True(before.AsSpan().SequenceEqual(volume.ToArray()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wake")]
    [InlineData("WAKE")]
    public void MissingOrInactiveLocalHeaderAllowsOrdinaryRecovery(string? signature)
    {
        using var volume = Volume();
        if (signature != null)
        {
            var bytes = new byte[8192];
            Encoding.ASCII.GetBytes(signature).CopyTo(bytes, 0);
            bytes[4096] = 0xA5;
            PutHibernationFile(volume, bytes);
        }
        else
        {
            // A copy outside the root supplies no local indication.
            using var fs = new NtfsFileSystem(volume, NtfsJournalMode.Ignore);
            using var file = fs.OpenFile(@"big\hiberfil.sys", FileMode.Create, FileAccess.Write);
            file.Write(HibernationHeader("HIBR"), 0, 4096);
        }

        var target = WriteRecoverableJournal(volume);
        var before = volume.ToArray();
        var info = NtfsJournal.Analyze(volume);
        Assert.Equal(NtfsJournalStatus.NeedsReplay, info.Status);
        Assert.True(info.CanReplayPersistently);
        Assert.True(before.AsSpan().SequenceEqual(volume.ToArray()));
        Assert.True(NtfsJournal.Replay(volume).Replayed);
        Assert.Equal(Pattern, Read(volume, target * 4096, Pattern.Length));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(4095)]
    public void AShortHibernationFileIsIndeterminate(int size)
    {
        using var volume = Volume();
        PutHibernationFile(volume, HibernationHeader("HIBR").AsSpan(0, size).ToArray());
        WriteRecoverableJournal(volume);

        AssertReplayRefused(volume, IndeterminateHibernation);
    }

    [Theory]
    [InlineData("rstr", "resume-in-progress signature")]
    [InlineData("RSTR", "resume-in-progress signature")]
    [InlineData("other", "unrecognized nonzero header")]
    [InlineData("", "unrecognized nonzero header")]
    public void AnUnrecognizedOrInterruptedResumeHeaderIsIndeterminate(string signature, string reason)
    {
        using var volume = Volume();
        var header = HibernationHeader(signature);
        header[^1] = 0xA5;
        PutHibernationFile(volume, header);
        WriteRecoverableJournal(volume);

        AssertReplayRefused(volume, reason);
    }

    [Theory]
    [InlineData(false, "initialized header")]
    [InlineData(true, "invalid header cluster mapping")]
    public void AnUntrustworthyHibernationDataMappingIsIndeterminate(bool invalidRun, string reason)
    {
        using var volume = Volume();
        PutHibernationFile(volume, HibernationHeader("HIBR"));
        var layout = JournalVolumeLayout.Read(volume);
        using (var fs = new NtfsFileSystem(volume, NtfsJournalMode.Ignore))
        {
            var entry = fs.GetDirectoryEntry(HibernationFile)!.Value;
            var mft = fs.GetFile(entry.Reference).Context.Mft;
            var record = mft.GetRecord(entry.Reference);
            var data = (NonResidentAttributeRecord)record.GetAttribute(AttributeType.Data);
            if (invalidRun)
            {
                data.DataRuns[0].RunOffset = layout.TotalClusters;
            }
            else
            {
                data.InitializedDataLength = 0;
            }

            mft.WriteRecord(record);
        }

        WriteRecoverableJournal(volume);
        AssertReplayRefused(volume, reason);
    }

    [Theory]
    [InlineData(false, false, "injected read failure")]
    [InlineData(false, true, "Unexpected end of the volume")]
    [InlineData(true, false, "injected read failure")]
    public void UnreadableHibernationMetadataCannotBeTreatedAsMissing(bool rootRecord, bool shortRead, string reason)
    {
        using var volume = Volume();
        var headerLcn = PutHibernationFile(volume, new byte[4096]);
        WriteRecoverableJournal(volume);
        var layout = JournalVolumeLayout.Read(volume);
        var fault = rootRecord ? layout.MftCluster * layout.BytesPerCluster + MasterFileTable.RootDirIndex * layout.MftRecordSize :
            headerLcn * layout.BytesPerCluster;
        using var unreadable = new HibernationReadFault(volume.ToArray(), fault, shortRead);

        AssertReplayRefused(unreadable, reason);
        Assert.Contains(IndeterminateHibernation, NtfsJournal.Analyze(unreadable).Detail);
    }

    [Fact]
    public void AnInvalidRootIndexCannotBeTreatedAsAnEmptyDirectory()
    {
        using var volume = Volume();
        PutHibernationFile(volume, HibernationHeader("HIBR"));
        using (var fs = new NtfsFileSystem(volume, NtfsJournalMode.Ignore))
        {
            var mft = fs.GetDirectory(MasterFileTable.RootDirIndex).Context.Mft;
            var record = mft.GetRecord(MasterFileTable.RootDirIndex, ignoreMagic: false);
            var index = (ResidentAttributeRecord)record.GetAttribute(AttributeType.IndexRoot, "$I30");
            var endOfEntries = new byte[4];
            index.DataBuffer.Read(IndexRoot.HeaderOffset + 4, endOfEntries, 0, endOfEntries.Length);
            index.DataBuffer.Write(IndexRoot.HeaderOffset, endOfEntries, 0, endOfEntries.Length);
            mft.WriteRecord(record);
        }

        WriteRecoverableJournal(volume);
        AssertReplayRefused(volume, "directory index node has no end entry");
    }

    [Fact]
    public void AHibernationIndexEntryWithAnInconsistentFileRecordIsRefused()
    {
        using var volume = Volume();
        PutHibernationFile(volume, new byte[4096]);
        using (var fs = new NtfsFileSystem(volume, NtfsJournalMode.Ignore))
        {
            var entry = fs.GetDirectoryEntry(HibernationFile)!.Value;
            var mft = fs.GetFile(entry.Reference).Context.Mft;
            var record = mft.GetRecord(entry.Reference);
            var name = (ResidentAttributeRecord)record.GetAttribute(AttributeType.FileName);
            name.DataBuffer.Write(0x42, [(byte)'X'], 0, 1);
            mft.WriteRecord(record);
        }

        WriteRecoverableJournal(volume);
        AssertReplayRefused(volume, "inconsistent file record");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ASignatureInEitherTheRawOrTheStagedViewBlocksReplay(bool activeBeforeRedo)
    {
        using var volume = Volume();
        var lcn = PutHibernationFile(volume, HibernationHeader(activeBeforeRedo ? "HIBR" : ""));
        var writer = new TestJournalWriter(volume);
        var restart = writer.Checkpoint(Attributes);
        writer.Append(Raw(lcn, activeBeforeRedo ? new byte[4] : Encoding.ASCII.GetBytes("HIBR")));
        writer.Append(Forget());
        writer.Finish(restart);

        using var tracked = new ReplayTrackingStream(volume.ToArray(), failFlush: 0);
        var reason = activeBeforeRedo ? ActiveHibernation : $"{ActiveHibernation} in the staged replay view";
        AssertReplayRefused(tracked, reason);
        Assert.Equal(NtfsJournalStatus.Unsupported, NtfsJournal.ReplayInMemory(tracked, out var overlay).Status);
        Assert.Null(overlay);
        Assert.Empty(tracked.Events);
    }

    [Fact]
    public void ARepairableTornMftBootstrapCannotHideTheHibernationFile()
    {
        using var volume = Volume();
        PutHibernationFile(volume, HibernationHeader("HIBR"));
        var writer = new TestJournalWriter(volume);
        var mft = writer.Layout.MftCluster;
        var offset = mft * writer.Layout.BytesPerCluster;
        var record = Read(volume, offset, writer.Layout.MftRecordSize, logical: true);
        var restart = writer.Checkpoint(Attributes);
        writer.Append(Op(NtfsLogOperation.InitializeFileRecordSegment, record, Mft, 0, [mft]));
        writer.Append(Forget());
        writer.Finish(restart);

        volume.Position = offset + record.Length - 2;
        var trailer = volume.ReadByte();
        volume.Position--;
        volume.WriteByte((byte)(trailer ^ 0xFF));

        AssertReplayRefused(volume, ActiveHibernation);
    }

    [Fact]
    public void APartitionStreamWithActiveHibernationIsAlsoRefused()
    {
        using var volume = Volume();
        PutHibernationFile(volume, HibernationHeader("HIBR"));
        WriteRecoverableJournal(volume);
        using var disk = new MemoryStream();
        disk.Position = 512;
        volume.WriteTo(disk);
        using var partition = new SubStream(disk, 512, volume.Length);
        var before = disk.ToArray();

        Assert.Contains(ActiveHibernation, Assert.Throws<IOException>(() => NtfsJournal.Replay(partition)).Message);
        Assert.True(before.AsSpan().SequenceEqual(disk.ToArray()));
    }

    [Fact]
    public void AReplayCallWithACleanJournalMakesNoChanges()
    {
        using var volume = Volume();
        PutHibernationFile(volume, HibernationHeader("HIBR"));
        var before = volume.ToArray();

        var info = NtfsJournal.Replay(volume);
        Assert.Equal(NtfsJournalStatus.Clean, info.Status);
        Assert.False(info.Replayed);
        Assert.True(before.AsSpan().SequenceEqual(volume.ToArray()));
    }

    private static byte[] HibernationHeader(string signature)
    {
        var bytes = new byte[4096];
        Encoding.ASCII.GetBytes(signature).CopyTo(bytes, 0);
        return bytes;
    }

    private static long PutHibernationFile(Stream volume, byte[] bytes)
    {
        using var fs = new NtfsFileSystem(volume, NtfsJournalMode.Ignore);
        using (var file = fs.OpenFile(HibernationFile, FileMode.Create, FileAccess.Write))
        {
            file.Write(bytes, 0, bytes.Length);
        }

        fs.SetAttributes(HibernationFile, FileAttributes.Hidden | FileAttributes.System);
        return fs.PathToClusters(HibernationFile).FirstOrDefault().Offset;
    }

    private static long WriteRecoverableJournal(MemoryStream volume)
    {
        var target = FreeCluster(volume);
        var writer = new TestJournalWriter(volume);
        var restart = writer.Checkpoint(Attributes);
        writer.Append(Raw(target, Pattern));
        writer.Append(Forget());
        writer.Finish(restart);
        return target;
    }

    private sealed class HibernationReadFault(byte[] image, long faultOffset, bool shortRead) : MemoryStream(image)
    {
        public override int Read(byte[] buffer, int offset, int count) =>
            FailRead(count) ? 0 : base.Read(buffer, offset, count);

#if NET
        public override int Read(Span<byte> buffer) => FailRead(buffer.Length) ? 0 : base.Read(buffer);
#endif

        private bool FailRead(int count)
        {
            if (Position > faultOffset || count <= faultOffset - Position)
            {
                return false;
            }

            if (!shortRead)
            {
                throw new IOException("injected read failure");
            }

            return true;
        }
    }
}
