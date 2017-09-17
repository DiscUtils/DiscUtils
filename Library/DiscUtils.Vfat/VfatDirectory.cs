using System;
using System.IO;
using DiscUtils.Fat;
using DiscUtils.Streams;
using Directory = DiscUtils.Fat.Directory;

namespace DiscUtils.Vfat
{
    internal class VfatDirectory : Directory
    {
        public VfatDirectory(Directory parent, long parentId)
            : base(parent, parentId)
        {
        }

        public VfatDirectory(FatFileSystem fileSystem, Stream fatStream)
            : base(fileSystem, fatStream)
        {
        }

        internal override SparseStream OpenFile(FileName name, FileMode mode, FileAccess fileAccess)
        {
            long fileId = FindEntry(name);
            bool exists = fileId != -1;

            if ((mode == FileMode.OpenOrCreate || mode == FileMode.CreateNew || mode == FileMode.Create) && !exists)
            {
                var vname = (VfatFileName)name;
                for (int part = vname.LastPart; part>=0; --part)
                {
                    var phony = new VfatDirectoryEntry(part, (VfatFileSystemOptions)FileSystem.FatOptions, vname, FileSystem.FatVariant);
                    AddEntry(phony);
                }

                // Create new file
                var newEntry = new DirectoryEntry(FileSystem.FatOptions, name, FatAttributes.Archive, FileSystem.FatVariant);
                newEntry.FirstCluster = 0; // i.e. Zero-length
                newEntry.CreationTime = FileSystem.ConvertFromUtc(DateTime.UtcNow);
                newEntry.LastWriteTime = newEntry.CreationTime;

                fileId = AddEntry(newEntry);

                return new FatFileStream(FileSystem, this, fileId, fileAccess);
            }

            // Should never get here...
            throw new NotImplementedException();
        }
    }
}
