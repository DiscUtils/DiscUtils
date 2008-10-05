//
// Copyright (c) 2008, Kenneth Bell
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
using System.Linq;
using System.Text;
using System.IO;

namespace DiscUtils
{
    public abstract class DiscDirectoryInfo : DiscFileSystemInfo
    {
        public abstract DiscDirectoryInfo[] GetDirectories();

        public abstract DiscDirectoryInfo[] GetDirectories(string pattern);

        public abstract DiscDirectoryInfo[] GetDirectories(string pattern, SearchOption option);

        public abstract DiscFileInfo[] GetFiles();

        public abstract DiscFileInfo[] GetFiles(string pattern);

        public abstract DiscFileInfo[] GetFiles(string pattern, SearchOption option);

        public abstract DiscFileSystemInfo[] GetFileSystemInfos();

        public abstract DiscFileSystemInfo[] GetFileSystemInfos(string pattern);

    }
}
