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
using Xunit;

namespace LibraryTests
{
    public class DiscFileSystemDirectoryTest
    {
        [Theory]
        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void Create(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo("SOMEDIR");
            dirInfo.Create();

            Assert.Equal(1, fs.Root.GetDirectories().Length);
        }

        [Theory]
        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void CreateRecursive(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo(@"SOMEDIR\CHILDDIR");
            dirInfo.Create();

            Assert.Equal(1, fs.Root.GetDirectories().Length);
            Assert.Equal(1, fs.GetDirectoryInfo(@"SOMEDIR").GetDirectories().Length);
            Assert.Equal("CHILDDIR", fs.GetDirectoryInfo(@"SOMEDIR").GetDirectories()[0].Name);
        }

        [Theory]
        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void CreateExisting(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo("SOMEDIR");
            dirInfo.Create();
            dirInfo.Create();

            Assert.Equal(1, fs.Root.GetDirectories().Length);
        }

        [Trait("Category", "ThrowsException")]
        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void CreateInvalid_Long(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo(new String('X', 256));
            Assert.Throws<IOException>(() => dirInfo.Create());
        }

        [Trait("Category", "ThrowsException")]
        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void CreateInvalid_Characters(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo("SOME\0DIR");
            Assert.Throws<IOException>(() => dirInfo.Create());
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void Exists(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo(@"SOMEDIR\CHILDDIR");
            dirInfo.Create();

            Assert.True(fs.GetDirectoryInfo(@"\").Exists);
            Assert.True(fs.GetDirectoryInfo(@"SOMEDIR").Exists);
            Assert.True(fs.GetDirectoryInfo(@"SOMEDIR\CHILDDIR").Exists);
            Assert.True(fs.GetDirectoryInfo(@"SOMEDIR\CHILDDIR\").Exists);
            Assert.False(fs.GetDirectoryInfo(@"NONDIR").Exists);
            Assert.False(fs.GetDirectoryInfo(@"SOMEDIR\NONDIR").Exists);
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void FullName(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            Assert.Equal(@"\", fs.Root.FullName);
            Assert.Equal(@"SOMEDIR\", fs.GetDirectoryInfo(@"SOMEDIR").FullName);
            Assert.Equal(@"SOMEDIR\CHILDDIR\", fs.GetDirectoryInfo(@"SOMEDIR\CHILDDIR").FullName);
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void Delete(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.CreateDirectory(@"Fred");
            Assert.Equal(1, fs.Root.GetDirectories().Length);

            fs.Root.GetDirectories(@"Fred")[0].Delete();
            Assert.Equal(0, fs.Root.GetDirectories().Length);
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void DeleteRecursive(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.CreateDirectory(@"Fred\child");
            Assert.Equal(1, fs.Root.GetDirectories().Length);

            fs.Root.GetDirectories(@"Fred")[0].Delete(true);
            Assert.Equal(0, fs.Root.GetDirectories().Length);
        }

        [Trait("Category", "ThrowsException")]
        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void DeleteRoot(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            Assert.Throws<IOException>(() => fs.Root.Delete());
        }

        [Trait("Category", "ThrowsException")]
        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void DeleteNonEmpty_NonRecursive(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.CreateDirectory(@"Fred\child");
            Assert.Throws<IOException>(() => fs.Root.GetDirectories(@"Fred")[0].Delete());
        }

        [Trait("Category", "SlowTest")]
        [MemberData(nameof(FileSystemSource.QuickReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void CreateDeleteLeakTest(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            for (int i = 0; i < 2000; ++i)
            {
                fs.CreateDirectory(@"Fred");
                fs.Root.GetDirectories(@"Fred")[0].Delete();
            }

            fs.CreateDirectory(@"SOMEDIR");
            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo(@"SOMEDIR");
            Assert.NotNull(dirInfo);

            for (int i = 0; i < 2000; ++i)
            {
                fs.CreateDirectory(@"SOMEDIR\Fred");
                dirInfo.GetDirectories(@"Fred")[0].Delete();
            }
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void Move(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.CreateDirectory(@"SOMEDIR\CHILD\GCHILD");
            fs.GetDirectoryInfo(@"SOMEDIR\CHILD").MoveTo("NEWDIR");

            Assert.Equal(2, fs.Root.GetDirectories().Length);
            Assert.Equal(0, fs.Root.GetDirectories("SOMEDIR")[0].GetDirectories().Length);
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void Extension(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            Assert.Equal("dir", fs.GetDirectoryInfo("fred.dir").Extension);
            Assert.Equal("", fs.GetDirectoryInfo("fred").Extension);
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void GetDirectories(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.CreateDirectory(@"SOMEDIR\CHILD\GCHILD");
            fs.CreateDirectory(@"A.DIR");

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

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void GetDirectories_BadPath(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            Assert.Throws<DirectoryNotFoundException>(() => fs.GetDirectories(@"\baddir"));
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void GetFiles(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.CreateDirectory(@"SOMEDIR\CHILD\GCHILD");
            fs.CreateDirectory(@"AAA.DIR");
            using (Stream s = fs.OpenFile(@"FOO.TXT", FileMode.Create)) { }
            using (Stream s = fs.OpenFile(@"SOMEDIR\CHILD.TXT", FileMode.Create)) { }
            using (Stream s = fs.OpenFile(@"SOMEDIR\FOO.TXT", FileMode.Create)) { }
            using (Stream s = fs.OpenFile(@"SOMEDIR\CHILD\GCHILD\BAR.TXT", FileMode.Create)) { }

            Assert.Equal(1, fs.Root.GetFiles().Length);
            Assert.Equal("FOO.TXT", fs.Root.GetFiles()[0].FullName);

            Assert.Equal(2, fs.Root.GetDirectories("SOMEDIR")[0].GetFiles("*.TXT").Length);
            Assert.Equal(4, fs.Root.GetFiles("*.TXT", SearchOption.AllDirectories).Length);

            Assert.Equal(0, fs.Root.GetFiles("*.DIR", SearchOption.AllDirectories).Length);
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void GetFileSystemInfos(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.CreateDirectory(@"SOMEDIR\CHILD\GCHILD");
            fs.CreateDirectory(@"AAA.EXT");
            using (Stream s = fs.OpenFile(@"FOO.TXT", FileMode.Create)) { }
            using (Stream s = fs.OpenFile(@"SOMEDIR\CHILD.EXT", FileMode.Create)) { }
            using (Stream s = fs.OpenFile(@"SOMEDIR\FOO.TXT", FileMode.Create)) { }
            using (Stream s = fs.OpenFile(@"SOMEDIR\CHILD\GCHILD\BAR.TXT", FileMode.Create)) { }

            Assert.Equal(3, fs.Root.GetFileSystemInfos().Length);

            Assert.Equal(1, fs.Root.GetFileSystemInfos("*.EXT").Length);
            Assert.Equal(2, fs.Root.GetFileSystemInfos("*.?XT").Length);
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void Parent(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.CreateDirectory("SOMEDIR");

            Assert.Equal(fs.Root, fs.Root.GetDirectories("SOMEDIR")[0].Parent);
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void Parent_Root(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            Assert.Null(fs.Root.Parent);
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void CreationTimeUtc(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.CreateDirectory("DIR");

            Assert.True(DateTime.UtcNow >= fs.Root.GetDirectories("DIR")[0].CreationTimeUtc);
            Assert.True(DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(10)) <= fs.Root.GetDirectories("DIR")[0].CreationTimeUtc);
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void CreationTime(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.CreateDirectory("DIR");

            Assert.True(DateTime.Now >= fs.Root.GetDirectories("DIR")[0].CreationTime);
            Assert.True(DateTime.Now.Subtract(TimeSpan.FromSeconds(10)) <= fs.Root.GetDirectories("DIR")[0].CreationTime);
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void LastAccessTime(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.CreateDirectory("DIR");
            DiscDirectoryInfo di = fs.GetDirectoryInfo("DIR");

            DateTime baseTime = DateTime.Now - TimeSpan.FromDays(2);
            di.LastAccessTime = baseTime;

            fs.CreateDirectory(@"DIR\CHILD");

            Assert.True(baseTime < di.LastAccessTime);
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void LastWriteTime(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.CreateDirectory("DIR");
            DiscDirectoryInfo di = fs.GetDirectoryInfo("DIR");

            DateTime baseTime = DateTime.Now - TimeSpan.FromMinutes(10);
            di.LastWriteTime = baseTime;

            fs.CreateDirectory(@"DIR\CHILD");

            Assert.True(baseTime < di.LastWriteTime);
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void Equals(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            Assert.Equal(fs.GetDirectoryInfo("foo"), fs.GetDirectoryInfo("foo"));
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void RootBehaviour(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            // Not all file systems can modify the root directory, so we just make sure 'get' and 'no-op' change work.
            fs.Root.Attributes = fs.Root.Attributes;
            fs.Root.CreationTimeUtc = fs.Root.CreationTimeUtc;
            fs.Root.LastAccessTimeUtc = fs.Root.LastAccessTimeUtc;
            fs.Root.LastWriteTimeUtc = fs.Root.LastWriteTimeUtc;
        }
    }

}
