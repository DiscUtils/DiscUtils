//
// Copyright (c) 2008-2009, Kenneth Bell
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
using System.IO;
using DiscUtils.Vhd;
using NUnit.Framework;

namespace DiscUtils
{
    [TestFixture]
    public class DiscFileSystemDirectoryTest
    {
        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Create(DiscFileSystem fs)
        {
            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo("SOMEDIR");
            dirInfo.Create();

            Assert.AreEqual(1, fs.Root.GetDirectories().Length);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void CreateRecursive(DiscFileSystem fs)
        {
            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo(@"SOMEDIR\CHILDDIR");
            dirInfo.Create();

            Assert.AreEqual(1, fs.Root.GetDirectories().Length);
            Assert.AreEqual(1, fs.GetDirectoryInfo(@"SOMEDIR").GetDirectories().Length);
            Assert.AreEqual("CHILDDIR", fs.GetDirectoryInfo(@"SOMEDIR").GetDirectories()[0].Name);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void CreateExisting(DiscFileSystem fs)
        {
            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo("SOMEDIR");
            dirInfo.Create();
            dirInfo.Create();

            Assert.AreEqual(1, fs.Root.GetDirectories().Length);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        [ExpectedException(typeof(IOException))]
        [Category("ThrowsException")]
        public void CreateInvalid_Long(DiscFileSystem fs)
        {
            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo(new String('X', 256));
            dirInfo.Create();
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        [ExpectedException(typeof(IOException))]
        [Category("ThrowsException")]
        public void CreateInvalid_Characters(DiscFileSystem fs)
        {
            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo("SOME\0DIR");
            dirInfo.Create();
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Exists(DiscFileSystem fs)
        {
            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo(@"SOMEDIR\CHILDDIR");
            dirInfo.Create();

            Assert.IsTrue(fs.GetDirectoryInfo(@"\").Exists);
            Assert.IsTrue(fs.GetDirectoryInfo(@"SOMEDIR").Exists);
            Assert.IsTrue(fs.GetDirectoryInfo(@"SOMEDIR\CHILDDIR").Exists);
            Assert.IsTrue(fs.GetDirectoryInfo(@"SOMEDIR\CHILDDIR\").Exists);
            Assert.IsFalse(fs.GetDirectoryInfo(@"NONDIR").Exists);
            Assert.IsFalse(fs.GetDirectoryInfo(@"SOMEDIR\NONDIR").Exists);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void FullName(DiscFileSystem fs)
        {
            Assert.AreEqual(@"\", fs.Root.FullName);
            Assert.AreEqual(@"SOMEDIR\", fs.GetDirectoryInfo(@"SOMEDIR").FullName);
            Assert.AreEqual(@"SOMEDIR\CHILDDIR\", fs.GetDirectoryInfo(@"SOMEDIR\CHILDDIR").FullName);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Delete(DiscFileSystem fs)
        {
            fs.CreateDirectory(@"Fred");
            Assert.AreEqual(1, fs.Root.GetDirectories().Length);

            fs.Root.GetDirectories(@"Fred")[0].Delete();
            Assert.AreEqual(0, fs.Root.GetDirectories().Length);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void DeleteRecursive(DiscFileSystem fs)
        {
            fs.CreateDirectory(@"Fred\child");
            Assert.AreEqual(1, fs.Root.GetDirectories().Length);

            fs.Root.GetDirectories(@"Fred")[0].Delete(true);
            Assert.AreEqual(0, fs.Root.GetDirectories().Length);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        [ExpectedException(typeof(IOException))]
        [Category("ThrowsException")]
        public void DeleteRoot(DiscFileSystem fs)
        {
            fs.Root.Delete();
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        [ExpectedException(typeof(IOException))]
        [Category("ThrowsException")]
        public void DeleteNonEmpty_NonRecursive(DiscFileSystem fs)
        {
            fs.CreateDirectory(@"Fred\child");
            fs.Root.GetDirectories(@"Fred")[0].Delete();
        }

        [TestCaseSource(typeof(FileSystemSource), "QuickReadWriteFileSystems")]
        [Category("SlowTest")]
        public void CreateDeleteLeakTest(DiscFileSystem fs)
        {
            for (int i = 0; i < 2000; ++i)
            {
                fs.CreateDirectory(@"Fred");
                fs.Root.GetDirectories(@"Fred")[0].Delete();
            }

            fs.CreateDirectory(@"SOMEDIR");
            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo(@"SOMEDIR");
            Assert.IsNotNull(dirInfo);

            for (int i = 0; i < 2000; ++i)
            {
                fs.CreateDirectory(@"SOMEDIR\Fred");
                dirInfo.GetDirectories(@"Fred")[0].Delete();
            }
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Move(DiscFileSystem fs)
        {
            fs.CreateDirectory(@"SOMEDIR\CHILD\GCHILD");
            fs.GetDirectoryInfo(@"SOMEDIR\CHILD").MoveTo("NEWDIR");

            Assert.AreEqual(2, fs.Root.GetDirectories().Length);
            Assert.AreEqual(0, fs.Root.GetDirectories("SOMEDIR")[0].GetDirectories().Length);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Extension(DiscFileSystem fs)
        {
            Assert.AreEqual("dir", fs.GetDirectoryInfo("fred.dir").Extension);
            Assert.AreEqual("", fs.GetDirectoryInfo("fred").Extension);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void GetDirectories(DiscFileSystem fs)
        {
            fs.CreateDirectory(@"SOMEDIR\CHILD\GCHILD");
            fs.CreateDirectory(@"A.DIR");

            Assert.AreEqual(2, fs.Root.GetDirectories().Length);

            DiscDirectoryInfo someDir = fs.Root.GetDirectories(@"SoMeDir")[0];
            Assert.AreEqual(1, fs.Root.GetDirectories("SOMEDIR").Length);
            Assert.AreEqual("SOMEDIR", someDir.Name);

            Assert.AreEqual(1, someDir.GetDirectories("*.*").Length);
            Assert.AreEqual("CHILD", someDir.GetDirectories("*.*")[0].Name);
            Assert.AreEqual(2, someDir.GetDirectories("*.*", SearchOption.AllDirectories).Length);

            Assert.AreEqual(4, fs.Root.GetDirectories("*.*", SearchOption.AllDirectories).Length);
            Assert.AreEqual(2, fs.Root.GetDirectories("*.*", SearchOption.TopDirectoryOnly).Length);

            Assert.AreEqual(1, fs.Root.GetDirectories("*.DIR", SearchOption.AllDirectories).Length);
            Assert.AreEqual(@"A.DIR\", fs.Root.GetDirectories("*.DIR", SearchOption.AllDirectories)[0].FullName);

            Assert.AreEqual(1, fs.Root.GetDirectories("GCHILD", SearchOption.AllDirectories).Length);
            Assert.AreEqual(@"SOMEDIR\CHILD\GCHILD\", fs.Root.GetDirectories("GCHILD", SearchOption.AllDirectories)[0].FullName);
        }

        [ExpectedException(typeof(DirectoryNotFoundException))]
        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void GetDirectories_BadPath(DiscFileSystem fs)
        {
            fs.GetDirectories(@"\baddir");
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void GetFiles(DiscFileSystem fs)
        {
            fs.CreateDirectory(@"SOMEDIR\CHILD\GCHILD");
            fs.CreateDirectory(@"AAA.DIR");
            using (Stream s = fs.OpenFile(@"FOO.TXT", FileMode.Create)) { }
            using (Stream s = fs.OpenFile(@"SOMEDIR\CHILD.TXT", FileMode.Create)) { }
            using (Stream s = fs.OpenFile(@"SOMEDIR\FOO.TXT", FileMode.Create)) { }
            using (Stream s = fs.OpenFile(@"SOMEDIR\CHILD\GCHILD\BAR.TXT", FileMode.Create)) { }

            Assert.AreEqual(1, fs.Root.GetFiles().Length);
            Assert.AreEqual("FOO.TXT", fs.Root.GetFiles()[0].FullName);

            Assert.AreEqual(2, fs.Root.GetDirectories("SOMEDIR")[0].GetFiles("*.TXT").Length);
            Assert.AreEqual(4, fs.Root.GetFiles("*.TXT", SearchOption.AllDirectories).Length);

            Assert.AreEqual(0, fs.Root.GetFiles("*.DIR", SearchOption.AllDirectories).Length);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void GetFileSystemInfos(DiscFileSystem fs)
        {
            fs.CreateDirectory(@"SOMEDIR\CHILD\GCHILD");
            fs.CreateDirectory(@"AAA.EXT");
            using (Stream s = fs.OpenFile(@"FOO.TXT", FileMode.Create)) { }
            using (Stream s = fs.OpenFile(@"SOMEDIR\CHILD.EXT", FileMode.Create)) { }
            using (Stream s = fs.OpenFile(@"SOMEDIR\FOO.TXT", FileMode.Create)) { }
            using (Stream s = fs.OpenFile(@"SOMEDIR\CHILD\GCHILD\BAR.TXT", FileMode.Create)) { }

            Assert.AreEqual(3, fs.Root.GetFileSystemInfos().Length);

            Assert.AreEqual(1, fs.Root.GetFileSystemInfos("*.EXT").Length);
            Assert.AreEqual(2, fs.Root.GetFileSystemInfos("*.?XT").Length);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Parent(DiscFileSystem fs)
        {
            fs.CreateDirectory("SOMEDIR");

            Assert.AreEqual(fs.Root, fs.Root.GetDirectories("SOMEDIR")[0].Parent);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Parent_Root(DiscFileSystem fs)
        {
            Assert.IsNull(fs.Root.Parent);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void CreationTimeUtc(DiscFileSystem fs)
        {
            fs.CreateDirectory("DIR");

            Assert.GreaterOrEqual(DateTime.UtcNow, fs.Root.GetDirectories("DIR")[0].CreationTimeUtc);
            Assert.LessOrEqual(DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(10)), fs.Root.GetDirectories("DIR")[0].CreationTimeUtc);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void CreationTime(DiscFileSystem fs)
        {
            fs.CreateDirectory("DIR");

            Assert.GreaterOrEqual(DateTime.Now, fs.Root.GetDirectories("DIR")[0].CreationTime);
            Assert.LessOrEqual(DateTime.Now.Subtract(TimeSpan.FromSeconds(10)), fs.Root.GetDirectories("DIR")[0].CreationTime);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void LastAccessTime(DiscFileSystem fs)
        {
            fs.CreateDirectory("DIR");
            DiscDirectoryInfo di = fs.GetDirectoryInfo("DIR");

            DateTime baseTime = DateTime.Now - TimeSpan.FromDays(2);
            di.LastAccessTime = baseTime;

            fs.CreateDirectory(@"DIR\CHILD");

            Assert.Less(baseTime, di.LastAccessTime);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void LastWriteTime(DiscFileSystem fs)
        {
            fs.CreateDirectory("DIR");
            DiscDirectoryInfo di = fs.GetDirectoryInfo("DIR");

            DateTime baseTime = DateTime.Now - TimeSpan.FromMinutes(10);
            di.LastWriteTime = baseTime;

            fs.CreateDirectory(@"DIR\CHILD");

            Assert.Less(baseTime, di.LastWriteTime);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Equals(DiscFileSystem fs)
        {
            Assert.AreEqual(fs.GetDirectoryInfo("foo"), fs.GetDirectoryInfo("foo"));
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void RootBehaviour(DiscFileSystem fs)
        {
            // Not all file systems can modify the root directory, so we just make sure 'get' and 'no-op' change work.
            fs.Root.Attributes = fs.Root.Attributes;
            fs.Root.CreationTimeUtc = fs.Root.CreationTimeUtc;
            fs.Root.LastAccessTimeUtc = fs.Root.LastAccessTimeUtc;
            fs.Root.LastWriteTimeUtc = fs.Root.LastWriteTimeUtc;
        }
    }

    public class FileSystemSource
    {
        public FileSystemSource()
        {
        }

        public IEnumerable<TestCaseData> ReadWriteFileSystems
        {
            get
            {
                yield return new TestCaseData(
                    new DelayLoadFileSystem(FatFileSystem)).SetName("FAT");

                // TODO: When format code complete, format a vanilla partition rather than relying on file on disk
                yield return new TestCaseData(
                    new DelayLoadFileSystem(DiagnosticNtfsFileSystem)).SetName("NTFS");
            }
        }


        public IEnumerable<TestCaseData> QuickReadWriteFileSystems
        {
            get
            {
                yield return new TestCaseData(
                    new DelayLoadFileSystem(FatFileSystem)).SetName("FAT");

                yield return new TestCaseData(
                    new DelayLoadFileSystem(NtfsFileSystem)).SetName("NTFS");
            }
        }

        private static DiscFileSystem FatFileSystem()
        {
            SparseMemoryBuffer buffer = new SparseMemoryBuffer(4096);
            SparseMemoryStream ms = new SparseMemoryStream();
            Geometry diskGeometry = Geometry.FromCapacity(30 * 1024 * 1024);
            return Fat.FatFileSystem.FormatFloppy(ms, FloppyDiskType.Extended, null);
        }

        public DiscFileSystem DiagnosticNtfsFileSystem()
        {
            SparseMemoryBuffer buffer = new SparseMemoryBuffer(4096);
            SparseMemoryStream ms = new SparseMemoryStream();
            Geometry diskGeometry = Geometry.FromCapacity(30 * 1024 * 1024);
            Ntfs.NtfsFileSystem.Format(ms, "", diskGeometry, 0, diskGeometry.TotalSectors);
            var discFs = new DiscUtils.Diagnostics.ValidatingFileSystem<Ntfs.NtfsFileSystem, Ntfs.NtfsFileSystemChecker>(ms);
            discFs.CheckpointInterval = 1;
            discFs.GlobalIOTraceCapturesStackTraces = false;
            return discFs;
        }

        public Ntfs.NtfsFileSystem NtfsFileSystem()
        {
            SparseMemoryBuffer buffer = new SparseMemoryBuffer(4096);
            SparseMemoryStream ms = new SparseMemoryStream();
            Geometry diskGeometry = Geometry.FromCapacity(30 * 1024 * 1024);
            return Ntfs.NtfsFileSystem.Format(ms, "", diskGeometry, 0, diskGeometry.TotalSectors);
        }

        private delegate DiscFileSystem FileSystemLoaderDelegate();

        private class DelayLoadFileSystem : DiscFileSystem
        {
            private FileSystemLoaderDelegate _loader;
            private DiscFileSystem _wrapped;

            public DelayLoadFileSystem(FileSystemLoaderDelegate loader)
            {
                _loader = loader;
            }

            private void Load()
            {
                if (_wrapped == null)
                {
                    _wrapped = _loader();
                }
            }

            public override string FriendlyName
            {
                get
                {
                    Load();
                    return _wrapped.FriendlyName;
                }
            }

            public override bool CanWrite
            {
                get
                {
                    Load();
                    return _wrapped.CanWrite;
                }
            }

            public override void CopyFile(string sourceFile, string destinationFile, bool overwrite)
            {
                Load();
                _wrapped.CopyFile(sourceFile, destinationFile, overwrite);
            }

            public override void CreateDirectory(string path)
            {
                Load();
                _wrapped.CreateDirectory(path);
            }

            public override void DeleteDirectory(string path)
            {
                Load();
                _wrapped.DeleteDirectory(path);
            }

            public override void DeleteFile(string path)
            {
                Load();
                _wrapped.DeleteFile(path);
            }

            public override bool DirectoryExists(string path)
            {
                Load();
                return _wrapped.DirectoryExists(path);
            }

            public override bool FileExists(string path)
            {
                Load();
                return _wrapped.FileExists(path);
            }

            public override string[] GetDirectories(string path, string searchPattern, SearchOption searchOption)
            {
                Load();
                return _wrapped.GetDirectories(path, searchPattern, searchOption);
            }

            public override string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
            {
                Load();
                return _wrapped.GetFiles(path, searchPattern, searchOption);
            }

            public override string[] GetFileSystemEntries(string path)
            {
                Load();
                return _wrapped.GetFileSystemEntries(path);
            }

            public override string[] GetFileSystemEntries(string path, string searchPattern)
            {
                Load();
                return _wrapped.GetFileSystemEntries(path, searchPattern);
            }

            public override void MoveDirectory(string sourceDirectoryName, string destinationDirectoryName)
            {
                Load();
                _wrapped.MoveDirectory(sourceDirectoryName, destinationDirectoryName);
            }

            public override void MoveFile(string sourceName, string destinationName, bool overwrite)
            {
                Load();
                _wrapped.MoveFile(sourceName, destinationName, overwrite);
            }

            public override Stream OpenFile(string path, FileMode mode, FileAccess access)
            {
                Load();
                return _wrapped.OpenFile(path, mode, access);
            }

            public override FileAttributes GetAttributes(string path)
            {
                Load();
                return _wrapped.GetAttributes(path);
            }

            public override void SetAttributes(string path, FileAttributes newValue)
            {
                Load();
                _wrapped.SetAttributes(path, newValue);
            }

            public override DateTime GetCreationTimeUtc(string path)
            {
                Load();
                return _wrapped.GetCreationTimeUtc(path);
            }

            public override void SetCreationTimeUtc(string path, DateTime newTime)
            {
                Load();
                _wrapped.SetCreationTimeUtc(path, newTime);
            }

            public override DateTime GetLastAccessTimeUtc(string path)
            {
                Load();
                return _wrapped.GetLastAccessTimeUtc(path);
            }

            public override void SetLastAccessTimeUtc(string path, DateTime newTime)
            {
                Load();
                _wrapped.SetLastAccessTimeUtc(path, newTime);
            }

            public override DateTime GetLastWriteTimeUtc(string path)
            {
                Load();
                return _wrapped.GetLastWriteTimeUtc(path);
            }

            public override void SetLastWriteTimeUtc(string path, DateTime newTime)
            {
                Load();
                _wrapped.SetLastWriteTimeUtc(path, newTime);
            }

            public override long GetFileLength(string path)
            {
                Load();
                return _wrapped.GetFileLength(path);
            }

            public override string VolumeLabel
            {
                get
                {
                    Load();
                    return _wrapped.VolumeLabel;
                }
            }
        }
    }
}
