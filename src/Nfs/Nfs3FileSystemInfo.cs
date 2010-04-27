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


namespace DiscUtils.Nfs
{
    internal sealed class Nfs3FileSystemInfo
    {
        public uint ReadMaxBytes { get; set; }
        public uint ReadPreferredBytes { get; set; }
        public uint ReadMultipleSize { get; set; }
        public uint WriteMaxBytes { get; set; }
        public uint WritePreferredBytes { get; set; }
        public uint WriteMultipleSize { get; set; }
        public uint DirectoryPreferredBytes { get; set; }
        public long MaxFileSize { get; set; }
        public Nfs3FileTime TimePrecision { get; set; }
        public Nfs3FileSystemProperties FileSystemProperties { get; set; }

        public Nfs3FileSystemInfo(XdrDataReader reader)
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
    }
}
