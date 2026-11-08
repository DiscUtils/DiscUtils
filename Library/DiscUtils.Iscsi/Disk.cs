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
using System.IO;
using DiscUtils.Partitions;
using DiscUtils.Streams;

namespace DiscUtils.Iscsi;

/// <summary>
/// Represents a disk accessed via iSCSI.
/// </summary>
public class Disk : VirtualDisk
{
    private readonly FileAccess _access;
    private readonly long _lun;
    private readonly Session _session;

    private DiskStream _stream;

    private AligningStream _aligningStream;

    internal Disk(Session session, long lun, FileAccess access)
    {
        _session = session;
        _lun = lun;
        _access = access;
    }

    /// <summary>
    /// Gets the size of the disk's logical blocks (in bytes).
    /// </summary>
    public override int BlockSize
    {
        get
        {
            _stream ??= new DiskStream(_session, _lun, _access);

            return _stream.BlockSize;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the layer data is opened for writing.
    /// </summary>
    public override bool CanWrite => _access.HasFlag(FileAccess.Write);

    /// <summary>
    /// The capacity of the disk.
    /// </summary>
    public override long Capacity
    {
        get
        {
            _stream ??= new DiskStream(_session, _lun, _access);

            return _stream.Length;
        }
    }

    /// <summary>
    /// Gets a stream that provides access to the disk's content.
    /// </summary>
    public override SparseStream Content
    {
        get
        {
            _stream ??= new DiskStream(_session, _lun, _access);

            _aligningStream ??= new AligningStream(_stream, Ownership.None, _stream.BlockSize);

            return _aligningStream;
        }
    }

    /// <summary>
    /// Gets the type of disk represented by this object.
    /// </summary>
    public override VirtualDiskClass DiskClass => VirtualDiskClass.HardDisk;

    /// <summary>
    /// Gets information about the type of disk.
    /// </summary>
    /// <remarks>This property provides access to meta-data about the disk format, for example whether the
    /// BIOS geometry is preserved in the disk file.</remarks>
    public override VirtualDiskTypeInfo DiskTypeInfo => new()
    {
        Name = "iSCSI",
        Variant = string.Empty,
        CanBeHardDisk = true,
        DeterministicGeometry = false,
        PreservesBiosGeometry = false,
        CalcGeometry = DiscUtils.Geometry.FromCapacity
    };

    /// <summary>
    /// The Geometry of the disk.
    /// </summary>
    public override Geometry? Geometry
    {
        get
        {
            // We detect the geometry (which will return a sensible default if the disk has no partitions).
            // We don't rely on asking the iSCSI target for the geometry because frequently values are returned
            // that are not valid as BIOS disk geometries.
            var stream = Content;
            var pos = stream.Position;

            var result = BiosPartitionTable.DetectGeometry(stream);

            stream.Position = pos;

            return result;
        }
    }

    /// <summary>
    /// Gets the disk layers that constitute the disk.
    /// </summary>
    public override IEnumerable<VirtualDiskLayer> Layers
    {
        get { yield break; }
    }

    /// <summary>
    /// Create a new differencing disk, possibly within an existing disk.
    /// </summary>
    /// <param name="fileSystem">The file system to create the disk on.</param>
    /// <param name="path">The path (or URI) for the disk to create.</param>
    /// <returns>The newly created disk.</returns>
    public override VirtualDisk CreateDifferencingDisk(DiscFileSystem fileSystem, string path)
    {
        throw new NotSupportedException("Differencing disks not supported for iSCSI disks");
    }

    /// <summary>
    /// Create a new differencing disk.
    /// </summary>
    /// <param name="path">The path (or URI) for the disk to create.</param>
    /// <param name="useAsync">Underlying files will be opened optimized for async use.</param>
    /// <returns>The newly created disk.</returns>
    public override VirtualDisk CreateDifferencingDisk(string path, bool useAsync = false)
    {
        throw new NotSupportedException("Differencing disks not supported for iSCSI disks");
    }

    /// <summary>
    /// Disposes of this instance. Like an operating system that unmounts a disk, it first asks the target to put
    /// the writes it acknowledged on stable storage.
    /// </summary>
    /// <param name="disposing"><c>true</c> if running inside Dispose().</param>
    /// <remarks>
    /// A failure of that last flush is not reported here (the session may already be gone). Call
    /// <c>Content.Flush()</c> before disposing to find out whether the target stored the writes.
    /// </remarks>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                _stream?.Flush(flushToDisk: true);
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException)
            {
            }
        }

        base.Dispose(disposing);
    }
}
