//
// Copyright (c) 2008-2010, Kenneth Bell
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
using System.Globalization;
using System.IO;
using DiscUtils.Vfs;

namespace DiscUtils.Iso9660
{
    internal class ReaderDirectory : File, IVfsDirectory<DirectoryRecord, File>
    {
        private List<DirectoryRecord> _records;

        public ReaderDirectory(IsoContext context, DirectoryRecord dirEntry)
            : base(context, dirEntry)
        {
            byte[] buffer = new byte[IsoUtilities.SectorSize];
            Stream extent = new ExtentStream(_context.DataStream, dirEntry.LocationOfExtent, uint.MaxValue, 0, 0);

            _records = new List<DirectoryRecord>();

            uint totalLength = dirEntry.DataLength;
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

                    if(!IsoUtilities.IsSpecialDirectory(dr))
                    {
                        _records.Add(dr);
                    }

                    pos += length;
                }
            }
        }

        public ICollection<DirectoryRecord> AllEntries
        {
            get { return _records; }
        }

        public DirectoryRecord GetEntryByName(string name)
        {
            bool anyVerMatch = (name.IndexOf(';') < 0);
            string normName = IsoUtilities.NormalizeFileName(name).ToUpper(CultureInfo.InvariantCulture);
            if (anyVerMatch)
            {
                normName = normName.Substring(0, normName.LastIndexOf(';') + 1);
            }

            foreach (DirectoryRecord r in _records)
            {
                string toComp = IsoUtilities.NormalizeFileName(r.FileIdentifier).ToUpper(CultureInfo.InvariantCulture);
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

        public File CreateNewFile(string name)
        {
            throw new NotSupportedException();
        }

    }
}
