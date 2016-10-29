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

using System.Threading.Tasks;

namespace MSBuildTask
{
    using System;
    using System.IO;
    using DiscUtils.Iso9660;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;

    public class CreateIso : Task
    {
        public CreateIso()
        {
            UseJoliet = true;
        }

        /// <summary>
        /// The name of the ISO file to create.
        /// </summary>
        [Required]
        public ITaskItem FileName { get; set; }

        /// <summary>
        /// Whether to use Joliet encoding for the ISO (default true).
        /// </summary>
        public bool UseJoliet { get; set; }

        /// <summary>
        /// The label for the ISO (may be truncated if too long)
        /// </summary>
        public string VolumeLabel { get; set; }

        /// <summary>
        /// The files to add to the ISO.
        /// </summary>
        [Required]
        public ITaskItem[] SourceFiles { get; set; }

        /// <summary>
        /// The boot image to add to the ISO.
        /// </summary>
        public ITaskItem BootImage { get; set; }

        /// <summary>
        /// Whether to patch to boot image (per ISOLINUX requireents).
        /// </summary>
        /// <remarks>
        /// Unless patched, ISOLINUX will indicate a checksum error upon boot.
        /// </remarks>
        public bool UpdateIsolinuxBootTable { get; set; }

        /// <summary>
        /// Sets the root to remove from the source files.
        /// </summary>
        /// <remarks>
        /// If the source file is C:\MyDir\MySubDir\file.txt, and RemoveRoot is C:\MyDir, the ISO will
        /// contain \MySubDir\file.txt.  If not specified, the file would be named \MyDir\MySubDir\file.txt.
        /// </remarks>
        public ITaskItem[] RemoveRoots { get; set; }

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.Normal, "Creating ISO file: '{0}'", FileName.ItemSpec);

            try
            {
                CDBuilder builder = new CDBuilder();
                builder.UseJoliet = UseJoliet;

                if (!string.IsNullOrEmpty(VolumeLabel))
                {
                    builder.VolumeIdentifier = VolumeLabel;
                }

                Stream bootImageStream = null;
                if (BootImage != null)
                {
                    bootImageStream = new FileStream(BootImage.GetMetadata("FullPath"), FileMode.Open, FileAccess.Read);
                    builder.SetBootImage(bootImageStream, BootDeviceEmulation.NoEmulation, 0);
                    builder.UpdateIsolinuxBootTable = UpdateIsolinuxBootTable;
                }

                foreach (var sourceFile in SourceFiles)
                {
                    builder.AddFile(GetDestinationPath(sourceFile), sourceFile.GetMetadata("FullPath"));
                }

                try
                {
                    builder.Build(FileName.ItemSpec);
                }
                finally
                {
                    if (bootImageStream != null)
                    {
                        bootImageStream.Dispose();
                    }
                }
            }
            catch(Exception e)
            {
                Log.LogErrorFromException(e, true, true, null);
                return false;
            }

            return !Log.HasLoggedErrors;
        }

        private string GetDestinationPath(ITaskItem sourceFile)
        {
            string fullPath = sourceFile.GetMetadata("FullPath");
            if (RemoveRoots != null)
            {
                foreach (var root in RemoveRoots)
                {
                    string rootPath = root.GetMetadata("FullPath");
                    if (fullPath.StartsWith(rootPath))
                    {
                        return fullPath.Substring(rootPath.Length);
                    }
                }
            }

            // Not under a known root - so full path (minus drive)...
            return sourceFile.GetMetadata("Directory") + sourceFile.GetMetadata("FileName") + sourceFile.GetMetadata("Extension");
        }
    }
}
