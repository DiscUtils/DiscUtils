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

using System.Globalization;
using System.IO;
using System.Management.Automation;

namespace DiscUtils.PowerShell
{
    internal class Utilities
    {
        /// <summary>
        /// Replace all ':' characters with '#'.
        /// </summary>
        /// <param name="path">The path to normalize</param>
        /// <returns>The normalized path</returns>
        /// <remarks>
        /// PowerShell has a bug that prevents tab-completion if the paths contain ':'
        /// characters, so in the external path for this provider we encode ':' as '#'.
        /// </remarks>
        public static string NormalizePath(string path)
        {
            return path.Replace(':', '#');
        }

        /// <summary>
        /// Replace all '#' characters with ':'.
        /// </summary>
        /// <param name="path">The path to normalize</param>
        /// <returns>The normalized path</returns>
        /// <remarks>
        /// PowerShell has a bug that prevents tab-completion if the paths contain ':'
        /// characters, so in the external path for this provider we encode ':' as '#'.
        /// </remarks>
        public static string DenormalizePath(string path)
        {
            return path.Replace('#', ':');
        }

        public static Stream CreatePsPath(SessionState session, string filePath)
        {
            string parentPath = session.Path.ParseParent(filePath, null);
            string childName = session.Path.ParseChildName(filePath);
            var parentItems = session.InvokeProvider.Item.Get(parentPath);
            if (parentItems.Count > 1)
            {
                throw new IOException(string.Format(CultureInfo.InvariantCulture, "PowerShell path {0} is ambiguous", parentPath));
            }
            else if (parentItems.Count < 1)
            {
                throw new DirectoryNotFoundException("No such directory");
            }

            DirectoryInfo parentAsDir = parentItems[0].BaseObject as DirectoryInfo;
            if (parentAsDir != null)
            {
                return File.Create(Path.Combine(parentAsDir.FullName, childName));
            }

            DiscDirectoryInfo parentAsDiscDir = parentItems[0].BaseObject as DiscDirectoryInfo;
            if (parentAsDiscDir != null)
            {
                return parentAsDiscDir.FileSystem.OpenFile(Path.Combine(parentAsDiscDir.FullName, childName), FileMode.Create, FileAccess.ReadWrite);
            }

            throw new FileNotFoundException("Path is not a directory", parentPath);
        }

        public static Stream OpenPsPath(SessionState session, string filePath, FileAccess access, FileShare share)
        {
            var items = session.InvokeProvider.Item.Get(filePath);
            if (items.Count == 1)
            {
                FileInfo itemAsFile = items[0].BaseObject as FileInfo;
                if (itemAsFile != null)
                {
                    return itemAsFile.Open(FileMode.Open, access, share);
                }

                DiscFileInfo itemAsDiscFile = items[0].BaseObject as DiscFileInfo;
                if (itemAsDiscFile != null)
                {
                    return itemAsDiscFile.Open(FileMode.Open, access);
                }

                throw new FileNotFoundException("Path is not a file", filePath);
            }
            else if (items.Count > 1)
            {
                throw new IOException(string.Format(CultureInfo.InvariantCulture, "PowerShell path {0} is ambiguous", filePath));
            }
            else
            {
                throw new FileNotFoundException("No such file", filePath);
            }
        }

        public static string ResolvePsPath(SessionState session, string filePath)
        {
            var paths = session.Path.GetResolvedPSPathFromPSPath(filePath);
            if (paths.Count > 1)
            {
                throw new IOException(string.Format(CultureInfo.InvariantCulture, "PowerShell path {0} is ambiguous", filePath));
            }
            else if (paths.Count < 1)
            {
                throw new IOException(string.Format(CultureInfo.InvariantCulture, "PowerShell path {0} not found", filePath));
            }

            return paths[0].Path;
        }

    }
}
