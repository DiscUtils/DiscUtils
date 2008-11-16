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
using System.IO;

namespace DiscUtils.Iso9660
{
    /// <summary>
    /// Class that creates ISO images.
    /// </summary>
    /// <example>
    /// <code>
    ///   CDBuilder builder = new CDBuilder();
    ///   builder.VolumeIdentifier = "MYISO";
    ///   builder.UseJoliet = true;
    ///   builder.AddFile("Hello.txt", Encoding.ASCII.GetBytes("hello world!"));
    ///   builder.Build(@"C:\TEMP\myiso.iso");
    /// </code>
    /// </example>
    public class CDBuilder
    {
        private List<BuildFileInfo> _files;
        private List<BuildDirectoryInfo> _dirs;
        private BuildDirectoryInfo _rootDirectory;

        private BuildParameters _buildParams;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public CDBuilder()
        {
            _files = new List<BuildFileInfo>();
            _dirs = new List<BuildDirectoryInfo>();
            _rootDirectory = new BuildDirectoryInfo("\0", null);
            _dirs.Add(_rootDirectory);

            _buildParams = new BuildParameters();
            _buildParams.UseJoliet = true;
        }

        /// <summary>
        /// Initiates a layout of the ISO image, returning a Stream.
        /// </summary>
        /// <returns>The stream containing the ISO image.</returns>
        /// <remarks>
        /// The ISO is built as it is read and not held in memory or on disk.  To obtain the
        /// full image read sequentially from the start to the end of the stream.  However
        /// seeking is supported, so reading arbitrary byte locations from the stream is also
        /// possible.
        /// </remarks>
        public Stream Build()
        {
            return new CDBuildStream(_files, _dirs, _rootDirectory, _buildParams);
        }

        /// <summary>
        /// Initiates a layout of the ISO image, returning a Stream.
        /// </summary>
        /// <param name="file">The file to write the ISO image to.</param>
        public void Build(string file)
        {
            using (CDBuildStream cdStream = new CDBuildStream(_files, _dirs, _rootDirectory, _buildParams))
            {
                using (FileStream fileStream = new FileStream(file, FileMode.Create, FileAccess.Write))
                {
                    byte[] buffer = new byte[2048];
                    int numRead = cdStream.Read(buffer, 0, buffer.Length);
                    while (numRead != 0)
                    {
                        fileStream.Write(buffer, 0, numRead);
                        numRead = cdStream.Read(buffer, 0, buffer.Length);
                    }
                }
            }
        }

        /// <summary>
        /// The Volume Identifier for the ISO file.
        /// </summary>
        /// <remarks>
        /// Must be a valid identifier, i.e. max 32 characters in the range A-Z, 0-9 or _.
        /// Lower-case characters are not permitted.
        /// </remarks>
        public string VolumeIdentifier
        {
            get { return _buildParams.VolumeIdentifier; }
            set {
                if (value.Length > 32 || !IsoUtilities.isValidDString(value))
                {
                    throw new ArgumentException("Not a valid volume identifier");
                }
                else
                {
                    _buildParams.VolumeIdentifier = value;
                }
            }
        }

        /// <summary>
        /// Indicates whether Joliet file-system extensions should be used.
        /// </summary>
        public bool UseJoliet
        {
            get { return _buildParams.UseJoliet; }
            set { _buildParams.UseJoliet = value; }
        }

        /// <summary>
        /// Adds a directory to the ISO image.
        /// </summary>
        /// <param name="name">The name of the directory on the ISO image.</param>
        /// <returns>The object representing this directory</returns>
        /// <remarks>
        /// The name is the full path to the directory, for example:
        /// <example><code>
        ///   builder.AddDirectory(@"DIRA\DIRB\DIRC");
        /// </code></example>
        /// </remarks>
        public BuildDirectoryInfo AddDirectory(string name)
        {
            string[] nameElements = name.Split('\\');
            return GetDirectory(nameElements, nameElements.Length, true);
        }

        /// <summary>
        /// Adds a byte array to the ISO image as a file.
        /// </summary>
        /// <param name="name">The name of the file on the ISO image.</param>
        /// <param name="content">The contents of the file.</param>
        /// <returns>The object representing this file.</returns>
        /// <remarks>
        /// The name is the full path to the file, for example:
        /// <example><code>
        ///   builder.AddFile(@"DIRA\DIRB\FILE.TXT;1", new byte[]{0,1,2});
        /// </code></example>
        /// <para>Note the version number at the end of the file name is optional, if not
        /// specified the default of 1 will be used.</para>
        /// </remarks>
        public BuildFileInfo AddFile(string name, byte[] content)
        {
            string[] nameElements = name.Split('\\');
            BuildDirectoryInfo dir = GetDirectory(nameElements, nameElements.Length - 1, true);

            BuildDirectoryMember existing;
            if (dir.TryGetMember(nameElements[nameElements.Length - 1], out existing))
            {
                throw new IOException("File already exists");
            }
            else
            {
                BuildFileInfo fi = new BuildFileInfo(nameElements[nameElements.Length - 1], dir, content);
                _files.Add(fi);
                dir.Add(fi);
                return fi;
            }
        }

        /// <summary>
        /// Adds a disk file to the ISO image as a file.
        /// </summary>
        /// <param name="name">The name of the file on the ISO image.</param>
        /// <param name="sourcePath">The name of the file on disk.</param>
        /// <returns>The object representing this file.</returns>
        /// <remarks>
        /// The name is the full path to the file, for example:
        /// <example><code>
        ///   builder.AddFile(@"DIRA\DIRB\FILE.TXT;1", @"C:\temp\tempfile.bin");
        /// </code></example>
        /// <para>Note the version number at the end of the file name is optional, if not
        /// specified the default of 1 will be used.</para>
        /// </remarks>
        public BuildFileInfo AddFile(string name, string sourcePath)
        {
            string[] nameElements = name.Split(new char[]{'\\'}, StringSplitOptions.RemoveEmptyEntries);
            BuildDirectoryInfo dir = GetDirectory(nameElements, nameElements.Length - 1, true);

            BuildDirectoryMember existing;
            if (dir.TryGetMember(nameElements[nameElements.Length - 1], out existing))
            {
                throw new IOException("File already exists");
            }
            else
            {
                BuildFileInfo fi = new BuildFileInfo(nameElements[nameElements.Length - 1], dir, sourcePath);
                _files.Add(fi);
                dir.Add(fi);
                return fi;
            }
        }

        /// <summary>
        /// Adds a stream to the ISO image as a file.
        /// </summary>
        /// <param name="name">The name of the file on the ISO image.</param>
        /// <param name="source">The contents of the file.</param>
        /// <returns>The object representing this file.</returns>
        /// <remarks>
        /// The name is the full path to the file, for example:
        /// <example><code>
        ///   builder.AddFile(@"DIRA\DIRB\FILE.TXT;1", stream);
        /// </code></example>
        /// <para>Note the version number at the end of the file name is optional, if not
        /// specified the default of 1 will be used.</para>
        /// </remarks>
        public BuildFileInfo AddFile(string name, Stream source)
        {
            if (!source.CanSeek)
            {
                throw new ArgumentException("source doesn't support seeking", "source");
            }

            string[] nameElements = name.Split('\\');
            BuildDirectoryInfo dir = GetDirectory(nameElements, nameElements.Length - 1, true);

            BuildDirectoryMember existing;
            if (dir.TryGetMember(nameElements[nameElements.Length - 1], out existing))
            {
                throw new IOException("File already exists");
            }
            else
            {
                BuildFileInfo fi = new BuildFileInfo(name, dir, source);
                _files.Add(fi);
                dir.Add(fi);
                return fi;
            }
        }

        private BuildDirectoryInfo GetDirectory(string[] path, int pathLength, bool createMissing)
        {
            BuildDirectoryInfo di = TryGetDirectory(path, pathLength, createMissing);

            if (di == null)
            {
                throw new DirectoryNotFoundException("Directory not found");
            }

            return di;
        }

        private BuildDirectoryInfo TryGetDirectory(string[] path, int pathLength, bool createMissing)
        {
            BuildDirectoryInfo focus = _rootDirectory;

            for (int i = 0; i < pathLength; ++i)
            {
                BuildDirectoryMember next;
                if (!focus.TryGetMember(path[i], out next))
                {
                    if (createMissing)
                    {
                        // This directory doesn't exist, create it...
                        BuildDirectoryInfo di = new BuildDirectoryInfo(path[i], focus);
                        focus.Add(di);
                        _dirs.Add(di);
                        focus = di;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    BuildDirectoryInfo nextAsBuildDirectoryInfo = next as BuildDirectoryInfo;
                    if (nextAsBuildDirectoryInfo == null)
                    {
                        throw new IOException("File with conflicting name exists");
                    }
                    else
                    {
                        focus = nextAsBuildDirectoryInfo;
                    }
                }
            }

            return focus;
        }


    }


}
