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
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using DiskRecord = DiscUtils.Tuple<string, DiscUtils.SparseStream, DiscUtils.Ownership>;

namespace DiscUtils.Xva
{
    /// <summary>
    /// A class that can be used to create Xen Virtual Appliance (XVA) files.
    /// </summary>
    /// <remarks>This class is not intended to be a general purpose XVA generator,
    /// the options to control the VM properties are strictly limited.  The class
    /// generates a minimal VM really as a wrapper for one or more disk images, 
    /// making them easy to import into XenServer.</remarks>
    public sealed class VirtualMachineBuilder : StreamBuilder, IDisposable
    {
        private List<DiskRecord> _disks;
        private string _vmDisplayName;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public VirtualMachineBuilder()
        {
            _disks = new List<DiskRecord>();
            _vmDisplayName = "VM";
        }

        /// <summary>
        /// Disposes this instance, including any underlying resources.
        /// </summary>
        public void Dispose()
        {
            foreach (DiskRecord r in _disks)
            {
                if (r.Third == Ownership.Dispose)
                {
                    r.Second.Dispose();
                }
            }
        }

        /// <summary>
        /// The display name of the VM.
        /// </summary>
        public string DisplayName
        {
            get { return _vmDisplayName; }
            set { _vmDisplayName = value; }
        }

        /// <summary>
        /// Adds a sparse disk image to the XVA file.
        /// </summary>
        /// <param name="label">The admin-visible name of the disk</param>
        /// <param name="content">The content of the disk</param>
        /// <param name="ownsContent">Indicates if ownership of content is transfered</param>
        public void AddDisk(string label, SparseStream content, Ownership ownsContent)
        {
            _disks.Add(new DiskRecord(label, content, ownsContent));
        }

        /// <summary>
        /// Adds a disk image to the XVA file.
        /// </summary>
        /// <param name="label">The admin-visible name of the disk</param>
        /// <param name="content">The content of the disk</param>
        /// <param name="ownsContent">Indicates if ownership of content is transfered</param>
        public void AddDisk(string label, Stream content, Ownership ownsContent)
        {
            _disks.Add(new DiskRecord(label, SparseStream.FromStream(content, ownsContent), Ownership.Dispose));
        }

        /// <summary>
        /// Creates a new stream that contains the XVA image.
        /// </summary>
        /// <returns>The new stream</returns>
        public override SparseStream Build()
        {
            TarFileBuilder tarBuilder = new TarFileBuilder();

            int[] diskIds;

            string ovaFileContent = GenerateOvaXml(out diskIds);
            tarBuilder.AddFile("ova.xml", Encoding.ASCII.GetBytes(ovaFileContent));

            int diskIdx = 0;
            foreach (var diskRec in _disks)
            {
                SparseStream diskStream = diskRec.Second;
                List<StreamExtent> extents = new List<StreamExtent>(diskStream.Extents);

                int lastChunkAdded = -1;
                foreach (StreamExtent extent in extents)
                {
                    int firstChunk = (int)(extent.Start / Sizes.OneMiB);
                    int lastChunk = (int)((extent.Start + extent.Length - 1) / Sizes.OneMiB);

                    for (int i = firstChunk; i <= lastChunk; ++i)
                    {
                        if (i != lastChunkAdded)
                        {
                            HashAlgorithm hashAlg = new SHA1Managed();
                            Stream chunkStream;

                            long diskBytesLeft = diskStream.Length - i * Sizes.OneMiB;
                            if(diskBytesLeft < Sizes.OneMiB)
                            {
                                chunkStream = new ConcatStream(
                                    Ownership.Dispose,
                                    new SubStream(diskStream, i * Sizes.OneMiB, diskBytesLeft),
                                    new ZeroStream(Sizes.OneMiB - diskBytesLeft));
                            }
                            else
                            {
                                chunkStream = new SubStream(diskStream, i * Sizes.OneMiB, Sizes.OneMiB);
                            }
                            HashStream chunkHashStream = new HashStream(chunkStream, Ownership.Dispose, hashAlg);

                            tarBuilder.AddFile(string.Format(CultureInfo.InvariantCulture, "Ref:{0}/{1:X8}", diskIds[diskIdx], i), chunkHashStream);
                            tarBuilder.AddFile(string.Format(CultureInfo.InvariantCulture, "Ref:{0}/{1:X8}.checksum", diskIds[diskIdx], i), new ChecksumStream(hashAlg));

                            lastChunkAdded = i;
                        }
                    }
                }

                // Make sure the last chunk is present, filled with zero's if necessary
                int lastActualChunk = (int)((diskStream.Length - 1) / Sizes.OneMiB);
                if (lastChunkAdded < lastActualChunk)
                {
                    HashAlgorithm hashAlg = new SHA1Managed();
                    Stream chunkStream = new ZeroStream(Sizes.OneMiB);
                    HashStream chunkHashStream = new HashStream(chunkStream, Ownership.Dispose, hashAlg);
                    tarBuilder.AddFile(string.Format(CultureInfo.InvariantCulture, "Ref:{0}/{1:X8}", diskIds[diskIdx], lastActualChunk), chunkHashStream);
                    tarBuilder.AddFile(string.Format(CultureInfo.InvariantCulture, "Ref:{0}/{1:X8}.checksum", diskIds[diskIdx], lastActualChunk), new ChecksumStream(hashAlg));
                }

                ++diskIdx;
            }

            return tarBuilder.Build();
        }

        internal override List<BuilderExtent> FixExtents(out long totalLength)
        {
            // Not required - deferred to TarFileBuilder
            throw new NotSupportedException();
        }

        private string GenerateOvaXml(out int[] diskIds)
        {
            int id = 0;

            Guid vmGuid = Guid.NewGuid();
            string vmName = _vmDisplayName;
            int vmId = id++;

            // Establish per-disk info
            Guid[] vbdGuids = new Guid[_disks.Count];
            int[] vbdIds = new int[_disks.Count];
            Guid[] vdiGuids = new Guid[_disks.Count];
            string[] vdiNames = new string[_disks.Count];
            int[] vdiIds = new int[_disks.Count];
            long[] vdiSizes = new long[_disks.Count];

            int diskIdx = 0;
            foreach(var disk in _disks)
            {
                vbdGuids[diskIdx] = Guid.NewGuid();
                vbdIds[diskIdx] = id++;
                vdiGuids[diskIdx] = Guid.NewGuid();
                vdiIds[diskIdx] = id++;
                vdiNames[diskIdx] = disk.First;
                vdiSizes[diskIdx] = Utilities.RoundUp(disk.Second.Length, Sizes.OneMiB);
                diskIdx++;
            }

            // Establish SR info
            Guid srGuid = Guid.NewGuid();
            string srName = "SR";
            int srId = id++;


            string vbdRefs = "";
            for (int i = 0; i < _disks.Count; ++i)
            {
                vbdRefs += string.Format(CultureInfo.InvariantCulture, Resources.XVA_ova_ref, "Ref:" + vbdIds[i]);
            }

            string vdiRefs = "";
            for (int i = 0; i < _disks.Count; ++i)
            {
                vdiRefs += string.Format(CultureInfo.InvariantCulture, Resources.XVA_ova_ref, "Ref:" + vdiIds[i]);
            }


            StringBuilder objectsString = new StringBuilder();

            objectsString.Append(string.Format(CultureInfo.InvariantCulture, Resources.XVA_ova_vm, "Ref:" + vmId, vmGuid, vmName, vbdRefs));

            for (int i = 0; i < _disks.Count; ++i)
            {
                objectsString.Append(string.Format(CultureInfo.InvariantCulture, Resources.XVA_ova_vbd, "Ref:" + vbdIds[i], vbdGuids[i], "Ref:" + vmId, "Ref:" + vdiIds[i], i));
            }

            for (int i = 0; i < _disks.Count; ++i)
            {
                objectsString.Append(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        Resources.XVA_ova_vdi,
                        "Ref:" + vdiIds[i],
                        vdiGuids[i],
                        vdiNames[i],
                        "Ref:" + srId,
                        "Ref:" + vbdIds[i],
                        vdiSizes[i]));
            }

            objectsString.Append(string.Format(CultureInfo.InvariantCulture, Resources.XVA_ova_sr, "Ref:" + srId, srGuid, srName, vdiRefs));

            diskIds = vdiIds;
            return string.Format(CultureInfo.InvariantCulture, Resources.XVA_ova_base, objectsString.ToString());
        }
    }
}
