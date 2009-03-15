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
    internal class StandardInformationAttribute : BaseAttribute
    {
        private DateTime _creationTime;
        private DateTime _modificationTime;
        private DateTime _mftChangedTime;
        private DateTime _lastAccessTime;
        private uint _filePermissions;
        private uint _maxVersions;
        private uint _version;
        private uint _classId;
        private uint _ownerId;
        private uint _securityId;
        private ulong _quotaCharged;
        private ulong _updateSequenceNumber;

        public StandardInformationAttribute(ResidentFileAttributeRecord record)
            : base(null, record)
        {
            byte[] data = record.GetData();

            _creationTime = DateTime.FromFileTimeUtc(Utilities.ToInt64LittleEndian(data, 0x00));
            _modificationTime = DateTime.FromFileTimeUtc(Utilities.ToInt64LittleEndian(data, 0x08));
            _mftChangedTime = DateTime.FromFileTimeUtc(Utilities.ToInt64LittleEndian(data, 0x10));
            _lastAccessTime = DateTime.FromFileTimeUtc(Utilities.ToInt64LittleEndian(data, 0x18));
            _filePermissions = Utilities.ToUInt32LittleEndian(data, 0x20);
            _maxVersions = Utilities.ToUInt32LittleEndian(data, 0x24);
            _version = Utilities.ToUInt32LittleEndian(data, 0x28);
            _classId = Utilities.ToUInt32LittleEndian(data, 0x2C);

            if (data.Length > 0x30)
            {
                _ownerId = Utilities.ToUInt32LittleEndian(data, 0x30);
                _securityId = Utilities.ToUInt32LittleEndian(data, 0x34);
                _quotaCharged = Utilities.ToUInt64LittleEndian(data, 0x38);
                _updateSequenceNumber = Utilities.ToUInt64LittleEndian(data, 0x40);
            }
        }

        public override void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "STANDARD INFORMATION ATTRIBUTE (" + (Name == null ? "No Name" : Name) + ")");
            writer.WriteLine(indent + "      Creation Time: " + _creationTime);
            writer.WriteLine(indent + "  Modification Time: " + _modificationTime);
            writer.WriteLine(indent + "   MFT Changed Time: " + _mftChangedTime);
            writer.WriteLine(indent + "   Last Access Time: " + _lastAccessTime);
            writer.WriteLine(indent + "   File Permissions: " + (FileAttributes)_filePermissions);
            writer.WriteLine(indent + "       Max Versions: " + _maxVersions);
            writer.WriteLine(indent + "            Version: " + _version);
            writer.WriteLine(indent + "           Class Id: " + _classId);
            writer.WriteLine(indent + "        Security Id: " + _securityId);
            writer.WriteLine(indent + "      Quota Charged: " + _quotaCharged);
            writer.WriteLine(indent + "     Update Seq Num: " + _updateSequenceNumber);
        }
    }

}
