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

using System.Globalization;
using System.IO;

namespace DiscUtils.Ntfs
{
    internal class ReparsePoints
    {
        private IndexView<Key, Data> _index;
        private File _file;

        public ReparsePoints(File file)
        {
            _file = file;
            _index = new IndexView<Key, Data>(file.GetIndex("$R"));
        }

        internal void Add(uint tag, FileRecordReference file)
        {
            Key newKey = new Key();
            newKey.Tag = tag;
            newKey.File = file;

            Data data = new Data();

            _index[newKey] = data;
            _file.UpdateRecordInMft();
        }

        internal void Remove(uint tag, FileRecordReference file)
        {
            Key key = new Key();
            key.Tag = tag;
            key.File = file;

            _index.Remove(key);
            _file.UpdateRecordInMft();
        }

        public void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "REPARSE POINT INDEX");

            foreach (var entry in _index.Entries)
            {
                writer.WriteLine(indent + "  REPARSE POINT INDEX ENTRY");
                writer.WriteLine(indent + "            Tag: " + entry.Key.Tag.ToString("x", CultureInfo.InvariantCulture));
                writer.WriteLine(indent + "  MFT Reference: " + entry.Key.File);
            }
        }

        internal sealed class Key : IByteArraySerializable
        {
            public uint Tag;
            public FileRecordReference File;

            #region IByteArraySerializable Members

            public int ReadFrom(byte[] buffer, int offset)
            {
                Tag = Utilities.ToUInt32LittleEndian(buffer, offset);
                File = new FileRecordReference(Utilities.ToUInt64LittleEndian(buffer, offset + 4));
                return 12;
            }

            public void WriteTo(byte[] buffer, int offset)
            {
                Utilities.WriteBytesLittleEndian(Tag, buffer, offset);
                Utilities.WriteBytesLittleEndian(File.Value, buffer, offset + 4);
                //Utilities.WriteBytesLittleEndian((uint)0, buffer, offset + 12);
            }

            public int Size
            {
                get { return 12; }
            }

            #endregion

            public override string ToString()
            {
                return string.Format(CultureInfo.InvariantCulture, "{0:x}:", Tag) + File;
            }
        }

        internal sealed class Data : IByteArraySerializable
        {
            #region IByteArraySerializable Members

            public int ReadFrom(byte[] buffer, int offset)
            {
                return 0;
            }

            public void WriteTo(byte[] buffer, int offset)
            {
            }

            public int Size
            {
                get { return 0; }
            }

            #endregion

            public override string ToString()
            {
                return "<no data>";
            }
        }
    }
}
