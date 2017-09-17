using System;
using System.IO;
using DiscUtils.Fat;
using DiscUtils.Streams;
using System.Collections.Generic;
using Directory = DiscUtils.Fat.Directory;

namespace DiscUtils.Vfat
{
    public class VfatFileSystem : FatFileSystem
    {
        private IDictionary<string, int> _filenameIndexer;

        public VfatFileSystem(Stream data)
            : base(data, new VfatFileSystemOptions())
        {
            _filenameIndexer = new Dictionary<string, int>();
        }

        public override SparseStream OpenFile(string path, FileMode mode, FileAccess access)
        {
            VfatDirectory parent;
            long entryId;
            try
            {
                entryId = GetDirectoryEntry(RootDir, path, out Directory p);
                parent = (VfatDirectory)p;
            }
            catch (ArgumentException)
            {
                throw new IOException("Invalid path: " + path);
            }

            if (parent == null)
            {
                throw new FileNotFoundException("Could not locate file", path);
            }

            if (entryId < 0)
            {
                return parent.OpenFile(VfatFileName.FromPath(path, this), mode, access);
            }

            DirectoryEntry dirEntry = parent.GetEntry(entryId);

            if ((dirEntry.Attributes & FatAttributes.Directory) != 0)
            {
                throw new IOException("Attempt to open directory as a file");
            }
            return parent.OpenFile(dirEntry.Name, mode, access);
        }

        internal override Directory DirectoryFactory(Directory parent, long parentId)
        {
            return new VfatDirectory(parent, parentId);
        }

        internal override Directory DirectoryFactory(Stream fatStream)
        {
            return new VfatDirectory(this, fatStream);
        }

        internal override FileName FileNameFactory(string name)
        {
            return VfatFileName.FromName(name, this, GetUniqueIndex(name));
        }

        public static new FatFileSystem FormatFloppy(Stream stream, FloppyDiskType type, string label)
        {
            FormatStream(stream, type, label);
            return new VfatFileSystem(stream);
        }

        public int GetUniqueIndex(string name)
        {
            if(!_filenameIndexer.ContainsKey(name))
                _filenameIndexer.Add(name, 0);  

            return ++_filenameIndexer[name];
        }
    }
}
