//
// Copyright (c) 2017, Bianco Veigel
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
    internal enum NfsProc3 : uint
    {
        Null = 0,
        GetAttr = 1,
        SetAttr = 2,
        Lookup = 3,
        Access = 4,
        Readlink = 5,
        Read = 6,
        Write = 7,
        Create = 8,
        Mkdir = 9,
        Symlink = 10,
        Mknod = 11,
        Remove = 12,
        Rmdir = 13,
        Rename = 14,
        Link = 15,
        Readdir = 16,
        Readdirplus = 17,
        Fsstat = 18,
        Fsinfo = 19,
        Pathconf = 20,
        Commit = 21,
    }
}