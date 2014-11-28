using DiscUtils.Vfs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DiscUtils.HfsPlus
{
    internal class Symlink : File, IVfsSymlink<DirEntry, File>
    {
        private string _targetPath;

        public Symlink(Context context, CatalogNodeId nodeId, CommonCatalogFileInfo catalogInfo)
            : base(context, nodeId, catalogInfo)
        {
        }

        public string TargetPath
        {
            get
            {
                if (_targetPath == null)
                {
                    using (BufferStream stream = new BufferStream(this.FileContent, FileAccess.Read))
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        _targetPath = reader.ReadToEnd();
                        _targetPath = _targetPath.Replace('/', '\\');
                    }
                }

                return _targetPath;
            }
        }
    }
}
