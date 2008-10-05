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
    public class CDBuilder
    {
        private List<BuildFileInfo> files;
        private List<BuildDirectoryInfo> dirs;
        private BuildDirectoryInfo rootDirectory;

        BuildParameters buildParams;

        public CDBuilder()
        {
            files = new List<BuildFileInfo>();
            dirs = new List<BuildDirectoryInfo>();
            rootDirectory = new BuildDirectoryInfo("\0", null);
            dirs.Add(rootDirectory);

            buildParams = new BuildParameters();
            buildParams.UseJoliet = true;
        }

        public Stream Build()
        {
            return new CDBuildStream(files, dirs, rootDirectory, buildParams);
        }

        public void Build(string file)
        {
            using (CDBuildStream cdStream = new CDBuildStream(files, dirs, rootDirectory, buildParams))
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

        public string VolumeIdentifier
        {
            get { return buildParams.VolumeIdentifier; }
            set {
                if (value.Length > 32 || !Utilities.isValidDString(value))
                {
                    throw new ArgumentException("Not a valid volume identifier");
                }
                else
                {
                    buildParams.VolumeIdentifier = value;
                }
            }
        }

        public bool UseJoliet
        {
            get { return buildParams.UseJoliet; }
            set { buildParams.UseJoliet = value; }
        }

        public BuildFileInfo AddFile(string name, byte[] content)
        {
            string[] nameElements = name.Split('\\');
            BuildDirectoryInfo dir = GetDirectory(nameElements, nameElements.Length - 1, true);

            BuildDirectoryMember existing;
            if (dir.TryGetMember(nameElements[nameElements.Length - 1], out existing))
            {
                throw new Exception("File already exists");
            }
            else
            {
                BuildFileInfo fi = new BuildFileInfo(nameElements[nameElements.Length - 1], dir, content);
                files.Add(fi);
                dir.Add(fi);
                return fi;
            }
        }

        public BuildFileInfo AddFile(string name, string sourcePath)
        {
            string[] nameElements = name.Split('\\');
            BuildDirectoryInfo dir = GetDirectory(nameElements, nameElements.Length - 1, true);

            BuildDirectoryMember existing;
            if (dir.TryGetMember(nameElements[nameElements.Length - 1], out existing))
            {
                throw new Exception("File already exists");
            }
            else
            {
                BuildFileInfo fi = new BuildFileInfo(name, dir, sourcePath);
                files.Add(fi);
                dir.Add(fi);
                return fi;
            }
        }

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
                throw new Exception("File already exists");
            }
            else
            {
                BuildFileInfo fi = new BuildFileInfo(name, dir, source);
                files.Add(fi);
                dir.Add(fi);
                return fi;
            }
        }

        private BuildFileInfo TryGetFile(string[] path)
        {
            BuildDirectoryInfo di = TryGetDirectory(path, path.Length - 1, false);
            if (di == null)
            {
                return null;
            }

            BuildDirectoryMember dirMember;
            if (di.TryGetMember(path[path.Length - 1], out dirMember))
            {
                if (!(dirMember is BuildFileInfo))
                {
                    throw new Exception("Member is not a file");
                }
                return (BuildFileInfo)dirMember;
            }

            return null;
        }

        private BuildDirectoryInfo GetDirectory(string[] path, int pathLength, bool createMissing)
        {
            BuildDirectoryInfo di = TryGetDirectory(path, pathLength, createMissing);

            if (di == null)
            {
                throw new Exception("Directory not found");
            }

            return di;
        }

        private BuildDirectoryInfo TryGetDirectory(string[] path, int pathLength, bool createMissing)
        {
            BuildDirectoryInfo focus = rootDirectory;

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
                        dirs.Add(di);
                        focus = di;
                    }
                    else
                    {
                        return null;
                    }
                }
                else if (!(next is BuildDirectoryInfo))
                {
                    throw new Exception("File with conflicting name exists");
                }
                else
                {
                    focus = (BuildDirectoryInfo)next;
                }
            }

            return focus;
        }


    }


}
