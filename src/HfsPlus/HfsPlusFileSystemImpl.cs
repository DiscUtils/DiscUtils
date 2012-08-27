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

    internal sealed class HfsPlusFileSystemImpl : VfsFileSystem<DirEntry, File, Directory, Context>, IUnixFileSystem
    {
        public HfsPlusFileSystemImpl(Stream s)
            : base(new DiscFileSystemOptions())
        {
            s.Position = 1024;

            byte[] headerBuf = Utilities.ReadFully(s, 512);
            VolumeHeader hdr = new VolumeHeader();
            hdr.ReadFrom(headerBuf, 0);

            Context = new HfsPlus.Context();
            Context.VolumeStream = s;
            Context.VolumeHeader = hdr;

            FileBuffer catalogBuffer = new FileBuffer(Context, hdr.CatalogFile, CatalogNodeId.CatalogFileId);
            Context.Catalog = new BTree<CatalogKey>(catalogBuffer);

            FileBuffer extentsBuffer = new FileBuffer(Context, hdr.ExtentsFile, CatalogNodeId.ExtentsFileId);
            Context.ExtentsOverflow = new BTree<ExtentKey>(extentsBuffer);

            // Establish Root directory
            byte[] rootThreadData = Context.Catalog.Find(new CatalogKey(CatalogNodeId.RootFolderId, string.Empty));
            CatalogThread rootThread = new CatalogThread();
            rootThread.ReadFrom(rootThreadData, 0);
            byte[] rootDirEntryData = Context.Catalog.Find(new CatalogKey(rootThread.ParentId, rootThread.Name));
            DirEntry rootDirEntry = new DirEntry(rootThread.Name, rootDirEntryData);
            RootDirectory = (Directory)GetFile(rootDirEntry);
        }

        public override string VolumeLabel
        {
            get 
            {
                byte[] rootThreadData = Context.Catalog.Find(new CatalogKey(CatalogNodeId.RootFolderId, string.Empty));
                CatalogThread rootThread = new CatalogThread();
                rootThread.ReadFrom(rootThreadData, 0);

                return rootThread.Name;
            }
        }

        public override string FriendlyName
        {
            get { return "Apple HFS+"; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public UnixFileSystemInfo GetUnixFileInfo(string path)
        {
            DirEntry dirEntry = GetDirectoryEntry(path);
            if (dirEntry == null)
            {
                throw new FileNotFoundException("No such file or directory", path);
            }

            return dirEntry.CatalogFileInfo.FileSystemInfo;
        }

        protected override File ConvertDirEntryToFile(DirEntry dirEntry)
        {
            if (dirEntry.IsDirectory)
            {
                return new Directory(Context, dirEntry.NodeId, dirEntry.CatalogFileInfo);
            }
            else
            {
                return new File(Context, dirEntry.NodeId, dirEntry.CatalogFileInfo);
            }
        }
    }
}
