//
// Copyright (c) 2026, DiscUtils contributors
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
using System.IO;
using System.Linq;
using DiscUtils.Streams;
using DiscUtils.Streams.Compatibility;
using static DiscUtils.Ntfs.Journal.JournalUtilities;

namespace DiscUtils.Ntfs.Journal;

internal enum NtfsHibernationEvidence
{
    NoLocalEvidence,
    Active,
    Indeterminate,
}

/// <summary>Checks the local hibernation file without mounting for writes or running journal recovery.</summary>
internal static class NtfsHibernation
{
    private const int HeaderSize = 4096;
    private const string FileName = "hiberfil.sys";

    public static (NtfsHibernationEvidence Evidence, string Detail) Probe(Stream volume, JournalVolumeLayout layout,
        bool allowMftMirror = false)
    {
        try
        {
            using var readOnly = new JournalReadOnlyStream(volume);
            var boot = new byte[512];
            ReadExactly(readOnly, 0, boot, 0, boot.Length);
            var context = new NtfsContext
            {
                RawStream = readOnly,
                ReadOnly = true,
                BiosParameterBlock = BiosParameterBlock.FromBytes(boot),
                Options = new NtfsOptions(),
            };

            using var mft = new MasterFileTable(context);
            context.Mft = mft;
            mft.Initialize(new File(context, ReadMftBootstrap(readOnly, layout, allowMftMirror)));
            var root = ReadRootDirectory(context);
            var entry = root.GetEntryByName(FileName, requireUnique: true);
            if (entry == null)
            {
                return (NtfsHibernationEvidence.NoLocalEvidence, "No root hiberfil.sys was found");
            }

            var record = mft.GetRecord(entry.Value.Reference);
            if (record == null || record.BaseFile.Value != 0 ||
                record.SequenceNumber != entry.Value.Reference.SequenceNumber ||
                (record.Flags & (FileRecordFlags.InUse | FileRecordFlags.IsDirectory)) != FileRecordFlags.InUse)
            {
                throw new InvalidDataException("The root hiberfil.sys entry does not reference an allocated file record");
            }

            var file = new File(context, record);
            if (file.GetAttribute(AttributeType.ReparsePoint, null) != null ||
                !file.GetStreams(AttributeType.FileName, null).Any(stream =>
                {
                    if (stream.Attribute.IsNonResident || stream.Attribute.Length is < 0x42 or > 0x240)
                    {
                        throw new InvalidDataException("hiberfil.sys has an invalid file name attribute");
                    }

                    var name = stream.GetContent<FileNameRecord>();
                    return name.ParentDirectory == root.MftReference &&
                        string.Equals(name.FileName, FileName, StringComparison.OrdinalIgnoreCase);
                }))
            {
                throw new InvalidDataException("The root hiberfil.sys entry has an inconsistent file record");
            }

            var data = file.GetAttribute(AttributeType.Data, null);
            if (data?.PrimaryRecord is not NonResidentAttributeRecord first || first.Flags != AttributeFlags.None ||
                first.StartVcn != 0 || first.DataLength < HeaderSize || first.InitializedDataLength < HeaderSize ||
                first.InitializedDataLength > first.DataLength || first.DataLength > first.AllocatedLength ||
                first.AllocatedLength % layout.BytesPerCluster != 0 || first.AllocatedLength / layout.BytesPerCluster > layout.TotalClusters)
            {
                throw new InvalidDataException("hiberfil.sys has no complete initialized header in ordinary file data");
            }

            var header = ReadHeader(readOnly, layout, first);
            var signature = header.AsSpan(0, 4);
            if (signature.SequenceEqual("hibr"u8) || signature.SequenceEqual("HIBR"u8) || signature.SequenceEqual("HORM"u8))
            {
                return (NtfsHibernationEvidence.Active, "hiberfil.sys has an active Windows hibernation signature");
            }

            if (signature.SequenceEqual("wake"u8) || signature.SequenceEqual("WAKE"u8))
            {
                return (NtfsHibernationEvidence.NoLocalEvidence, "The local hiberfil.sys has an invalidated resume signature");
            }

            if (Array.TrueForAll(header, b => b == 0))
            {
                return (NtfsHibernationEvidence.NoLocalEvidence, "The local hiberfil.sys header is zeroed");
            }

            if (signature.SequenceEqual("rstr"u8) || signature.SequenceEqual("RSTR"u8))
            {
                return (NtfsHibernationEvidence.Indeterminate, "hiberfil.sys has a resume-in-progress signature");
            }

            return (NtfsHibernationEvidence.Indeterminate, "hiberfil.sys has an unrecognized nonzero header");
        }
        catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or IndexOutOfRangeException or
                                  OverflowException or InvalidOperationException or NotSupportedException or NotImplementedException or
                                  NullReferenceException)
        {
            return (NtfsHibernationEvidence.Indeterminate, e.Message);
        }
    }

    private static FileRecord ReadMftBootstrap(Stream volume, JournalVolumeLayout layout, bool allowMftMirror)
    {
        var bytes = new byte[layout.MftRecordSize];
        ReadRecord(layout.MftCluster);
        if (allowMftMirror && !RemoveFixups((byte[])bytes.Clone(), 0, bytes.Length))
        {
            // Only the bootstrap record comes from the mirror. The lookup still reads the original volume.
            ReadRecord(layout.MftMirrorCluster);
        }

        var record = new FileRecord(layout.BytesPerSector);
        record.FromBytes(bytes);
        if (record.MasterFileTableIndex != MasterFileTable.MftIndex || record.BaseFile.Value != 0 ||
            (record.Flags & FileRecordFlags.InUse) == 0 ||
            record.GetAttribute(AttributeType.Data) == null || record.GetAttribute(AttributeType.Bitmap) == null)
        {
            throw new InvalidDataException("The $MFT bootstrap record is invalid");
        }

        return record;

        void ReadRecord(long lcn)
        {
            if (!layout.ContainsClusters(lcn, (bytes.Length + (long)layout.BytesPerCluster - 1) / layout.BytesPerCluster))
            {
                throw new InvalidDataException("The $MFT bootstrap record is outside the volume");
            }

            ReadExactly(volume, lcn * layout.BytesPerCluster, bytes, 0, bytes.Length);
            if (U32(bytes, 0) != SignatureFile || !HasFixupLayout(bytes, 0, bytes.Length))
            {
                throw new InvalidDataException("The $MFT bootstrap record is invalid");
            }
        }
    }

    private static Directory ReadRootDirectory(NtfsContext context)
    {
        var record = context.Mft.GetRecord(MasterFileTable.RootDirIndex, ignoreMagic: false);
        const FileRecordFlags rootFlags = FileRecordFlags.InUse | FileRecordFlags.IsDirectory;
        if (record == null || record.BaseFile.Value != 0 || (record.Flags & rootFlags) != rootFlags)
        {
            throw new InvalidDataException("The root directory record is unavailable");
        }

        var root = new Directory(context, record);
        if (root.GetAttribute(AttributeType.IndexRoot, "$I30") is not { IsNonResident: false, Flags: AttributeFlags.None })
        {
            throw new InvalidDataException("The root directory index is unavailable");
        }

        var upCaseRecord = context.Mft.GetRecord(MasterFileTable.UpCaseIndex, ignoreMagic: false);
        if (upCaseRecord == null || upCaseRecord.BaseFile.Value != 0 || (upCaseRecord.Flags & FileRecordFlags.InUse) == 0)
        {
            throw new InvalidDataException("The $UpCase record is unavailable");
        }

        var upCase = new File(context, upCaseRecord);
        if (upCase.GetAttribute(AttributeType.Data, null) is not { Flags: AttributeFlags.None } data ||
            data.Length is < 512 or > 131072 || (data.Length & 1) != 0 || data.PrimaryRecord.InitializedDataLength != data.Length)
        {
            throw new InvalidDataException("The $UpCase table is invalid");
        }

        context.UpperCase = new UpperCase(upCase);
        if (context.UpperCase.Compare(FileName, "HIBERFIL.SYS") != 0)
        {
            throw new InvalidDataException("The $UpCase table cannot compare the hibernation file name");
        }

        return root;
    }

    private static byte[] ReadHeader(Stream volume, JournalVolumeLayout layout, NonResidentAttributeRecord extent)
    {
        // A sparse or uninitialized range can read back as zeros without any bytes having been checked on disk.
        var header = new byte[HeaderSize];
        var done = 0;
        long lcn = 0;
        long vcn = 0;
        foreach (var run in extent.DataRuns)
        {
            lcn = checked(lcn + run.RunOffset);
            if (run.IsSparse || !layout.ContainsClusters(lcn, run.RunLength) ||
                vcn > extent.LastVcn || run.RunLength - 1 > extent.LastVcn - vcn)
            {
                throw new InvalidDataException("hiberfil.sys has an invalid header cluster mapping");
            }

            var count = (int)Math.Min(run.RunLength * layout.BytesPerCluster, header.Length - done);
            ReadExactly(volume, lcn * layout.BytesPerCluster, header, done, count);
            done += count;
            vcn += run.RunLength;
            if (done == header.Length)
            {
                return header;
            }
        }

        throw new InvalidDataException("hiberfil.sys does not map a complete header in its first extent");
    }
}

/// <summary>A borrowed volume stream that cannot write, flush or close its source.</summary>
internal sealed class JournalReadOnlyStream(Stream volume) : SparseStream.ReadOnlySparseStream
{
    public override bool CanRead => volume.CanRead;

    public override bool CanSeek => volume.CanSeek;

    public override long Length => volume.Length;

    public override long Position
    {
        get => volume.Position;
        set => volume.Position = value;
    }

    public override IEnumerable<StreamExtent> Extents => volume is SparseStream sparse ? sparse.Extents : [new StreamExtent(0, Length)];

    public override long? GetPositionInBaseStream(Stream baseStream, long virtualPosition) =>
        ReferenceEquals(this, baseStream) ? virtualPosition : volume.GetPositionInBaseStream(baseStream, virtualPosition);

    public override long Seek(long offset, SeekOrigin origin) => volume.Seek(offset, origin);

    public override int Read(byte[] buffer, int offset, int count) => volume.Read(buffer, offset, count);

    public override int Read(Span<byte> buffer) => volume.Read(buffer);
}
