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

using DiscUtils.Internal;
using DiscUtils.Streams;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;

namespace DiscUtils.Ntfs;

internal class Index : IDisposable
{
    private readonly ObjectCache<long, IndexBlock> _blockCache;
    protected BiosParameterBlock _bpb;

    private readonly IComparer<byte[]> _comparer;
    protected File _file;
    private Bitmap _indexBitmap;
    protected string _name;

    private readonly IndexRoot _root;
    private readonly IndexNode _rootNode;

    public Index(File file, string name, BiosParameterBlock bpb, UpperCase upCase)
    {
        _file = file;
        _name = name;
        _bpb = bpb;
        IsFileIndex = name == "$I30";

        _blockCache = new ObjectCache<long, IndexBlock>();

        var stream = _file.GetStream(AttributeType.IndexRoot, _name)
            ?? throw new FileNotFoundException($"Missing stream '{_name}' in file {file.MftReference.MftIndex} ('{file.BestName}')", $"{file.BestName}:{name}");

        _root = stream.GetContent<IndexRoot>();
        _comparer = _root.GetCollator(upCase);

        using (var s = _file.OpenStream(AttributeType.IndexRoot, _name, FileAccess.Read))
        {
            byte[] allocated = null;

            var buffer = s.Length <= 1024
                ? stackalloc byte[(int)s.Length]
                : (allocated = ArrayPool<byte>.Shared.Rent((int)s.Length)).AsSpan(0, (int)s.Length);

            try
            {
                s.ReadExactly(buffer);

                _rootNode = new IndexNode(WriteRootNodeToDisk, 0, this, true, buffer[IndexRoot.HeaderOffset..]);

            }
            finally
            {
                if (allocated is not null)
                {
                    ArrayPool<byte>.Shared.Return(allocated);
                }
            }
            // Give the attribute some room to breathe, so long as it doesn't squeeze others out
            // BROKEN, BROKEN, BROKEN - how to figure this out?  Query at the point of adding entries to the root node?
            _rootNode.TotalSpaceAvailable += _file.MftRecordFreeSpace(AttributeType.IndexRoot, _name) - 100;
        }

        if (_file.StreamExists(AttributeType.IndexAllocation, _name))
        {
            AllocationStream = _file.OpenStream(AttributeType.IndexAllocation, _name, FileAccess.ReadWrite);
        }

        if (_file.StreamExists(AttributeType.Bitmap, _name))
        {
            _indexBitmap = new Bitmap(_file.OpenStream(AttributeType.Bitmap, _name, FileAccess.ReadWrite),
                long.MaxValue);
        }
    }

    private Index(AttributeType attrType, AttributeCollationRule collationRule, File file, string name,
                  BiosParameterBlock bpb, UpperCase upCase)
    {
        _file = file;
        _name = name;
        _bpb = bpb;
        IsFileIndex = name == "$I30";

        _blockCache = new ObjectCache<long, IndexBlock>();

        _file.CreateStream(AttributeType.IndexRoot, _name);

        _root = new IndexRoot
        {
            AttributeType = (uint)attrType,
            CollationRule = collationRule,
            IndexAllocationSize = (uint)bpb.IndexBufferSize,
            RawClustersPerIndexRecord = bpb.RawIndexBufferSize
        };

        _comparer = _root.GetCollator(upCase);

        _rootNode = new IndexNode(WriteRootNodeToDisk, 0, this, true, 32);
    }

    internal Stream AllocationStream { get; private set; }

    public int Count
    {
        get
        {
            var i = 0;
            foreach (var entry in Entries)
            {
                ++i;
            }

            return i;
        }
    }

    public IEnumerable<KeyValuePair<byte[], byte[]>> Entries
    {
        get
        {
            foreach (var entry in Enumerate(_rootNode))
            {
                yield return new KeyValuePair<byte[], byte[]>(entry.KeyBuffer, entry.DataBuffer);
            }
        }
    }

    internal uint IndexBufferSize => _root.IndexAllocationSize;

    internal bool IsFileIndex { get; }

    public byte[] this[byte[] key]
    {
        get
        {
            if (TryGetValue(key, out var value))
            {
                return value;
            }

            throw new KeyNotFoundException();
        }

        set
        {
            _rootNode.TotalSpaceAvailable = _rootNode.CalcSize() +
                                            _file.MftRecordFreeSpace(AttributeType.IndexRoot, _name);
            if (_rootNode.TryFindEntry(key, out var oldEntry, out var node))
            {
                node.UpdateEntry(key, value);
            }
            else
            {
                _rootNode.AddEntry(key, value);
            }
        }
    }

    public void Dispose()
    {
        if (_indexBitmap != null)
        {
            _indexBitmap.Dispose();
            _indexBitmap = null;
        }
    }

    public static void Create(AttributeType attrType, AttributeCollationRule collationRule, File file, string name)
    {
        var idx = new Index(attrType, collationRule, file, name, file.Context.BiosParameterBlock,
            file.Context.UpperCase);

        idx.WriteRootNodeToDisk();
    }

    public IEnumerable<KeyValuePair<byte[], byte[]>> FindAll(IComparable<byte[]> query, bool validate = false)
    {
        if (validate && (!IsFileIndex || _root.AttributeType != (uint)AttributeType.FileName ||
            _root.CollationRule != AttributeCollationRule.Filename || _root.IndexAllocationSize != _bpb.IndexBufferSize ||
            _root.RawClustersPerIndexRecord == 0))
        {
            throw new InvalidDataException("The directory index root has an invalid format");
        }

        foreach (var entry in FindAllIn(query, _rootNode, validate, depth: 0))
        {
            yield return new KeyValuePair<byte[], byte[]>(entry.KeyBuffer, entry.DataBuffer);
        }
    }

    public bool ContainsKey(byte[] key)
    {
        return TryGetValue(key, out var value);
    }

    public bool Remove(byte[] key)
    {
        _rootNode.TotalSpaceAvailable = _rootNode.CalcSize() +
                                        _file.MftRecordFreeSpace(AttributeType.IndexRoot, _name);

        var found = _rootNode.RemoveEntry(key, out var overflowEntry);
        if (overflowEntry != null)
        {
            throw new IOException("Error removing entry, root overflowed");
        }

        return found;
    }

    public bool TryGetValue(byte[] key, out byte[] value)
    {

        if (_rootNode.TryFindEntry(key, out var entry, out var node))
        {
            value = entry.DataBuffer;
            return true;
        }

        value = null;
        return false;
    }

    internal static string EntryAsString(IndexEntry entry, string fileName, string indexName)
    {
        IByteArraySerializable keyValue = null;
        IByteArraySerializable dataValue = null;

        // Try to guess the type of data in the key and data fields from the filename and index name
        if (indexName == "$I30")
        {
            keyValue = new FileNameRecord();
            dataValue = new FileRecordReference();
        }
        else if (fileName == "$ObjId" && indexName == "$O")
        {
            keyValue = new ObjectIds.IndexKey();
            dataValue = new ObjectIdRecord();
        }
        else if (fileName == "$Reparse" && indexName == "$R")
        {
            keyValue = new ReparsePoints.Key();
            dataValue = new ReparsePoints.Data();
        }
        else if (fileName == "$Quota")
        {
            if (indexName == "$O")
            {
                keyValue = new Quotas.OwnerKey();
                dataValue = new Quotas.OwnerRecord();
            }
            else if (indexName == "$Q")
            {
                keyValue = new Quotas.OwnerRecord();
                dataValue = new Quotas.QuotaRecord();
            }
        }
        else if (fileName == "$Secure")
        {
            if (indexName == "$SII")
            {
                keyValue = new SecurityDescriptors.IdIndexKey();
                dataValue = new SecurityDescriptors.IdIndexData();
            }
            else if (indexName == "$SDH")
            {
                keyValue = new SecurityDescriptors.HashIndexKey();
                dataValue = new SecurityDescriptors.IdIndexData();
            }
        }

        try
        {
            if (keyValue != null && dataValue != null)
            {
                keyValue.ReadFrom(entry.KeyBuffer);
                dataValue.ReadFrom(entry.DataBuffer);
                return $"{{{keyValue}-->{dataValue}}}";
            }
        }
        catch
        {
            return "{Parsing-Error}";
        }

        return "{Unknown-Index-Type}";
    }

    internal long IndexBlockVcnToPosition(long vcn)
    {
        if (vcn % _root.RawClustersPerIndexRecord != 0)
        {
            throw new NotSupportedException($"Unexpected vcn (not a multiple of clusters-per-index-record): vcn={vcn} rcpir={_root.RawClustersPerIndexRecord}");
        }

        if (_bpb.BytesPerCluster <= _root.IndexAllocationSize)
        {
            return vcn * _bpb.BytesPerCluster;
        }

        if (_root.RawClustersPerIndexRecord != 8)
        {
            throw new NotSupportedException(
                $"Unexpected RawClustersPerIndexRecord (multiple index blocks per cluster): {_root.RawClustersPerIndexRecord}");
        }

        return vcn / _root.RawClustersPerIndexRecord * _root.IndexAllocationSize;
    }

    internal bool ShrinkRoot()
    {
        if (_rootNode.Depose())
        {
            WriteRootNodeToDisk();
            _rootNode.TotalSpaceAvailable = _rootNode.CalcSize() +
                                            _file.MftRecordFreeSpace(AttributeType.IndexRoot, _name);
            return true;
        }

        return false;
    }

    internal IndexBlock GetSubBlock(IndexEntry parentEntry)
    {
        var block = _blockCache[parentEntry.ChildrenVirtualCluster];
        if (block == null)
        {
            block = new IndexBlock(this, false, parentEntry, _bpb);
            _blockCache[parentEntry.ChildrenVirtualCluster] = block;
        }

        return block;
    }

    internal IndexBlock AllocateBlock(IndexEntry parentEntry)
    {
        if (AllocationStream == null)
        {
            _file.CreateStream(AttributeType.IndexAllocation, _name);
            AllocationStream = _file.OpenStream(AttributeType.IndexAllocation, _name, FileAccess.ReadWrite);
        }

        if (_indexBitmap == null)
        {
            _file.CreateStream(AttributeType.Bitmap, _name);
            _indexBitmap = new Bitmap(_file.OpenStream(AttributeType.Bitmap, _name, FileAccess.ReadWrite),
                long.MaxValue);
        }

        var idx = _indexBitmap.AllocateFirstAvailable(0);

        parentEntry.ChildrenVirtualCluster = idx *
                                             MathUtilities.Ceil(_bpb.IndexBufferSize,
                                                 _bpb.SectorsPerCluster * _bpb.BytesPerSector);
        parentEntry.Flags |= IndexEntryFlags.Node;

        var block = IndexBlock.Initialize(this, false, parentEntry, _bpb);
        _blockCache[parentEntry.ChildrenVirtualCluster] = block;
        return block;
    }

    internal void FreeBlock(long vcn)
    {
        var idx = vcn / MathUtilities.Ceil(_bpb.IndexBufferSize, _bpb.SectorsPerCluster * _bpb.BytesPerSector);
        _indexBitmap.MarkAbsent(idx);
        _blockCache.Remove(vcn);
    }

    internal int Compare(byte[] x, byte[] y)
    {
        return _comparer.Compare(x, y);
    }

    internal void Dump(TextWriter writer, string prefix)
    {
        NodeAsString(writer, prefix, _rootNode, "R");
    }

    protected IEnumerable<IndexEntry> Enumerate(IndexNode node)
    {
        foreach (var focus in node.Entries)
        {
            if ((focus.Flags & IndexEntryFlags.Node) != 0)
            {
                var block = GetSubBlock(focus);
                foreach (var subEntry in Enumerate(block.Node))
                {
                    yield return subEntry;
                }
            }

            if ((focus.Flags & IndexEntryFlags.End) == 0)
            {
                yield return focus;
            }
        }
    }

    private IEnumerable<IndexEntry> FindAllIn(IComparable<byte[]> query, IndexNode node, bool validate, int depth)
    {
        if (validate)
        {
            ValidateLookupNode(node, depth);
        }

        foreach (var focus in node.Entries)
        {
            var searchChildren = true;
            var matches = false;
            var keepIterating = true;

            if ((focus.Flags & IndexEntryFlags.End) == 0)
            {
                var compVal = query.CompareTo(focus.KeyBuffer);
                if (compVal == 0)
                {
                    matches = true;
                }
                else if (compVal > 0)
                {
                    searchChildren = false;
                }
                else if (compVal < 0)
                {
                    keepIterating = false;
                }
            }

            if (searchChildren && (focus.Flags & IndexEntryFlags.Node) != 0)
            {
                if (validate)
                {
                    ValidateLookupChild(focus.ChildrenVirtualCluster);
                }

                var block = GetSubBlock(focus);
                foreach (var entry in FindAllIn(query, block.Node, validate, depth + 1))
                {
                    yield return entry;
                }
            }

            if (matches)
            {
                yield return focus;
            }

            if (!keepIterating)
            {
                yield break;
            }
        }
    }

    private void ValidateLookupNode(IndexNode node, int depth)
    {
        var header = node.Header;
        if (depth >= 64 || header.OffsetToFirstEntry < IndexHeader.Size ||
            header.OffsetToFirstEntry + (long)node.CalcEntriesSize() != header.TotalSizeOfEntries ||
            header.TotalSizeOfEntries > header.AllocatedSizeOfEntries)
        {
            throw new InvalidDataException("The directory index has invalid node bounds or depth");
        }

        byte[] previousKey = null;
        foreach (var entry in node.Entries)
        {
            if ((entry.Flags & ~(IndexEntryFlags.Node | IndexEntryFlags.End)) != 0)
            {
                throw new InvalidDataException("The directory index has an invalid entry flag");
            }

            if ((entry.Flags & IndexEntryFlags.End) != 0)
            {
                return;
            }

            var key = entry.KeyBuffer;
            if (key.Length < 0x42 || key.Length != 0x42 + 2 * key[0x40] ||
                (previousKey != null && _comparer.Compare(previousKey, key) > 0))
            {
                throw new InvalidDataException("The directory index has an invalid or out-of-order filename");
            }

            previousKey = key;
        }

        throw new InvalidDataException("The directory index node has no end entry");
    }

    private void ValidateLookupChild(long vcn)
    {
        if (vcn < 0 || vcn > long.MaxValue / Math.Max(_bpb.BytesPerCluster, (long)IndexBufferSize) ||
            AllocationStream == null || _indexBitmap == null)
        {
            throw new InvalidDataException("The directory index has an invalid child reference");
        }

        var position = IndexBlockVcnToPosition(vcn);
        if (position > AllocationStream.Length - IndexBufferSize || position % IndexBufferSize != 0 ||
            !_indexBitmap.IsPresent(position / IndexBufferSize))
        {
            throw new InvalidDataException("The directory index child is outside the allocated index blocks");
        }
    }

    private void WriteRootNodeToDisk()
    {
        _rootNode.Header.AllocatedSizeOfEntries = (uint)_rootNode.CalcSize();
        var bufferSize = (int)(_rootNode.Header.AllocatedSizeOfEntries + _root.Size);
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            _root.WriteTo(buffer);
            _rootNode.WriteTo(buffer.AsSpan(_root.Size));
            using var s = _file.OpenStream(AttributeType.IndexRoot, _name, FileAccess.Write);
            s.Position = 0;
            s.Write(buffer, 0, bufferSize);
            s.SetLength(s.Position);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private void NodeAsString(TextWriter writer, string prefix, IndexNode node, string id)
    {
        writer.WriteLine($"{prefix}{id}:");
        foreach (var entry in node.Entries)
        {
            if ((entry.Flags & IndexEntryFlags.End) != 0)
            {
                writer.WriteLine($"{prefix}      E");
            }
            else
            {
                writer.WriteLine($"{prefix}      {EntryAsString(entry, _file.BestName, _name)}");
            }

            if ((entry.Flags & IndexEntryFlags.Node) != 0)
            {
                NodeAsString(writer, $"{prefix}        ", GetSubBlock(entry).Node,
                    $":i{entry.ChildrenVirtualCluster}");
            }
        }
    }
}