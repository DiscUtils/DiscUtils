//
// Copyright (c) 2008-2009, Kenneth Bell
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
using System.IO;

namespace DiscUtils.Ntfs.Attributes
{
    internal class ObjectIdAttribute : BaseAttribute
    {
        private Guid _objectId;
        private Guid _birthVolumeId;
        private Guid _birthObjectId;
        private Guid _domainId;

        public ObjectIdAttribute(NtfsFileSystem fileSystem, FileAttributeRecord record)
            : base(fileSystem, record)
        {
            if (record.DataLength > 0)
            {
                using (Stream s = Open(FileAccess.Read))
                {
                    _objectId = new Guid(Utilities.ReadFully(s, 16));

                    if (record.DataLength > 16)
                    {
                        _birthVolumeId = new Guid(Utilities.ReadFully(s, 16));
                    }
                    if (record.DataLength > 32)
                    {
                        _birthObjectId = new Guid(Utilities.ReadFully(s, 16));
                    }
                    if (record.DataLength > 48)
                    {
                        _domainId = new Guid(Utilities.ReadFully(s, 16));
                    }
                }
            }
        }

        public override void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "OBJECT ID ATTRIBUTE (" + (Name == null ? "No Name" : Name) + ")");
            writer.WriteLine(indent + "        Object ID: " + _objectId);
            writer.WriteLine(indent + "  Birth Volume ID: " + _birthVolumeId);
            writer.WriteLine(indent + "  Birth Object ID: " + _birthObjectId);
            writer.WriteLine(indent + "        Domain ID: " + _domainId);
        }
    }
}
