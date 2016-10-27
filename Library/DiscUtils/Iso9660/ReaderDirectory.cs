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

namespace DiscUtils.Iso9660
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using DiscUtils.Vfs;

    internal class ReaderDirectory : File, IVfsDirectory<ReaderDirEntry, File>
    {
        private List<ReaderDirEntry> _records;
        private ReaderDirEntry _self;

        public ReaderDirectory(IsoContext context, ReaderDirEntry dirEntry)
            : base(context, dirEntry)
        {
            byte[] buffer = new byte[IsoUtilities.SectorSize];
            Stream extent = new ExtentStream(_context.DataStream, dirEntry.Record.LocationOfExtent, uint.MaxValue, 0, 0);

            _records = new List<ReaderDirEntry>();

            uint totalLength = dirEntry.Record.DataLength;
            uint totalRead = 0;
            while (totalRead < totalLength)
            {
                int toRead = (int)Math.Min(buffer.Length, totalLength - totalRead);
                uint bytesRead = (uint)Utilities.ReadFully(extent, buffer, 0, toRead);
                if (bytesRead != toRead)
                {
                    throw new IOException("Failed to read whole directory");
                }

                totalRead += (uint)bytesRead;

                uint pos = 0;
                while (pos < bytesRead && buffer[pos] != 0)
                {
                    DirectoryRecord dr;
                    uint length = (uint)DirectoryRecord.ReadFrom(buffer, (int)pos, context.VolumeDescriptor.CharacterEncoding, out dr);

                    if (!IsoUtilities.IsSpecialDirectory(dr))
                    {
                        ReaderDirEntry childDirEntry = new ReaderDirEntry(_context, dr);

                        if (context.SuspDetected && !string.IsNullOrEmpty(context.RockRidgeIdentifier))
                        {
                            if (childDirEntry.SuspRecords == null || !childDirEntry.SuspRecords.HasEntry(context.RockRidgeIdentifier, "RE"))
                            {
                                _records.Add(childDirEntry);
                            }
                        }
                        else
                        {
                            _records.Add(childDirEntry);
                        }
                    }
                    else if (dr.FileIdentifier == "\0")
                    {
                        _self = new ReaderDirEntry(_context, dr);
                    }

                    pos += length;
                }
            }
        }

        public ICollection<ReaderDirEntry> AllEntries
        {
            get { return _records; }
        }

        public ReaderDirEntry Self
        {
            get { return _self; }
        }

        public override byte[] SystemUseData
        {
            get { return _self.Record.SystemUseData; }
        }

        public ReaderDirEntry GetEntryByName(string name)
        {
            bool anyVerMatch = name.IndexOf(';') < 0;
            string normName = IsoUtilities.NormalizeFileName(name).ToUpper(CultureInfo.InvariantCulture);
            if (anyVerMatch)
            {
                normName = normName.Substring(0, normName.LastIndexOf(';') + 1);
            }

            foreach (ReaderDirEntry r in _records)
            {
                string toComp = IsoUtilities.NormalizeFileName(r.FileName).ToUpper(CultureInfo.InvariantCulture);
                if (!anyVerMatch && toComp == normName)
                {
                    return r;
                }
                else if (anyVerMatch && toComp.StartsWith(normName, StringComparison.Ordinal))
                {
                    return r;
                }
            }

            return null;
        }

        public ReaderDirEntry CreateNewFile(string name)
        {
            throw new NotSupportedException();
        }
    }
}
