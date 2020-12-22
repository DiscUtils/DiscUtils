//
// Copyright (c) 2019, Quamotion bvba
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

using DiscUtils;
using DiscUtils.Dmg;
using DiscUtils.HfsPlus;
using DiscUtils.Setup;
using DiscUtils.Streams;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace LibraryTests.HfsPlus
{
    public class HfsPlusTest
    {
        private const string SystemVersionPath = @"System\Library\CoreServices\SystemVersion.plist";
        private const string DeviceSupportPath = "/Applications/Xcode.app/Content/Developer/Platforms/iPhoneOS.Platform/DeviceSupport/";
        static HfsPlusTest()
        {
            SetupHelper.RegisterAssembly(typeof(HfsPlusFileSystem).Assembly);
        }

#if NETCOREAPP
        public static IEnumerable<object[]> GetDeveloperDiskImages()
        {
            if (!Directory.Exists(DeviceSupportPath))
            {
                yield break;
            }

            foreach (var directory in Directory.GetDirectories(DeviceSupportPath))
            {
                yield return new object[] { Path.Combine(directory, "DeveloperDiskImage.dmg") };
            }
        }

        [MemberData(nameof(GetDeveloperDiskImages))]
        [MacOSOnlyTheory]
        public void ReadFilesystemTest(string path)
        {
            using (Stream developerDiskImageStream = File.OpenRead(path))
            using (var disk = new Disk(developerDiskImageStream, Ownership.None))
            {
                // Find the first (and supposedly, only, HFS partition)
                var volumes = VolumeManager.GetPhysicalVolumes(disk);
                foreach (var volume in volumes)
                {
                    var fileSystems = FileSystemManager.DetectFileSystems(volume);

                    var fileSystem = Assert.Single(fileSystems);
                    Assert.Equal("HFS+", fileSystem.Name);

                    using (HfsPlusFileSystem hfs = (HfsPlusFileSystem)fileSystem.Open(volume))
                    {
                        Assert.True(hfs.FileExists(SystemVersionPath));

                        using (Stream systemVersionStream = hfs.OpenFile(SystemVersionPath, FileMode.Open, FileAccess.Read))
                        using (MemoryStream copyStream = new MemoryStream())
                        {
                            Assert.NotEqual(0, systemVersionStream.Length);
                            systemVersionStream.CopyTo(copyStream);
                            Assert.Equal(systemVersionStream.Length, copyStream.Length);

                            copyStream.Seek(0, SeekOrigin.Begin);
                            Plist.Parse(copyStream);
                        }
                    }
                }
            }
        }
#endif
    }
}
