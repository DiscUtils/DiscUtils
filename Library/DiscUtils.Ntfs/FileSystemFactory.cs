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
using DiscUtils.Vfs;
using LTRData.Extensions.Buffers;

namespace DiscUtils.Ntfs;

[VfsFileSystemFactory]
internal class FileSystemFactory : VfsFileSystemFactory
{
    public override IEnumerable<FileSystemInfo> Detect(Stream stream, VolumeInfo volume)
    {
        if (NtfsFileSystem.Detect(stream))
        {
            return SingleValueEnumerable.Get(new VfsFileSystemInfo("NTFS", "Microsoft NTFS", Open));
        }

        return [];
    }

    private NtfsFileSystem Open(Stream stream, VolumeInfo volumeInfo, FileSystemParameters parameters)
    {
        const string key = "NtfsJournalMode";
        if (parameters == null || !parameters.Parameters.ContainsKey(key))
        {
            return new NtfsFileSystem(stream);
        }

        var text = parameters.Parameters[key]?.Trim();
        if (!Enum.TryParse<NtfsJournalMode>(text, ignoreCase: true, out var mode) ||
            !string.Equals(text, Enum.GetName(typeof(NtfsJournalMode), mode), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"{key} must be Replay, Refuse or Ignore", nameof(parameters));
        }

        return new NtfsFileSystem(stream, mode);
    }
}
