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
using System.Reflection;
using DiscUtils.Vfs;

namespace DiscUtils
{
    /// <summary>
    /// FileSystemManager determines which file systems are present on a volume.
    /// </summary>
    /// <remarks>
    /// The static detection methods detect default file systems.  To plug in additional
    /// file systems, create an instance of this class and call RegisterFileSystems.
    /// </remarks>
    public sealed class FileSystemManager
    {
        private static List<VfsFileSystemFactory> s_DetectedFactories;
        private static FileSystemManager s_DefaultInstance = new FileSystemManager();

        private List<VfsFileSystemFactory> _factories;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public FileSystemManager()
        {
            _factories = new List<VfsFileSystemFactory>(DetectedFactories);
        }

        /// <summary>
        /// Registers new file systems with an instance of this class.
        /// </summary>
        /// <param name="factory">The detector for the new file systems</param>
        public void RegisterFileSystems(VfsFileSystemFactory factory)
        {
            _factories.Add(factory);
        }

        /// <summary>
        /// Registers new file systems detected in an assembly.
        /// </summary>
        /// <param name="assembly">The assembly to inspect</param>
        /// <remarks>
        /// To be detected, the <c>VfsFileSystemFactory</c> instances must be marked with the
        /// <c>VfsFileSystemFactoryAttribute</c>> attribute.
        /// </remarks>
        public void RegisterFileSystems(Assembly assembly)
        {
            _factories.AddRange(DetectFactories(assembly));
        }

        /// <summary>
        /// Detect which file systems are present on a volume.
        /// </summary>
        /// <param name="volume">The volume to inspect</param>
        /// <returns>The list of file systems detected.</returns>
        public static FileSystemInfo[] DetectDefaultFileSystems(VolumeInfo volume)
        {
            return s_DefaultInstance.DetectFileSystems(volume);
        }

        /// <summary>
        /// Detect which file systems are present in a stream.
        /// </summary>
        /// <param name="stream">The stream to inspect</param>
        /// <returns>The list of file systems detected.</returns>
        public static FileSystemInfo[] DetectDefaultFileSystems(Stream stream)
        {
            return s_DefaultInstance.DetectFileSystems(stream);
        }

        /// <summary>
        /// Detect which file systems are present on a volume.
        /// </summary>
        /// <param name="volume">The volume to inspect</param>
        /// <returns>The list of file systems detected.</returns>
        public FileSystemInfo[] DetectFileSystems(VolumeInfo volume)
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
        public FileSystemInfo[] DetectFileSystems(Stream stream)
        {
            return DoDetect(stream, null);
        }

        private FileSystemInfo[] DoDetect(Stream stream, VolumeInfo volume)
        {
            BufferedStream detectStream = new BufferedStream(stream);
            List<FileSystemInfo> detected = new List<FileSystemInfo>();

            foreach (var factory in _factories)
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
                    s_DetectedFactories = DetectFactories(typeof(FileSystemManager).Assembly);
                }

                return s_DetectedFactories;
            }
        }

        private static List<VfsFileSystemFactory> DetectFactories(Assembly assembly)
        {
            List<VfsFileSystemFactory> factories = new List<VfsFileSystemFactory>();

            foreach (var type in assembly.GetTypes())
            {
                foreach (VfsFileSystemFactoryAttribute attr in Attribute.GetCustomAttributes(type, typeof(VfsFileSystemFactoryAttribute), false))
                {
                    factories.Add((VfsFileSystemFactory)Activator.CreateInstance(type));
                }
            }
            return factories;
        }
    }
}
