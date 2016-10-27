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

namespace DiscUtils.HfsPlus
{
    using System;
    using System.IO;
    using DiscUtils.Vfs;

    internal sealed class DirEntry : VfsDirEntry
    {
        private string _name;
        private CommonCatalogFileInfo _info;

        public DirEntry(string name, byte[] dirEntryData)
        {
            _name = name;
            _info = ParseDirEntryData(dirEntryData);
        }

        public override bool IsDirectory
        {
            get { return _info.RecordType == CatalogRecordType.FolderRecord; }
        }

        public override bool IsSymlink
        {
            get
            {
                return
                    !IsDirectory
                    && ((FileTypeFlags)((CatalogFileInfo)_info).FileInfo.FileType) == FileTypeFlags.SymLinkFileType;
            }
        }

        public override string FileName
        {
            get { return _name; }
        }

        public override bool HasVfsTimeInfo
        {
            get { return true; }
        }

        public override DateTime LastAccessTimeUtc
        {
            get { return _info.AccessTime; }
        }

        public override DateTime LastWriteTimeUtc
        {
            get { return _info.ContentModifyTime; }
        }

        public override DateTime CreationTimeUtc
        {
            get { return _info.CreateTime; }
        }

        public override bool HasVfsFileAttributes
        {
            get { return true; }
        }

        public override FileAttributes FileAttributes
        {
            get { return Utilities.FileAttributesFromUnixFileType(_info.FileSystemInfo.FileType); }
        }

        public override long UniqueCacheId
        {
            get { return _info.FileId; }
        }

        public CatalogNodeId NodeId
        {
            get { return _info.FileId; }
        }

        public CommonCatalogFileInfo CatalogFileInfo
        {
            get { return _info; }
        }

        internal static bool IsFileOrDirectory(byte[] dirEntryData)
        {
            CatalogRecordType type = (CatalogRecordType)Utilities.ToInt16BigEndian(dirEntryData, 0);
            return type == CatalogRecordType.FolderRecord || type == CatalogRecordType.FileRecord;
        }

        private static CommonCatalogFileInfo ParseDirEntryData(byte[] dirEntryData)
        {
            CatalogRecordType type = (CatalogRecordType)Utilities.ToInt16BigEndian(dirEntryData, 0);

            CommonCatalogFileInfo result = null;
            switch (type)
            {
                case CatalogRecordType.FolderRecord:
                    result = new CatalogDirInfo();
                    break;
                case CatalogRecordType.FileRecord:
                    result = new CatalogFileInfo();
                    break;
                default:
                    throw new NotImplementedException("Unknown catalog record type: " + type);
            }

            result.ReadFrom(dirEntryData, 0);
            return result;
        }
    }
}
