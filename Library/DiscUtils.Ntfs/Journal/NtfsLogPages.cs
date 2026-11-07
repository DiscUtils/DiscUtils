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
using static DiscUtils.Ntfs.Journal.JournalUtilities;

namespace DiscUtils.Ntfs.Journal;

internal enum PageKind
{
    FileRecord,
    IndexBuffer,
    Raw,
}

/// <summary>
/// A file record or index buffer being replayed, in its logical form (update sequence array removed).
/// </summary>
internal sealed class LogicalPage
{
    public PageKind Kind { get; set; }

    /// <summary>Where every byte of the page lives: the page's clusters and the offset of the page in the first.</summary>
    public long[] Lcns { get; set; }

    public int StartInFirstCluster { get; set; }

    public byte[] Data { get; set; }

    /// <summary>The signature and update sequence array are intact (or the replay made them so).</summary>
    public bool Valid { get; set; }

    public bool Dirty { get; set; }

    public uint Signature => U32(Data, 0);

    public ulong Lsn
    {
        get => U64(Data, 0x08);
        set => Put64(Data, 0x08, value);
    }
}

/// <summary>
/// All volume pages the replay reads and changes. Nothing is written until <see cref="Commit"/>, so a refused
/// replay leaves the volume untouched, and the same set of changes serves a write to disk or a read-only overlay.
/// </summary>
internal sealed class NtfsLogPages
{
    private readonly Stream _volume;
    private readonly JournalVolumeLayout _layout;
    private readonly Dictionary<long, LogicalPage> _logical = [];
    private readonly Dictionary<long, byte[]> _rawClusters = [];
    private readonly Dictionary<long, byte[]> _rawOriginal = [];
    private readonly Dictionary<long, PageKind> _clusterUse = [];

    public NtfsLogPages(Stream volume, JournalVolumeLayout layout)
    {
        _volume = volume;
        _layout = layout;
    }

    public int ClusterSize => _layout.BytesPerCluster;

    /// <summary>File records, index buffers and plain clusters whose bytes the replay changes (set by <see cref="Commit"/>).</summary>
    public int ChangedPageCount { get; private set; }

    /// <summary>
    /// Gets a file record or index buffer of <paramref name="length"/> bytes that starts <paramref name="start"/>
    /// bytes into the clusters <paramref name="lcns"/>.
    /// </summary>
    public LogicalPage GetLogical(PageKind kind, long[] lcns, int start, int length)
    {
        var (spanLcns, startInFirst) = Span(lcns, start, length);
        var key = (spanLcns[0] * ClusterSize) + startInFirst;
        if (_logical.TryGetValue(key, out var page))
        {
            if (page.Kind != kind || page.Data.Length != length)
            {
                throw new JournalException($"The log uses the bytes at {key:X} both as {page.Kind} and as {kind}");
            }

            return page;
        }

        Claim(spanLcns, kind);
        var data = new byte[length];
        ReadSpan(spanLcns, startInFirst, data, 0, length);
        var expected = kind == PageKind.FileRecord ? SignatureFile : SignatureIndx;
        page = new LogicalPage
        {
            Kind = kind,
            Lcns = spanLcns,
            StartInFirstCluster = startInFirst,
            Data = data,
        };
        page.Valid = page.Signature == expected && RemoveFixups(data, 0, length);
        _logical[key] = page;
        return page;
    }

    /// <summary>
    /// Reads bytes of a non-resident attribute page that has no update sequence array (bitmaps, plain data).
    /// </summary>
    public void ReadRaw(long[] lcns, int start, byte[] buffer, int offset, int length)
    {
        var (spanLcns, startInFirst) = Span(lcns, start, length);
        Claim(spanLcns, PageKind.Raw);
        ReadSpan(spanLcns, startInFirst, buffer, offset, length);
    }

    public void WriteRaw(long[] lcns, int start, byte[] buffer, int offset, int length)
    {
        var (spanLcns, startInFirst) = Span(lcns, start, length);
        Claim(spanLcns, PageKind.Raw);
        var position = startInFirst;
        foreach (var lcn in spanLcns)
        {
            var cluster = RawCluster(lcn);
            var chunk = Math.Min(length, ClusterSize - position);
            Array.Copy(buffer, offset, cluster, position, chunk);
            offset += chunk;
            length -= chunk;
            position = 0;
            if (length == 0)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Turns every change into whole clusters: logical pages get a fresh update sequence array.
    /// </summary>
    public Dictionary<long, byte[]> Commit()
    {
        // Plain clusters are replayed unconditionally; only those whose bytes really changed are written.
        var clusters = new Dictionary<long, byte[]>();
        var changed = 0;
        foreach (var pair in _rawClusters)
        {
            if (!pair.Value.AsSpan().SequenceEqual(_rawOriginal[pair.Key]))
            {
                clusters[pair.Key] = pair.Value;
                changed++;
            }
        }

        foreach (var page in _logical.Values)
        {
            if (!page.Dirty)
            {
                continue;
            }

            changed++;

            var bytes = (byte[])page.Data.Clone();
            if (page.Valid)
            {
                ApplyFixups(bytes, 0, bytes.Length);
            }

            var position = page.StartInFirstCluster;
            var offset = 0;
            foreach (var lcn in page.Lcns)
            {
                if (!clusters.TryGetValue(lcn, out var cluster))
                {
                    cluster = new byte[ClusterSize];
                    ReadExactly(_volume, lcn * ClusterSize, cluster, 0, ClusterSize);
                    clusters[lcn] = cluster;
                }

                var chunk = Math.Min(bytes.Length - offset, ClusterSize - position);
                Array.Copy(bytes, offset, cluster, position, chunk);
                offset += chunk;
                position = 0;
                if (offset == bytes.Length)
                {
                    break;
                }
            }
        }

        ChangedPageCount = changed;
        return clusters;
    }

    private (long[] Lcns, int StartInFirst) Span(long[] lcns, int start, int length)
    {
        if (start < 0 || length <= 0)
        {
            throw new JournalException("The log addresses a page with a negative offset or length");
        }

        var firstIndex = start / ClusterSize;
        var lastIndex = (start + length - 1) / ClusterSize;
        if (lastIndex >= lcns.Length)
        {
            throw new JournalException("The log addresses bytes beyond the clusters it names");
        }

        var span = new long[lastIndex - firstIndex + 1];
        for (var i = 0; i < span.Length; i++)
        {
            var lcn = lcns[firstIndex + i];
            if (lcn <= 0 || lcn >= _layout.TotalClusters)
            {
                throw new JournalException($"The log names cluster {lcn}, which is not on the volume");
            }

            span[i] = lcn;
        }

        return (span, start % ClusterSize);
    }

    private void Claim(long[] lcns, PageKind kind)
    {
        foreach (var lcn in lcns)
        {
            if (_clusterUse.TryGetValue(lcn, out var existing) && existing != kind)
            {
                throw new JournalException($"The log uses cluster {lcn} both as {existing} and as {kind}");
            }

            _clusterUse[lcn] = kind;
        }
    }

    private byte[] RawCluster(long lcn)
    {
        if (!_rawClusters.TryGetValue(lcn, out var cluster))
        {
            cluster = new byte[ClusterSize];
            ReadExactly(_volume, lcn * ClusterSize, cluster, 0, ClusterSize);
            _rawClusters[lcn] = cluster;
            _rawOriginal[lcn] = (byte[])cluster.Clone();
        }

        return cluster;
    }

    private void ReadSpan(long[] lcns, int startInFirst, byte[] buffer, int offset, int length)
    {
        var position = startInFirst;
        foreach (var lcn in lcns)
        {
            var chunk = Math.Min(length, ClusterSize - position);
            if (_rawClusters.TryGetValue(lcn, out var cached))
            {
                Array.Copy(cached, position, buffer, offset, chunk);
            }
            else
            {
                ReadExactly(_volume, (lcn * ClusterSize) + position, buffer, offset, chunk);
            }

            offset += chunk;
            length -= chunk;
            position = 0;
            if (length == 0)
            {
                break;
            }
        }
    }
}
