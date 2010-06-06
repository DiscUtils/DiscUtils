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

using System;
using System.Collections.Generic;
using System.IO;
using DiscUtils.Vfs;

namespace DiscUtils
{
    /// <summary>
    /// FileSystemManager determines which file system(s) is present on a volume.
    /// </summary>
    public static class FileSystemManager
    {
        private static List<VfsFileSystemFactory> s_DetectedFactories;

        /// <summary>
        /// Detect which file systems are present on a volume.
        /// </summary>
        /// <param name="volume">The volume to inspect</param>
        /// <returns>The list of file systems detected.</returns>
        public static FileSystemInfo[] DetectFileSystems(VolumeInfo volume)
        {
            using (Stream s = volume.Open())
            {
                return DoDetect(s, volume);
            }
        }

        /// <summary>
        /// Detect which file systems are present in a stream.
        /// </summary>
        /// <param name="stream">The stream to inspect</param>
        /// <returns>The list of file systems detected.</returns>
        public static FileSystemInfo[] DetectFileSystems(Stream stream)
        {
            return DoDetect(stream, null);
        }

        private static FileSystemInfo[] DoDetect(Stream stream, VolumeInfo volume)
        {
            BufferedStream detectStream = new BufferedStream(stream);
            List<FileSystemInfo> detected = new List<FileSystemInfo>();

            foreach (var factory in DetectedFactories)
            {
                detected.AddRange(factory.Detect(detectStream, volume));
            }

            return detected.ToArray();
        }

        private static List<VfsFileSystemFactory> DetectedFactories
        {
            get
            {
                if (s_DetectedFactories == null)
                {
                    List<VfsFileSystemFactory> factories = new List<VfsFileSystemFactory>();

                    foreach (var type in typeof(VolumeManager).Assembly.GetTypes())
                    {
                        foreach (VfsFileSystemFactoryAttribute attr in Attribute.GetCustomAttributes(type, typeof(VfsFileSystemFactoryAttribute), false))
                        {
                            factories.Add((VfsFileSystemFactory)Activator.CreateInstance(type));
                        }
                    }

                    s_DetectedFactories = factories;
                }

                return s_DetectedFactories;
            }
        }
    }
}
