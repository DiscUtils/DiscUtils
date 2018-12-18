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

using System;

namespace DiscUtils.Nfs
{
    public sealed class Nfs3FileSystemInfo
    {
        internal Nfs3FileSystemInfo(XdrDataReader reader)
        {
            ReadMaxBytes = reader.ReadUInt32();
            ReadPreferredBytes = reader.ReadUInt32();
            ReadMultipleSize = reader.ReadUInt32();
            WriteMaxBytes = reader.ReadUInt32();
            WritePreferredBytes = reader.ReadUInt32();
            WriteMultipleSize = reader.ReadUInt32();
            DirectoryPreferredBytes = reader.ReadUInt32();
            MaxFileSize = reader.ReadInt64();
            TimePrecision = new Nfs3FileTime(reader);
            FileSystemProperties = (Nfs3FileSystemProperties)reader.ReadInt32();
        }

        public Nfs3FileSystemInfo()
        {
        }

        /// <summary>
        /// The preferred size of a READDIR request.
        /// </summary>
        public uint DirectoryPreferredBytes { get; set; }

        /// <summary>
        /// A bit mask of file system properties.
        /// </summary>
        public Nfs3FileSystemProperties FileSystemProperties { get; set; }

        /// <summary>
        /// The maximum size of a file on the file system.
        /// </summary>
        public long MaxFileSize { get; set; }

        /// <summary>
        /// The maximum size in bytes of a READ request supported
        /// by the server. Any READ with a number greater than
        /// rtmax will result in a short read of rtmax bytes or
        /// less.
        /// </summary>
        public uint ReadMaxBytes { get; set; }

        /// <summary>
        /// The suggested multiple for the size of a READ request.
        /// </summary>
        public uint ReadMultipleSize { get; set; }

        /// <summary>
        /// The preferred size of a READ request. This should be
        /// the same as rtmax unless there is a clear benefit in
        /// performance or efficiency.
        /// </summary>
        public uint ReadPreferredBytes { get; set; }

        /// <summary>
        /// The server time granularity. When setting a file time
        /// using SETATTR, the server guarantees only to preserve
        /// times to this accuracy. If this is {0, 1}, the server
        /// can support nanosecond times, {0, 1000000}
        /// denotes millisecond precision, and {1, 0} indicates that times
        /// are accurate only to the nearest second.
        /// </summary>
        public Nfs3FileTime TimePrecision { get; set; }

        /// <summary>
        /// The maximum size of a WRITE request supported by the
        /// server. In general, the client is limited by wtmax
        /// since there is no guarantee that a server can handle a
        /// larger write. Any WRITE with a count greater than wtmax
        /// will result in a short write of at most wtmax bytes.
        /// </summary>
        public uint WriteMaxBytes { get; set; }

        /// <summary>
        /// The suggested multiple for the size of a WRITE
        /// request.
        /// </summary>
        public uint WriteMultipleSize { get; set; }

        /// <summary>
        /// The preferred size of a WRITE request. This should be
        /// the same as wtmax unless there is a clear benefit in
        /// performance or efficiency.
        /// </summary>
        public uint WritePreferredBytes { get; set; }

        internal void Write(XdrDataWriter writer)
        {
            writer.Write(ReadMaxBytes);
            writer.Write(ReadPreferredBytes);
            writer.Write(ReadMultipleSize);
            writer.Write(WriteMaxBytes);
            writer.Write(WritePreferredBytes);
            writer.Write(WriteMultipleSize);
            writer.Write(DirectoryPreferredBytes);
            writer.Write(MaxFileSize);
            TimePrecision.Write(writer);
            writer.Write((int)FileSystemProperties);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Nfs3FileSystemInfo);
        }

        public bool Equals(Nfs3FileSystemInfo other)
        {
            if (other == null)
            {
                return false;
            }

            return other.ReadMaxBytes == ReadMaxBytes
                && other.ReadPreferredBytes == ReadPreferredBytes
                && other.ReadMultipleSize == ReadMultipleSize
                && other.WriteMaxBytes == WriteMaxBytes
                && other.WritePreferredBytes == WritePreferredBytes
                && other.WriteMultipleSize == WriteMultipleSize
                && other.DirectoryPreferredBytes == DirectoryPreferredBytes
                && other.MaxFileSize == MaxFileSize
                && object.Equals(other.TimePrecision, TimePrecision)
                && other.FileSystemProperties == FileSystemProperties;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                HashCode.Combine(ReadMaxBytes, ReadPreferredBytes, ReadMultipleSize, WriteMaxBytes, WritePreferredBytes, WriteMultipleSize, DirectoryPreferredBytes, MaxFileSize),
                TimePrecision, FileSystemProperties);
        }
    }
}