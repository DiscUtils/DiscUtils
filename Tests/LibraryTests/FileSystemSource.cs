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
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace DiscUtils
{
    public delegate DiscFileSystem NewFileSystemDelegate();

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
                    new NewFileSystemDelegate(FatFileSystem)).SetName("FAT");

                // TODO: When format code complete, format a vanilla partition rather than relying on file on disk
                yield return new TestCaseData(
                    new NewFileSystemDelegate(DiagnosticNtfsFileSystem)).SetName("NTFS");
            }
        }


        public IEnumerable<TestCaseData> QuickReadWriteFileSystems
        {
            get
            {
                yield return new TestCaseData(
                    new NewFileSystemDelegate(FatFileSystem)).SetName("FAT");

                yield return new TestCaseData(
                    new NewFileSystemDelegate(NtfsFileSystem)).SetName("NTFS");
            }
        }

        private static DiscFileSystem FatFileSystem()
        {
            SparseMemoryBuffer buffer = new SparseMemoryBuffer(4096);
            SparseMemoryStream ms = new SparseMemoryStream();
            Geometry diskGeometry = Geometry.FromCapacity(30 * 1024 * 1024);
            return Fat.FatFileSystem.FormatFloppy(ms, FloppyDiskType.Extended, null);
        }

        public static DiscFileSystem DiagnosticNtfsFileSystem()
        {
            SparseMemoryBuffer buffer = new SparseMemoryBuffer(4096);
            SparseMemoryStream ms = new SparseMemoryStream();
            Geometry diskGeometry = Geometry.FromCapacity(30 * 1024 * 1024);
            Ntfs.NtfsFileSystem.Format(ms, "", diskGeometry, 0, diskGeometry.TotalSectorsLong);
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
            return Ntfs.NtfsFileSystem.Format(ms, "", diskGeometry, 0, diskGeometry.TotalSectorsLong);
        }

    }
}
