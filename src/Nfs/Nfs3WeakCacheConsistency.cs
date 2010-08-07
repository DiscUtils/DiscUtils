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
    internal sealed class Nfs3WeakCacheConsistencyAttr
    {
        public long Size { get; set; }
        public Nfs3FileTime ModifyTime { get; set; }
        public Nfs3FileTime ChangeTime { get; set; }

        public Nfs3WeakCacheConsistencyAttr(XdrDataReader reader)
        {
            Size = reader.ReadInt64();
            ModifyTime = new Nfs3FileTime(reader);
            ChangeTime = new Nfs3FileTime(reader);
        }
    }

    internal sealed class Nfs3WeakCacheConsistency
    {
        public Nfs3WeakCacheConsistencyAttr Before { get; set; }
        public Nfs3FileAttributes After { get; set; }

        public Nfs3WeakCacheConsistency(XdrDataReader reader)
        {
            if (reader.ReadBool())
            {
                Before = new Nfs3WeakCacheConsistencyAttr(reader);
            }
            if (reader.ReadBool())
            {
                After = new Nfs3FileAttributes(reader);
            }
        }
    }
}
