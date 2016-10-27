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

namespace DiscUtils.Archives
{
    using System;
    using System.IO;

    internal sealed class UnixBuildFileRecord
    {
        private string _name;
        private UnixFilePermissions _fileMode;
        private int _ownerId;
        private int _groupId;
        private DateTime _modificationTime;
        private BuilderExtentSource _source;

        public UnixBuildFileRecord(string name, byte[] buffer)
            : this(name, new BuilderBufferExtentSource(buffer), 0, 0, 0, Utilities.UnixEpoch)
        {
        }

        public UnixBuildFileRecord(string name, Stream stream)
            : this(name, new BuilderStreamExtentSource(stream), 0, 0, 0, Utilities.UnixEpoch)
        {
        }

        public UnixBuildFileRecord(
            string name, byte[] buffer, UnixFilePermissions fileMode, int ownerId, int groupId, DateTime modificationTime)
            : this(name, new BuilderBufferExtentSource(buffer), fileMode, ownerId, groupId, modificationTime)
        {
        }

        public UnixBuildFileRecord(
            string name, Stream stream, UnixFilePermissions fileMode, int ownerId, int groupId, DateTime modificationTime)
            : this(name, new BuilderStreamExtentSource(stream), fileMode, ownerId, groupId, modificationTime)
        {
        }

        public UnixBuildFileRecord(string name, BuilderExtentSource fileSource, UnixFilePermissions fileMode, int ownerId, int groupId, DateTime modificationTime)
        {
            _name = name;
            _source = fileSource;
            _fileMode = fileMode;
            _ownerId = ownerId;
            _groupId = groupId;
            _modificationTime = modificationTime;
        }

        public string Name
        {
            get { return _name; }
        }

        public UnixFilePermissions FileMode
        {
            get { return this._fileMode; }
        }

        public int OwnerId
        {
            get { return this._ownerId; }
        }

        public int GroupId
        {
            get { return this._groupId; }
        }

        public DateTime ModificationTime
        {
            get { return this._modificationTime; }
        }

        public BuilderExtent Fix(long pos)
        {
            return _source.Fix(pos);
        }
    }
}
