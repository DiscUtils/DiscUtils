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
using System.Globalization;
using System.IO;

namespace DiscUtils
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    internal sealed class VirtualDiskTransportAttribute : Attribute
    {
        private string _scheme;

        public VirtualDiskTransportAttribute(string scheme)
        {
            _scheme = scheme;
        }

        public string Scheme
        {
            get { return _scheme; }
        }
    }

    internal abstract class VirtualDiskTransport : IDisposable
    {
        public abstract void Connect(Uri uri, string username, string password);

        public virtual void Dispose(bool disposing)
        {
        }

        public abstract bool IsRawDisk { get; }

        public abstract VirtualDisk OpenDisk(FileAccess access);

        public abstract FileLocator GetFileLocator();

        public abstract string GetFileName();

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }

    [VirtualDiskTransport("file")]
    internal sealed class FileTransport : VirtualDiskTransport
    {
        private string _path;

        public override void Connect(Uri uri, string username, string password)
        {
            _path = uri.LocalPath;
            if (!Directory.Exists(Utilities.GetDirectoryFromPath(_path)))
            {
                throw new FileNotFoundException(string.Format(CultureInfo.InvariantCulture, "No such file '{0}'", uri.OriginalString), _path);
            }
        }

        public override bool IsRawDisk
        {
            get { return false; }
        }

        public override VirtualDisk OpenDisk(FileAccess access)
        {
            throw new NotSupportedException();
        }

        public override FileLocator GetFileLocator()
        {
            return new LocalFileLocator(Path.GetDirectoryName(_path));
        }

        public override string GetFileName()
        {
            return Path.GetFileName(_path);
        }
    }
}
