using System;
using System.IO;
using System.Linq;
using System.Text;
using DiscUtils;
using DiscUtils.Ntfs;
using DiscUtils.Ntfs.Journal;
using DiscUtils.Streams;

namespace LibraryTests.Ntfs.Journal;

/// <summary>
/// The parts of a replay the page-image scenarios do not reach: real operations on index buffers and file records
/// through the whole replay, the checkpoint's tables, compensation records and top-level actions, freed and moved
/// clusters, both table layouts, tail copies, the $MFTMirr copy, and every way a journal is refused.
/// </summary>
public class NtfsJournalReplayDetailTest
{
    private const ushort Mft = 0x18;
    private const ushort Index = 0x40;
    private const ushort Data = 0x68;
    private const uint Tx = 0x40;

    private static readonly (ushort, uint, ulong, uint, string)[] Attributes =
    [
        (Mft, 0x80, 0x0001000000000000UL, 0, ""),
        (Index, 0xA0, 0x0005000000000005UL, 4096, "$I30"),
        (Data, 0x80, 0x0001000000000006UL, 0, ""),
    ];

    #region volume helpers

    private static MemoryStream Volume(int files = 150)
    {
        var stream = new MemoryStream();
        var geometry = Geometry.FromCapacity(16 * 1024 * 1024);
        using (var ntfs = NtfsFileSystem.Format(stream, "DETAIL", geometry, 0, geometry.TotalSectorsLong))
        {
            ntfs.CreateDirectory("big");
            for (var i = 0; i < files; i++)
            {
                using var s = ntfs.OpenFile($@"big\file-with-a-long-name-{i:D4}.txt", FileMode.Create);
                s.WriteByte((byte)i);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static NtfsFileSystem Open(Stream volume) => new(volume, NtfsJournalMode.Ignore);

    /// <summary>Where an MFT record lives: its cluster, its offset there in 512-byte units, and its VCN in $MFT.</summary>
    private static (long Lcn, int BlockOffset, long Vcn) MftRecord(Stream volume, long record)
    {
        using var ntfs = Open(volume);
        var position = record * 1024;
        var vcn = position / 4096;
        long v = 0;
        foreach (var range in ntfs.PathToClusters("$MFT"))
        {
            if (vcn < v + range.Count)
            {
                return (range.Offset + (vcn - v), (int)(position % 4096) / 512, vcn);
            }

            v += range.Count;
        }

        throw new InvalidOperationException("no such record");
    }

    private static long RecordOf(Stream volume, string path)
    {
        using var ntfs = Open(volume);
        return ntfs.GetFileId(path) & 0x0000FFFFFFFFFFFF;
    }

    private static long FirstIndexBuffer(Stream volume)
    {
        var bytes = ((MemoryStream)volume).ToArray();
        for (var lcn = 16; lcn < bytes.Length / 4096; lcn++)
        {
            if (EndianUtilities.ToUInt32LittleEndian(bytes, lcn * 4096) == JournalUtilities.SignatureIndx)
            {
                return lcn;
            }
        }

        throw new InvalidOperationException("no index buffer");
    }

    private static long FreeCluster(Stream volume)
    {
        var bytes = ((MemoryStream)volume).ToArray();
        for (var lcn = (bytes.Length / 4096) - 2; lcn > 16; lcn--)
        {
            if (bytes.AsSpan(lcn * 4096, 4096).ToArray().All(b => b == 0))
            {
                return lcn;
            }
        }

        throw new InvalidOperationException("no free cluster");
    }

    private static byte[] ReadLogical(Stream volume, long position, int length)
    {
        var bytes = new byte[length];
        JournalUtilities.ReadExactly(volume, position, bytes, 0, length);
        Assert.True(JournalUtilities.RemoveFixups(bytes, 0, length));
        return bytes;
    }

    private static void WriteLogical(Stream volume, long position, byte[] logical)
    {
        var bytes = (byte[])logical.Clone();
        JournalUtilities.ApplyFixups(bytes, 0, bytes.Length);
        volume.Position = position;
        volume.Write(bytes, 0, bytes.Length);
    }

    private static byte[] ReadRaw(Stream volume, long position, int length)
    {
        var bytes = new byte[length];
        JournalUtilities.ReadExactly(volume, position, bytes, 0, length);
        return bytes;
    }

    private static TestLogRecord RawWrite(long lcn, byte[] redo, byte[] undo, ushort attribute = Data) => new()
    {
        Redo = NtfsLogOperation.UpdateNonresidentValue,
        Undo = NtfsLogOperation.UpdateNonresidentValue,
        TransactionId = Tx,
        RedoData = redo,
        UndoData = undo,
        TargetAttribute = attribute,
        TargetVcn = lcn,
        Lcns = [lcn],
    };

    private static TestLogRecord Forget(uint tx = Tx) =>
        new() { Redo = NtfsLogOperation.ForgetTransaction, Undo = NtfsLogOperation.CompensationLogRecord, TransactionId = tx };

    private static void AssertRefused(Stream volume, string reason)
    {
        var image = ((MemoryStream)volume).ToArray();
        var info = NtfsJournal.Analyze(volume);
        Assert.True(info.Status == NtfsJournalStatus.Unsupported, $"expected Unsupported ({reason}), got {info}");
        Assert.True(info.Detail.Contains(reason), $"expected '{reason}' in '{info.Detail}'");
        Assert.Throws<IOException>(() => NtfsJournal.Replay(volume));
        Assert.True(image.AsSpan().SequenceEqual(((MemoryStream)volume).ToArray()));
    }

    #endregion

    [Fact]
    public void IndexBufferOperations_AreRedone_AndUndone()
    {
        foreach (var committed in new[] { true, false })
        {
            using var volume = Volume();
            var lcn = FirstIndexBuffer(volume);
            var original = ReadLogical(volume, lcn * 4096, 4096);
            var entry = 0x18 + (int)EndianUtilities.ToUInt32LittleEndian(original, 0x18);
            var oldInfo = original.AsSpan(entry + 0x18, 0x38).ToArray();
            var newInfo = Enumerable.Repeat((byte)0x77, 0x38).ToArray();

            if (!committed)
            {
                // The change reached the disk, the transaction did not finish.
                var changed = (byte[])original.Clone();
                newInfo.CopyTo(changed, entry + 0x18);
                WriteLogical(volume, lcn * 4096, changed);
            }

            var writer = new TestJournalWriter(volume);
            var restart = writer.Checkpoint(Attributes);
            var lsn = writer.Append(new TestLogRecord
            {
                Redo = NtfsLogOperation.UpdateFileNameAllocation,
                Undo = NtfsLogOperation.UpdateFileNameAllocation,
                TransactionId = Tx,
                RedoData = newInfo,
                UndoData = oldInfo,
                TargetAttribute = Index,
                TargetVcn = 0,
                AttributeOffset = entry,
                Lcns = [lcn],
            });
            if (committed)
            {
                writer.Append(Forget());
            }

            writer.Finish(restart);
            var info = NtfsJournal.Replay(volume);
            Assert.Equal(NtfsJournalStatus.NeedsReplay, info.Status);

            var result = ReadLogical(volume, lcn * 4096, 4096);
            Assert.True(result.AsSpan(entry + 0x18, 0x38).SequenceEqual(committed ? newInfo : oldInfo));
            Assert.Equal(committed ? lsn : writer.LastLsn + 1, EndianUtilities.ToUInt64LittleEndian(result, 8));
            Assert.Equal(committed ? 0 : 1, info.TransactionsToUndo);
        }
    }

    [Fact]
    public void FileRecordOperations_AreRedone_AndUndone_AndAnUpToDateRecordIsLeftAlone()
    {
        using var volume = Volume();
        var record = RecordOf(volume, @"big\file-with-a-long-name-0007.txt");
        var (lcn, block, vcn) = MftRecord(volume, record);
        var position = (lcn * 4096) + (block * 512);
        var original = ReadLogical(volume, position, 1024);
        var created = original.AsSpan(0x38 + 0x18, 8).ToArray();
        var newTime = BitConverter.GetBytes(0x01D0000000000000L);

        var writer = new TestJournalWriter(volume);
        var restart = writer.Checkpoint(Attributes);
        var update = new TestLogRecord
        {
            Redo = NtfsLogOperation.UpdateResidentValue,
            Undo = NtfsLogOperation.UpdateResidentValue,
            TransactionId = Tx,
            RedoData = newTime,
            UndoData = created,
            TargetAttribute = Mft,
            TargetVcn = vcn,
            ClusterBlockOffset = block,
            RecordOffset = 0x38,
            AttributeOffset = 0x18,
            Lcns = [lcn],
        };
        var lsn = writer.Append(update);
        writer.Append(Forget());
        writer.Finish(restart);

        var info = NtfsJournal.Replay(volume);
        Assert.Contains(record, info.FileRecordsChanged);
        var result = ReadLogical(volume, position, 1024);
        Assert.True(result.AsSpan(0x38 + 0x18, 8).SequenceEqual(newTime));
        Assert.Equal(lsn, EndianUtilities.ToUInt64LittleEndian(result, 8));

        // Now the record is newer than the log: a second journal with the same change is not needed.
        var again = new TestJournalWriter(volume);
        var restart2 = again.Checkpoint(Attributes);
        update.RedoData = BitConverter.GetBytes(0x01C0000000000000L);
        again.Append(update);
        again.Append(Forget());
        again.Finish(restart2);
        Assert.Equal(NtfsJournalStatus.NothingToReplay, NtfsJournal.Analyze(volume).Status);
    }

    [Fact]
    public void CheckpointTables_DirtyPagesAndTransactions_DriveRedoAndUndo()
    {
        foreach (var version in new uint[] { 0, 1 })
        {
            // Entries are 0x2C bytes in version 0 and 0x28 in version 1: the third entry's offset differs.
            var data = (ushort)(version == 0 ? 0x18 + 0x2C : Data);
            using var volume = Volume(files: 5);
            var lcn = FreeCluster(volume);
            var before = ReadRaw(volume, lcn * 4096, 4096);
            var after = Enumerable.Repeat((byte)0x5C, 4096).ToArray();
            volume.Position = lcn * 4096;
            volume.Write(after, 0, after.Length);

            // The change was logged before the checkpoint; the checkpoint lists the page as dirty and the
            // transaction as running. Nothing after the checkpoint finishes it.
            var writer = new TestJournalWriter(volume);
            var change = writer.Append(RawWrite(lcn, after, before, attribute: data));
            var restart = writer.Checkpoint(
                [(Mft, 0x80, 0x0001000000000000UL, 0, ""), (data, 0x80, 0x0001000000000006UL, 0, "$X")],
                [(data, lcn, change, [lcn])],
                [(Tx, TransactionState.Active, change, change), (0x68, TransactionState.Committed, change, change), (0x90, TransactionState.Prepared, change, change)],
                ntfsVersion: version);
            writer.Finish(restart, oldestLsn: change);

            var info = NtfsJournal.Replay(volume);
            Assert.True(info.Status == NtfsJournalStatus.NeedsReplay, $"v{version}: {info}");
            Assert.Equal(1, info.TransactionsToUndo);
            Assert.True(before.AsSpan().SequenceEqual(ReadRaw(volume, lcn * 4096, 4096)));
        }
    }

    [Fact]
    public void CompensationRecords_AndTopLevelActions_LimitWhatIsUndone()
    {
        using var volume = Volume(files: 5);
        var x = FreeCluster(volume);
        var y = x - 1;
        var z = x - 2;
        var zeros = new byte[64];
        byte[] Fill(byte b) => Enumerable.Repeat(b, 64).ToArray();
        foreach (var lcn in new[] { x, y, z })
        {
            volume.Position = lcn * 4096;
            volume.Write(Fill(0xEE), 0, 64);
        }

        var writer = new TestJournalWriter(volume);
        var restart = writer.Checkpoint(Attributes);

        // X: a change that will be undone. Y: a nested action that ended (top level action): kept.
        // Z: changed and then compensated already (the CLR's redo is the undo): stays as compensated.
        var a = writer.Append(RawWrite(x, Fill(0xEE), zeros));
        var b = writer.Append(new TestLogRecord { Redo = NtfsLogOperation.UpdateNonresidentValue, Undo = NtfsLogOperation.UpdateNonresidentValue, TransactionId = Tx, RedoData = Fill(0xEE), UndoData = zeros, TargetAttribute = Data, TargetVcn = y, Lcns = [y], UndoNextLsn = a });
        var end = writer.Append(new TestLogRecord { Redo = NtfsLogOperation.EndTopLevelAction, Undo = NtfsLogOperation.Noop, TransactionId = Tx, UndoNextLsn = a });
        var c = writer.Append(new TestLogRecord { Redo = NtfsLogOperation.UpdateNonresidentValue, Undo = NtfsLogOperation.UpdateNonresidentValue, TransactionId = Tx, RedoData = Fill(0xEE), UndoData = zeros, TargetAttribute = Data, TargetVcn = z, Lcns = [z], UndoNextLsn = end });
        writer.Append(new TestLogRecord { Redo = NtfsLogOperation.UpdateNonresidentValue, Undo = NtfsLogOperation.CompensationLogRecord, TransactionId = Tx, RedoData = Fill(0x11), TargetAttribute = Data, TargetVcn = z, Lcns = [z], UndoNextLsn = a });
        Assert.True(b > a && c > b);
        writer.Finish(restart);

        NtfsJournal.Replay(volume);
        Assert.True(ReadRaw(volume, x * 4096, 64).SequenceEqual(zeros));
        Assert.True(ReadRaw(volume, y * 4096, 64).SequenceEqual(Fill(0xEE)));
        Assert.True(ReadRaw(volume, z * 4096, 64).SequenceEqual(Fill(0x11)));
    }

    [Fact]
    public void FreedClusters_AreNotReplayed_AndHotFixMovesAPage()
    {
        using var volume = Volume(files: 5);
        var freed = FreeCluster(volume);
        var original = freed - 1;
        var moved = freed - 2;
        var data = Enumerable.Repeat((byte)0x42, 128).ToArray();

        var writer = new TestJournalWriter(volume);
        var restart = writer.Checkpoint(Attributes);
        writer.Append(RawWrite(freed, data, new byte[128]));
        var range = new byte[16];
        EndianUtilities.WriteBytesLittleEndian((ulong)freed, range, 0);
        EndianUtilities.WriteBytesLittleEndian(1UL, range, 8);
        writer.Append(new TestLogRecord { Redo = NtfsLogOperation.DeleteDirtyClusters, Undo = NtfsLogOperation.Noop, TransactionId = Tx, RedoData = range });

        // A page that moved (bad cluster replaced): the replay writes to the new cluster.
        writer.Append(new TestLogRecord { Redo = NtfsLogOperation.UpdateNonresidentValue, Undo = NtfsLogOperation.UpdateNonresidentValue, TransactionId = Tx, RedoData = data, UndoData = new byte[128], TargetAttribute = Data, TargetVcn = 7000, Lcns = [original] });
        writer.Append(new TestLogRecord { Redo = NtfsLogOperation.HotFix, Undo = NtfsLogOperation.Noop, TransactionId = Tx, TargetAttribute = Data, TargetVcn = 7000, Lcns = [moved] });
        writer.Append(Forget());
        writer.Finish(restart);

        NtfsJournal.Replay(volume);
        Assert.True(ReadRaw(volume, freed * 4096, 128).All(b => b == 0));
        Assert.True(ReadRaw(volume, original * 4096, 128).All(b => b == 0));
        Assert.True(ReadRaw(volume, moved * 4096, 128).SequenceEqual(data));
    }

    [Fact]
    public void AnAttributeOpenedAfterTheCheckpoint_CanBeChanged()
    {
        foreach (var version in new uint[] { 0, 1 })
        {
            using var volume = Volume();
            var lcn = FirstIndexBuffer(volume);
            var original = ReadLogical(volume, lcn * 4096, 4096);
            var changed = (byte[])original.Clone();
            changed[0x60] ^= 0xFF;

            var writer = new TestJournalWriter(volume);
            var restart = writer.Checkpoint([(Mft, 0x80, 0x0001000000000000UL, 0, "")], ntfsVersion: version);
            writer.Append(new TestLogRecord
            {
                Redo = NtfsLogOperation.OpenNonresidentAttribute,
                Undo = NtfsLogOperation.Noop,
                TransactionId = Tx,
                RedoData = TestJournalWriter.OpenAttributeEntryBytes(0xA0, 0x0005000000000005UL, 4096, version),
                UndoData = Encoding.Unicode.GetBytes("$I30"),
                TargetAttribute = 0x90,
            });

            // A whole new index buffer image (how NTFS logs a new one), in logical form.
            writer.Append(RawWrite(lcn, changed, original, attribute: 0x90));
            writer.Append(Forget());
            writer.Finish(restart);

            NtfsJournal.Replay(volume);
            Assert.True(ReadLogical(volume, lcn * 4096, 4096).AsSpan(0x40, 0x100).SequenceEqual(changed.AsSpan(0x40, 0x100)));
        }
    }

    [Fact]
    public void BitmapOperations_SetAndClearRanges_AndAnEmptyRangeChangesNothing()
    {
        using var volume = Volume(files: 5);
        var lcn = FreeCluster(volume);
        byte[] Range(uint first, uint count)
        {
            var b = new byte[8];
            EndianUtilities.WriteBytesLittleEndian(first, b, 0);
            EndianUtilities.WriteBytesLittleEndian(count, b, 4);
            return b;
        }

        TestLogRecord Bits(NtfsLogOperation op, uint first, uint count) =>
            new() { Redo = op, Undo = NtfsLogOperation.Noop, TransactionId = Tx, RedoData = Range(first, count), TargetAttribute = Data, TargetVcn = lcn, Lcns = [lcn], RecordOffset = 16 };

        var writer = new TestJournalWriter(volume);
        var restart = writer.Checkpoint(Attributes);
        writer.Append(Bits(NtfsLogOperation.SetBitsInNonresidentBitMap, 3, 20));
        writer.Append(Bits(NtfsLogOperation.ClearBitsInNonresidentBitMap, 5, 2));
        writer.Append(Bits(NtfsLogOperation.SetBitsInNonresidentBitMap, 100, 0));
        writer.Append(Forget());
        writer.Finish(restart);

        NtfsJournal.Replay(volume);
        Assert.Equal(new byte[] { 0x98, 0xFF, 0x7F, 0 }, ReadRaw(volume, (lcn * 4096) + 16, 4));
    }

    [Fact]
    public void TailCopies_HoldTheNewestPages()
    {
        foreach (var version in new[] { 1, 2 })
        {
            var scenario = JournalScenario.Create(ntfs => ntfs.CreateDirectory(@"tail"));
            using var volume = JournalScenario.Copy(scenario.Before);
            var writer = new TestJournalWriter(volume, version) { TailCopies = true };
            var restart = writer.Checkpoint(Attributes);
            ulong previous = 0;
            foreach (var record in scenario.Records)
            {
                record.UndoNextLsn = previous;
                previous = writer.Append(record);
            }

            writer.Append(Forget(JournalScenario.Transaction));

            // The page with the last records only made it to the tail area before the crash.
            writer.PagesOnlyInTail.Add(writer.LsnToOffsetForTest(writer.LastLsn) & ~(long)(TestJournalWriter.PageSize - 1));
            writer.Finish(restart);

            NtfsJournal.Replay(volume);
            scenario.AssertVolumeEquals(scenario.After, volume);
        }
    }

    [Fact]
    public void RestartPages_TheNewerValidCopyIsUsed()
    {
        var scenario = JournalScenario.Create(ntfs => ntfs.CreateDirectory(@"restart"));
        foreach (var damageFirst in new[] { true, false })
        {
            using var volume = JournalScenario.Copy(scenario.Before);
            var writer = scenario.WriteJournal(volume, committed: true);
            var page = new byte[TestJournalWriter.PageSize];
            if (damageFirst)
            {
                // A torn first copy: the second one is used.
                writer.Storage.Read(0, page, 0, page.Length);
                page[0x3FE] ^= 0xFF;
                writer.Storage.Write(0, page, 0, page.Length);
            }
            else
            {
                // An older first copy (lower current LSN): the second one is used.
                var older = writer.RestartPage(0, 0, clean: true, currentLsn: 1);
                writer.Storage.Write(0, older, 0, older.Length);
            }

            NtfsJournal.Replay(volume);
            scenario.AssertVolumeEquals(scenario.After, volume);
        }
    }

    [Fact]
    public void MftRecordsZeroToThree_AreCopiedToTheMftMirror()
    {
        // Enough new files to grow $MFT: record 0 ($MFT's own runs) changes.
        var scenario = JournalScenario.Create(ntfs =>
        {
            for (var i = 0; i < 400; i++)
            {
                ntfs.CreateDirectory($"d{i}");
            }
        });
        using var volume = JournalScenario.Copy(scenario.Before);
        scenario.WriteJournal(volume, committed: true);
        NtfsJournal.Replay(volume);
        scenario.AssertVolumeEquals(scenario.After, volume);

        using var ntfs = Open(volume);
        var mft = ntfs.PathToClusters("$MFT").First().Offset;
        var mirror = ntfs.PathToClusters("$MFTMirr").First().Offset;
        Assert.True(ReadRaw(volume, mft * 4096, 4096).SequenceEqual(ReadRaw(volume, mirror * 4096, 4096)));
    }

    [Fact]
    public void LogFileRecord_ComesFromTheMirrorWhenTheMftCopyIsDamaged()
    {
        var scenario = JournalScenario.Create(ntfs => ntfs.CreateDirectory(@"mirror"));
        using var volume = JournalScenario.Copy(scenario.Before);
        scenario.WriteJournal(volume, committed: true);
        var (lcn, block, _) = MftRecord(volume, 2);
        var position = (lcn * 4096) + (block * 512);
        volume.Position = position + 0x1FE;
        var original = volume.ReadByte();
        volume.Position = position + 0x1FE;
        volume.WriteByte(0x99);

        NtfsJournal.Replay(volume);
        volume.Position = position + 0x1FE;
        volume.WriteByte((byte)original);
        scenario.AssertVolumeEquals(scenario.After, volume);

        // Both copies damaged: the log cannot be found.
        using var both = JournalScenario.Copy(scenario.Before);
        scenario.WriteJournal(both, committed: true);
        using (var ntfs = Open(both))
        {
            foreach (var path in new[] { "$MFT", "$MFTMirr" })
            {
                var start = (ntfs.PathToClusters(path).First().Offset * 4096) + (2 * 1024);
                both.Position = start + 0x1FE;
                both.WriteByte(0x99);
            }
        }

        AssertRefused(both, "$LogFile");
    }

    [Theory]
    [InlineData("resident")]
    [InlineData("no data")]
    [InlineData("sparse")]
    [InlineData("runs")]
    public void LogFileRecord_ThatIsNotWhatNtfsWrites_IsRefused(string damage)
    {
        var scenario = JournalScenario.Create(ntfs => ntfs.CreateDirectory(@"x"));
        using var volume = JournalScenario.Copy(scenario.Before);
        scenario.WriteJournal(volume, committed: true);
        using (var ntfs = Open(volume))
        {
            foreach (var path in new[] { "$MFT", "$MFTMirr" })
            {
                var start = (ntfs.PathToClusters(path).First().Offset * 4096) + (2 * 1024);
                var record = ReadLogical(volume, start, 1024);
                var attr = (int)EndianUtilities.ToUInt16LittleEndian(record, 0x14);
                while (EndianUtilities.ToUInt32LittleEndian(record, attr) != 0x80)
                {
                    attr += (int)EndianUtilities.ToUInt32LittleEndian(record, attr + 4);
                }

                var runs = attr + EndianUtilities.ToUInt16LittleEndian(record, attr + 0x20);
                switch (damage)
                {
                    case "resident": record[attr + 8] = 0; break;
                    case "no data": EndianUtilities.WriteBytesLittleEndian(0x81u, record, attr); break;
                    case "sparse": record[runs] = 0x01; break;
                    case "runs": record[runs] = 0x99; break;
                }

                WriteLogical(volume, start, record);
            }
        }

        AssertRefused(volume, "$LogFile");
    }

    [Fact]
    public void Journals_ThatAreDamagedOrUnsupported_AreRefused_AndNothingIsWritten()
    {
        var scenario = JournalScenario.Create(ntfs => ntfs.CreateDirectory(@"refuse"));

        MemoryStream With(Action<TestJournalWriter, ulong> damage, bool committed = true)
        {
            var volume = JournalScenario.Copy(scenario.Before);
            var writer = new TestJournalWriter(volume);
            var restart = writer.Checkpoint(Attributes);
            damage(writer, restart);
            return volume;
        }

        // No restart page at all, and not an empty (0xFF) log.
        using (var volume = JournalScenario.Copy(scenario.Before))
        {
            var writer = new TestJournalWriter(volume);
            writer.Storage.Write(0, new byte[8192], 0, 8192);
            AssertRefused(volume, "no valid restart page");
        }

        // A log file service version DiscUtils does not know.
        using (var volume = With((w, r) =>
        {
            w.Finish(r);
            var page = new byte[TestJournalWriter.PageSize];
            w.Storage.Read(0, page, 0, page.Length);
            Assert.True(JournalUtilities.RemoveFixups(page, 0, page.Length));
            EndianUtilities.WriteBytesLittleEndian((ushort)3, page, 0x1C);
            JournalUtilities.ApplyFixups(page, 0, page.Length);
            w.Storage.Write(0, page, 0, page.Length);
            w.Storage.Write(TestJournalWriter.PageSize, page, 0, page.Length);
        }))
        {
            AssertRefused(volume, "version 3");
        }

        // An implausible restart area.
        using (var volume = With((w, r) =>
        {
            w.Finish(r);
            var page = new byte[TestJournalWriter.PageSize];
            w.Storage.Read(0, page, 0, page.Length);
            Assert.True(JournalUtilities.RemoveFixups(page, 0, page.Length));
            EndianUtilities.WriteBytesLittleEndian(8u, page, 0x30 + 0x10);
            JournalUtilities.ApplyFixups(page, 0, page.Length);
            w.Storage.Write(0, page, 0, page.Length);
            w.Storage.Write(TestJournalWriter.PageSize, page, 0, page.Length);
        }))
        {
            AssertRefused(volume, "implausible restart area");
        }

        // The restart record the restart area points at is not in the log.
        using (var volume = With((w, r) => w.Finish(r + 0x100)))
        {
            AssertRefused(volume, "restart record");
        }

        // A record without a transaction.
        using (var volume = With((w, r) =>
        {
            w.Append(new TestLogRecord { Redo = NtfsLogOperation.Noop, Undo = NtfsLogOperation.Noop, TransactionId = 0 });
            w.Finish(r);
        }))
        {
            AssertRefused(volume, "no transaction");
        }

        // A record naming more clusters than its dirty page holds.
        using (var volume = With((w, r) =>
        {
            var record = RawWrite(100, new byte[8], new byte[8]);
            record.Lcns = [100, 101];
            w.Append(record);
            w.Finish(r);
        }))
        {
            AssertRefused(volume, "beyond its dirty page");
        }

        // An undo chain that loops, points at another transaction, or at nothing.
        foreach (var (link, reason) in new (Func<ulong, ulong>, string)[]
                 {
                     (self => self, "loops"),
                     (self => self - 0x10, "belongs to"),
                     (self => self + 0x1000, "not in the log"),
                 })
        {
            using var volume = With((w, r) =>
            {
                var other = w.Append(new TestLogRecord { Redo = NtfsLogOperation.Noop, Undo = NtfsLogOperation.Noop, TransactionId = 0x68 });
                var record = RawWrite(100, new byte[8], new byte[8]);
                var lsn = w.LsnOf(w.LsnToOffsetForTest(other) + 0x50, 1);
                record.UndoNextLsn = reason == "belongs to" ? other : link(lsn);
                var actual = w.Append(record);
                Assert.Equal(lsn, actual);
                w.Finish(r);
            });
            AssertRefused(volume, reason);
        }

        // A change to an attribute that is not open.
        using (var volume = With((w, r) =>
        {
            w.Append(RawWrite(100, new byte[8], new byte[8], attribute: 0x200));
            w.Append(Forget());
            w.Finish(r);
        }))
        {
            AssertRefused(volume, "not open");
        }

        // A bit range record that is too short.
        using (var volume = With((w, r) =>
        {
            w.Append(new TestLogRecord { Redo = NtfsLogOperation.SetBitsInNonresidentBitMap, Undo = NtfsLogOperation.Noop, TransactionId = Tx, RedoData = new byte[4], TargetAttribute = Data, TargetVcn = 100, Lcns = [100] });
            w.Append(Forget());
            w.Finish(r);
        }))
        {
            AssertRefused(volume, "bit range");
        }

        // A whole index buffer image that does not fit the buffer.
        using (var volume = With((w, r) =>
        {
            w.Append(new TestLogRecord { Redo = NtfsLogOperation.UpdateNonresidentValue, Undo = NtfsLogOperation.Noop, TransactionId = Tx, RedoData = new byte[64], TargetAttribute = Index, TargetVcn = 100, RecordOffset = 4090, Lcns = [100] });
            w.Append(Forget());
            w.Finish(r);
        }))
        {
            AssertRefused(volume, "past its index buffer");
        }

        // Clusters that are not on the volume.
        using (var volume = With((w, r) =>
        {
            w.Append(RawWrite(1L << 40, new byte[8], new byte[8]));
            w.Append(Forget());
            w.Finish(r);
        }))
        {
            AssertRefused(volume, "not on the volume");
        }
    }

    [Fact]
    public void DamagedPages_AreRefused_ExceptARecordThatIsRebuiltOrAHole()
    {
        var scenario = JournalScenario.Create(ntfs => ntfs.CreateDirectory(@"pages"));
        using var probe = JournalScenario.Copy(scenario.Before);
        var lcn = FreeCluster(probe);

        MemoryStream With(byte[] page, TestLogRecord record)
        {
            var volume = JournalScenario.Copy(scenario.Before);
            volume.Position = lcn * 4096;
            volume.Write(page, 0, page.Length);
            var writer = new TestJournalWriter(volume);
            var restart = writer.Checkpoint(Attributes);
            writer.Append(record);
            writer.Append(Forget());
            writer.Finish(restart);
            return volume;
        }

        TestLogRecord On(NtfsLogOperation op, ushort attribute, byte[] data, int recordOffset = 0, int attributeOffset = 0) =>
            new() { Redo = op, Undo = NtfsLogOperation.Noop, TransactionId = Tx, RedoData = data, TargetAttribute = attribute, TargetVcn = lcn, RecordOffset = recordOffset, AttributeOffset = attributeOffset, Lcns = [lcn] };

        var zeros = new byte[4096];

        // A change to a file record that is not one.
        using (var volume = With(zeros, On(NtfsLogOperation.UpdateResidentValue, Mft, new byte[8], 0x38, 0x18)))
        {
            AssertRefused(volume, "damaged");
        }

        // A change to an index buffer that is not one.
        using (var volume = With(zeros, On(NtfsLogOperation.UpdateFileNameAllocation, Index, new byte[0x38], 0, 0x40)))
        {
            AssertRefused(volume, "damaged");
        }

        // A page chkdsk marked bad.
        var baad = new byte[4096];
        EndianUtilities.WriteBytesLittleEndian(JournalUtilities.SignatureBaad, baad, 0);
        using (var volume = With(baad, On(NtfsLogOperation.UpdateResidentValue, Mft, new byte[8], 0x38, 0x18)))
        {
            AssertRefused(volume, "bad");
        }

        // A hole is skipped.
        var hole = new byte[4096];
        EndianUtilities.WriteBytesLittleEndian(JournalUtilities.SignatureHole, hole, 0);
        using (var volume = With(hole, On(NtfsLogOperation.UpdateResidentValue, Mft, new byte[8], 0x38, 0x18)))
        {
            Assert.Equal(NtfsJournalStatus.NothingToReplay, NtfsJournal.Analyze(volume).Status);
        }

        // A file record that was never written is rebuilt from its InitializeFileRecordSegment.
        var root = MftRecord(probe, 5);
        var image = ReadLogical(probe, (root.Lcn * 4096) + (root.BlockOffset * 512), 1024);
        using (var volume = With(zeros, On(NtfsLogOperation.InitializeFileRecordSegment, Mft, image.AsSpan(0, (int)EndianUtilities.ToUInt32LittleEndian(image, 0x18)).ToArray())))
        {
            NtfsJournal.Replay(volume);
            var rebuilt = ReadLogical(volume, lcn * 4096, 1024);
            Assert.Equal(JournalUtilities.SignatureFile, EndianUtilities.ToUInt32LittleEndian(rebuilt, 0));
        }
    }

    [Fact]
    public void Replay_NeedsAWritableStream_AndInMemoryReplayOfACleanVolumeChangesNothing()
    {
        var scenario = JournalScenario.Create(ntfs => ntfs.CreateDirectory(@"stream"));
        using var volume = JournalScenario.Copy(scenario.Before);
        scenario.WriteJournal(volume, committed: true);
        using var readOnly = new MemoryStream(volume.ToArray(), writable: false);
        Assert.Throws<IOException>(() => NtfsJournal.Replay(readOnly));

        using var clean = new MemoryStream(scenario.After, writable: false);
        var info = NtfsJournal.ReplayInMemory(clean, out var replayed);
        Assert.Equal(NtfsJournalStatus.Clean, info.Status);
        Assert.Null(replayed);
        Assert.Contains("Clean", info.ToString());
    }

    [Fact]
    public void Fixups_AreChecked_AndTheSequenceNumberWraps()
    {
        var record = new byte[1024];
        EndianUtilities.WriteBytesLittleEndian(JournalUtilities.SignatureFile, record, 0);
        EndianUtilities.WriteBytesLittleEndian((ushort)0x30, record, 4);
        EndianUtilities.WriteBytesLittleEndian((ushort)3, record, 6);
        EndianUtilities.WriteBytesLittleEndian((ushort)0xFFFE, record, 0x30);
        record[0x1FE] = 0xAB;
        JournalUtilities.ApplyFixups(record, 0, record.Length);
        Assert.Equal(1, EndianUtilities.ToUInt16LittleEndian(record, 0x30));
        Assert.Equal(1, EndianUtilities.ToUInt16LittleEndian(record, 0x1FE));

        var torn = (byte[])record.Clone();
        torn[0x3FE] = 9;
        Assert.False(JournalUtilities.RemoveFixups(torn, 0, torn.Length));
        Assert.True(JournalUtilities.RemoveFixups(record, 0, record.Length));
        Assert.Equal(0xAB, record[0x1FE]);

        Assert.Throws<JournalException>(() => JournalUtilities.ApplyFixups(new byte[1024], 0, 1024));
        Assert.False(JournalUtilities.HasFixupLayout(new byte[1000], 0, 1000));
        Assert.Throws<JournalException>(() => JournalUtilities.ReadExactly(new MemoryStream(new byte[10]), 0, new byte[20], 0, 20));
    }
}
