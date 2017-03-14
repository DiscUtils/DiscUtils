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
using System.IO;
using DiscUtils;
using DiscUtils.Iso9660;
using Xunit;

namespace LibraryTests.Iso9660
{
    public class IsoDirectoryInfoTest
    {
        [Fact]
        public void Exists()
        {
            CDBuilder builder = new CDBuilder();
            builder.AddFile(@"SOMEDIR\CHILDDIR\FILE.TXT", new byte[0]);
            CDReader fs = new CDReader(builder.Build(), false);

            Assert.True(fs.GetDirectoryInfo(@"\").Exists);
            Assert.True(fs.GetDirectoryInfo(@"SOMEDIR").Exists);
            Assert.True(fs.GetDirectoryInfo(@"SOMEDIR\CHILDDIR").Exists);
            Assert.True(fs.GetDirectoryInfo(@"SOMEDIR\CHILDDIR\").Exists);
            Assert.False(fs.GetDirectoryInfo(@"NONDIR").Exists);
            Assert.False(fs.GetDirectoryInfo(@"SOMEDIR\NONDIR").Exists);
        }

        [Fact]
        public void FullName()
        {
            CDBuilder builder = new CDBuilder();
            CDReader fs = new CDReader(builder.Build(), false);

            Assert.Equal(@"\", fs.Root.FullName);
            Assert.Equal(@"SOMEDIR\", fs.GetDirectoryInfo(@"SOMEDIR").FullName);
            Assert.Equal(@"SOMEDIR\CHILDDIR\", fs.GetDirectoryInfo(@"SOMEDIR\CHILDDIR").FullName);
        }

        [Fact]
        public void SimpleSearch()
        {
            CDBuilder builder = new CDBuilder();
            builder.AddFile(@"SOMEDIR\CHILDDIR\GCHILDIR\FILE.TXT", new byte[0]);
            CDReader fs = new CDReader(builder.Build(), false);

            DiscDirectoryInfo di = fs.GetDirectoryInfo(@"SOMEDIR\CHILDDIR");
            DiscFileInfo[] fis = di.GetFiles("*.*", SearchOption.AllDirectories);
        }

        [Fact]
        public void Extension()
        {
            CDBuilder builder = new CDBuilder();
            CDReader fs = new CDReader(builder.Build(), false);

            Assert.Equal("dir", fs.GetDirectoryInfo("fred.dir").Extension);
            Assert.Equal("", fs.GetDirectoryInfo("fred").Extension);
        }

        [Fact]
        public void GetDirectories()
        {
            CDBuilder builder = new CDBuilder();
            builder.AddDirectory(@"SOMEDIR\CHILD\GCHILD");
            builder.AddDirectory(@"A.DIR");
            CDReader fs = new CDReader(builder.Build(), false);


            Assert.Equal(2, fs.Root.GetDirectories().Length);

            DiscDirectoryInfo someDir = fs.Root.GetDirectories(@"SoMeDir")[0];
            Assert.Equal(1, fs.Root.GetDirectories("SOMEDIR").Length);
            Assert.Equal("SOMEDIR", someDir.Name);

            Assert.Equal(1, someDir.GetDirectories("*.*").Length);
            Assert.Equal("CHILD", someDir.GetDirectories("*.*")[0].Name);
            Assert.Equal(2, someDir.GetDirectories("*.*", SearchOption.AllDirectories).Length);

            Assert.Equal(4, fs.Root.GetDirectories("*.*", SearchOption.AllDirectories).Length);
            Assert.Equal(2, fs.Root.GetDirectories("*.*", SearchOption.TopDirectoryOnly).Length);

            Assert.Equal(1, fs.Root.GetDirectories("*.DIR", SearchOption.AllDirectories).Length);
            Assert.Equal(@"A.DIR\", fs.Root.GetDirectories("*.DIR", SearchOption.AllDirectories)[0].FullName);

            Assert.Equal(1, fs.Root.GetDirectories("GCHILD", SearchOption.AllDirectories).Length);
            Assert.Equal(@"SOMEDIR\CHILD\GCHILD\", fs.Root.GetDirectories("GCHILD", SearchOption.AllDirectories)[0].FullName);
        }

        [Fact]
        public void GetFiles()
        {
            CDBuilder builder = new CDBuilder();
            builder.AddDirectory(@"SOMEDIR\CHILD\GCHILD");
            builder.AddDirectory(@"AAA.DIR");
            builder.AddFile(@"FOO.TXT", new byte[10]);
            builder.AddFile(@"SOMEDIR\CHILD.TXT", new byte[10]);
            builder.AddFile(@"SOMEDIR\FOO.TXT", new byte[10]);
            builder.AddFile(@"SOMEDIR\CHILD\GCHILD\BAR.TXT", new byte[10]);
            CDReader fs = new CDReader(builder.Build(), false);

            Assert.Equal(1, fs.Root.GetFiles().Length);
            Assert.Equal("FOO.TXT", fs.Root.GetFiles()[0].FullName);

            Assert.Equal(2, fs.Root.GetDirectories("SOMEDIR")[0].GetFiles("*.TXT").Length);
            Assert.Equal(4, fs.Root.GetFiles("*.TXT", SearchOption.AllDirectories).Length);

            Assert.Equal(0, fs.Root.GetFiles("*.DIR", SearchOption.AllDirectories).Length);
        }

        [Fact]
        public void GetFileSystemInfos()
        {
            CDBuilder builder = new CDBuilder();
            builder.AddDirectory(@"SOMEDIR\CHILD\GCHILD");
            builder.AddDirectory(@"AAA.EXT");
            builder.AddFile(@"FOO.TXT", new byte[10]);
            builder.AddFile(@"SOMEDIR\CHILD.TXT", new byte[10]);
            builder.AddFile(@"SOMEDIR\FOO.TXT", new byte[10]);
            builder.AddFile(@"SOMEDIR\CHILD\GCHILD\BAR.TXT", new byte[10]);
            CDReader fs = new CDReader(builder.Build(), false);

            Assert.Equal(3, fs.Root.GetFileSystemInfos().Length);

            Assert.Equal(1, fs.Root.GetFileSystemInfos("*.EXT").Length);
            Assert.Equal(2, fs.Root.GetFileSystemInfos("*.?XT").Length);
        }

        [Fact]
        public void Parent()
        {
            CDBuilder builder = new CDBuilder();
            builder.AddDirectory(@"SOMEDIR");
            CDReader fs = new CDReader(builder.Build(), false);

            Assert.Equal(fs.Root, fs.Root.GetDirectories("SOMEDIR")[0].Parent);
        }

        [Fact]
        public void Parent_Root()
        {
            CDBuilder builder = new CDBuilder();
            CDReader fs = new CDReader(builder.Build(), false);

            Assert.Null(fs.Root.Parent);
        }

        [Fact]
        public void RootBehaviour()
        {
            // Start time rounded down to whole seconds
            DateTime start = DateTime.UtcNow;
            start = new DateTime(start.Year, start.Month, start.Day, start.Hour, start.Minute, start.Second);

            CDBuilder builder = new CDBuilder();
            CDReader fs = new CDReader(builder.Build(), false);
            DateTime end = DateTime.UtcNow;

            Assert.Equal(FileAttributes.Directory | FileAttributes.ReadOnly, fs.Root.Attributes);
            Assert.True(fs.Root.CreationTimeUtc >= start);
            Assert.True(fs.Root.CreationTimeUtc <= end);
            Assert.True(fs.Root.LastAccessTimeUtc >= start);
            Assert.True(fs.Root.LastAccessTimeUtc <= end);
            Assert.True(fs.Root.LastWriteTimeUtc >= start);
            Assert.True(fs.Root.LastWriteTimeUtc <= end);
        }

        [Fact]
        public void Attributes()
        {
            // Start time rounded down to whole seconds
            DateTime start = DateTime.UtcNow;
            start = new DateTime(start.Year, start.Month, start.Day, start.Hour, start.Minute, start.Second);

            CDBuilder builder = new CDBuilder();
            builder.AddDirectory("Foo");
            CDReader fs = new CDReader(builder.Build(), false);
            DateTime end = DateTime.UtcNow;

            DiscDirectoryInfo di = fs.GetDirectoryInfo("Foo");

            Assert.Equal(FileAttributes.Directory | FileAttributes.ReadOnly, di.Attributes);
            Assert.True(di.CreationTimeUtc >= start);
            Assert.True(di.CreationTimeUtc <= end);
            Assert.True(di.LastAccessTimeUtc >= start);
            Assert.True(di.LastAccessTimeUtc <= end);
            Assert.True(di.LastWriteTimeUtc >= start);
            Assert.True(di.LastWriteTimeUtc <= end);
        }
    }
}
