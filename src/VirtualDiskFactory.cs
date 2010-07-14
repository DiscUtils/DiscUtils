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
using System.IO;

namespace DiscUtils
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    internal sealed class VirtualDiskFactoryAttribute : Attribute
    {
        private string _type;
        private string[] _fileExtensions;

        public VirtualDiskFactoryAttribute(string type, string fileExtensions)
        {
            _type = type;
            _fileExtensions = fileExtensions.Replace(".","").Split(',');
        }

        public string Type
        {
            get { return _type; }
        }

        public string[] FileExtensions
        {
            get { return _fileExtensions; }
        }
    }

    internal abstract class VirtualDiskFactory
    {
        public abstract string[] Variants { get; }

        public abstract DiskImageBuilder GetImageBuilder(string variant);

        public abstract VirtualDisk CreateDisk(FileLocator locator, string variant, string path, long capacity, Geometry geometry, Dictionary<string, string> parameters);

        public abstract VirtualDisk OpenDisk(string path, FileAccess access);
        public abstract VirtualDisk OpenDisk(FileLocator locator, string path, FileAccess access);

        public VirtualDisk OpenDisk(DiscFileSystem fileSystem, string path, FileAccess access)
        {
            return OpenDisk(new DiscFileLocator(fileSystem, @"\"), path, access);
        }
    }
}
