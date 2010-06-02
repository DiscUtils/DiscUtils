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
using System.Text;

namespace DiscUtils.Udf
{
    internal class Directory : File
    {
        private List<FileIdentifier> _entries;

        public Directory(UdfContext context, LogicalPartition partition, ExtendedFileEntry fileEntry)
            : base(context, partition, fileEntry, (uint)partition.LogicalBlockSize)
        {
            if (Content.Capacity > int.MaxValue)
            {
                throw new NotImplementedException("Very large directory");
            }

            _entries = new List<FileIdentifier>();

            byte[] contentBytes = Utilities.ReadFully(Content, 0, (int)Content.Capacity);

            int pos = 0;
            while (pos < contentBytes.Length)
            {
                FileIdentifier id = new FileIdentifier();
                int size = id.ReadFrom(contentBytes, pos);

                if ((id.FileCharacteristics & (FileCharacteristic.Deleted | FileCharacteristic.Parent)) == 0)
                {
                    _entries.Add(id);
                }

                pos += size;
            }
        }

        public List<FileIdentifier> Entries
        {
            get
            {
                return _entries;
            }
        }

        internal FileIdentifier GetEntryByName(string name)
        {
            foreach (var entry in _entries)
            {
                if (string.Compare(entry.Name, name, true) == 0)
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
