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
using System.Text;

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
    public sealed class CDBuilder : StreamBuilder
    {
        private List<BuildFileInfo> _files;
        private List<BuildDirectoryInfo> _dirs;
        private BuildDirectoryInfo _rootDirectory;

        private BuildParameters _buildParams;

        private const long DiskStart = 0x8000;

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
                if (value.Length > 32)
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
                BuildFileInfo fi = new BuildFileInfo(nameElements[nameElements.Length - 1], dir, source);
                _files.Add(fi);
                dir.Add(fi);
                return fi;
            }
        }

        internal override List<BuilderExtent> FixExtents(out long totalLength)
        {
            List<BuilderExtent> fixedRegions = new List<BuilderExtent>();

            DateTime buildTime = DateTime.UtcNow;

            Encoding suppEncoding = _buildParams.UseJoliet ? Encoding.BigEndianUnicode : Encoding.ASCII;

            Dictionary<BuildDirectoryMember, uint> primaryLocationTable = new Dictionary<BuildDirectoryMember, uint>();
            Dictionary<BuildDirectoryMember, uint> supplementaryLocationTable = new Dictionary<BuildDirectoryMember, uint>();

            long focus = DiskStart + 3 * IsoUtilities.SectorSize; // Primary, Supplementary, End (fixed at end...)

            // ####################################################################
            // # 1. Fix file locations
            // ####################################################################

            // Find end of the file data, fixing the files in place as we go
            foreach (BuildFileInfo fi in _files)
            {
                primaryLocationTable.Add(fi, (uint)(focus / IsoUtilities.SectorSize));
                supplementaryLocationTable.Add(fi, (uint)(focus / IsoUtilities.SectorSize));
                FileExtent extent = new FileExtent(fi, focus);

                // Only remember files of non-zero length (otherwise we'll stomp on a valid file)
                if (extent.Length != 0)
                {
                    fixedRegions.Add(extent);
                }

                focus += Utilities.RoundUp(extent.Length, IsoUtilities.SectorSize);
            }


            // ####################################################################
            // # 2. Fix directory locations
            // ####################################################################

            // There are two directory tables
            //  1. Primary        (std ISO9660)
            //  2. Supplementary  (Joliet)

            // Find start of the second set of directory data, fixing ASCII directories in place.
            long startOfFirstDirData = focus;
            foreach (BuildDirectoryInfo di in _dirs)
            {
                primaryLocationTable.Add(di, (uint)(focus / IsoUtilities.SectorSize));
                DirectoryExtent extent = new DirectoryExtent(di, primaryLocationTable, Encoding.ASCII, focus);
                fixedRegions.Add(extent);
                focus += Utilities.RoundUp(extent.Length, IsoUtilities.SectorSize);
            }

            // Find end of the second directory table, fixing supplementary directories in place.
            long startOfSecondDirData = focus;
            foreach (BuildDirectoryInfo di in _dirs)
            {
                supplementaryLocationTable.Add(di, (uint)(focus / IsoUtilities.SectorSize));
                DirectoryExtent extent = new DirectoryExtent(di, supplementaryLocationTable, suppEncoding, focus);
                fixedRegions.Add(extent);
                focus += Utilities.RoundUp(extent.Length, IsoUtilities.SectorSize);
            }

            // ####################################################################
            // # 3. Fix path tables
            // ####################################################################

            // There are four path tables:
            //  1. LE, ASCII
            //  2. BE, ASCII
            //  3. LE, Supp Encoding (Joliet)
            //  4. BE, Supp Encoding (Joliet)

            // Find end of the path table
            long startOfFirstPathTable = focus;
            PathTable pathTable = new PathTable(false, Encoding.ASCII, _dirs, primaryLocationTable, focus);
            fixedRegions.Add(pathTable);
            focus += Utilities.RoundUp(pathTable.Length, IsoUtilities.SectorSize);
            long primaryPathTableLength = pathTable.Length;

            long startOfSecondPathTable = focus;
            pathTable = new PathTable(true, Encoding.ASCII, _dirs, primaryLocationTable, focus);
            fixedRegions.Add(pathTable);
            focus += Utilities.RoundUp(pathTable.Length, IsoUtilities.SectorSize);

            long startOfThirdPathTable = focus;
            pathTable = new PathTable(false, suppEncoding, _dirs, supplementaryLocationTable, focus);
            fixedRegions.Add(pathTable);
            focus += Utilities.RoundUp(pathTable.Length, IsoUtilities.SectorSize);
            long supplementaryPathTableLength = pathTable.Length;

            long startOfFourthPathTable = focus;
            pathTable = new PathTable(true, suppEncoding, _dirs, supplementaryLocationTable, focus);
            fixedRegions.Add(pathTable);
            focus += Utilities.RoundUp(pathTable.Length, IsoUtilities.SectorSize);

            // Find the end of the disk
            totalLength = focus;


            // ####################################################################
            // # 4. Prepare volume descriptors now other structures are fixed
            // ####################################################################

            PrimaryVolumeDescriptor pvDesc = new PrimaryVolumeDescriptor(
                (uint)(totalLength / IsoUtilities.SectorSize),             // VolumeSpaceSize
                (uint)(primaryPathTableLength),                            // PathTableSize
                (uint)(startOfFirstPathTable / IsoUtilities.SectorSize),   // TypeLPathTableLocation
                (uint)(startOfSecondPathTable / IsoUtilities.SectorSize),  // TypeMPathTableLocation
                (uint)(startOfFirstDirData / IsoUtilities.SectorSize),     // RootDirectory.LocationOfExtent
                (uint)_rootDirectory.GetDataSize(Encoding.ASCII),          // RootDirectory.DataLength
                buildTime
                );
            pvDesc.VolumeIdentifier = _buildParams.VolumeIdentifier;
            PrimaryVolumeDescriptorRegion pvdr = new PrimaryVolumeDescriptorRegion(pvDesc, DiskStart);
            fixedRegions.Insert(0, pvdr);

            SupplementaryVolumeDescriptor svDesc = new SupplementaryVolumeDescriptor(
                (uint)(totalLength / IsoUtilities.SectorSize),             // VolumeSpaceSize
                (uint)(supplementaryPathTableLength),                      // PathTableSize
                (uint)(startOfThirdPathTable / IsoUtilities.SectorSize),   // TypeLPathTableLocation
                (uint)(startOfFourthPathTable / IsoUtilities.SectorSize),  // TypeMPathTableLocation
                (uint)(startOfSecondDirData / IsoUtilities.SectorSize),    // RootDirectory.LocationOfExtent
                (uint)_rootDirectory.GetDataSize(suppEncoding),            // RootDirectory.DataLength
                buildTime,
                suppEncoding
                );
            svDesc.VolumeIdentifier = _buildParams.VolumeIdentifier;
            SupplementaryVolumeDescriptorRegion svdr = new SupplementaryVolumeDescriptorRegion(svDesc, DiskStart + IsoUtilities.SectorSize);
            fixedRegions.Insert(1, svdr);

            VolumeDescriptorSetTerminator evDesc = new VolumeDescriptorSetTerminator();
            VolumeDescriptorSetTerminatorRegion evdr = new VolumeDescriptorSetTerminatorRegion(evDesc, DiskStart + (IsoUtilities.SectorSize * 2));
            fixedRegions.Insert(2, evdr);

            return fixedRegions;
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
