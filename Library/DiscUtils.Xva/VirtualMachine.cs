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
using System.Xml.XPath;
using DiscUtils.Archives;
using DiscUtils.Streams;

namespace DiscUtils.Xva
{
    /// <summary>
    /// Class representing the virtual machine stored in a Xen Virtual Appliance (XVA)
    /// file.
    /// </summary>
    /// <remarks>XVA is a VM archive, not just a disk archive.  It can contain multiple
    /// disk images.  This class provides access to all of the disk images within the
    /// XVA file.</remarks>
    public sealed class VirtualMachine : IDisposable
    {
        private static readonly XPathExpression FindVDIsExpression = XPathExpression.Compile("/value/struct/member[child::name='objects']/value/array/data/value/struct[child::member/value='VDI']");
        private static readonly XPathExpression GetDiskId = XPathExpression.Compile("member[child::name='id']/value");
        private static readonly XPathExpression GetDiskUuid = XPathExpression.Compile("member[child::name='snapshot']/value/struct/member[child::name='uuid']/value");
        private static readonly XPathExpression GetDiskNameLabel = XPathExpression.Compile("member[child::name='snapshot']/value/struct/member[child::name='name_label']/value");
        private static readonly XPathExpression GetDiskCapacity = XPathExpression.Compile("member[child::name='snapshot']/value/struct/member[child::name='virtual_size']/value");
        private readonly Ownership _ownership;

        private Stream _fileStream;

        /// <summary>
        /// Initializes a new instance of the VirtualMachine class.
        /// </summary>
        /// <param name="fileStream">The stream containing the .XVA file.</param>
        /// <remarks>
        /// Ownership of the stream is not transfered.
        /// </remarks>
        public VirtualMachine(Stream fileStream)
            : this(fileStream, Ownership.None) {}

        /// <summary>
        /// Initializes a new instance of the VirtualMachine class.
        /// </summary>
        /// <param name="fileStream">The stream containing the .XVA file.</param>
        /// <param name="ownership">Whether to transfer ownership of <c>fileStream</c> to the new instance.</param>
        public VirtualMachine(Stream fileStream, Ownership ownership)
        {
            _fileStream = fileStream;
            _ownership = ownership;
            _fileStream.Position = 0;
            Archive = new TarFile(fileStream);
        }

        internal TarFile Archive { get; }

        /// <summary>
        /// Gets the disks in this XVA.
        /// </summary>
        /// <returns>An enumeration of disks.</returns>
        public IEnumerable<Disk> Disks
        {
            get
            {
                using (Stream docStream = Archive.OpenFile("ova.xml"))
                {
                    XPathDocument ovaDoc = new XPathDocument(docStream);
                    XPathNavigator nav = ovaDoc.CreateNavigator();
                    foreach (XPathNavigator node in nav.Select(FindVDIsExpression))
                    {
                        XPathNavigator idNode = node.SelectSingleNode(GetDiskId);

                        // Skip disks which are only referenced, not present
                        if (Archive.DirExists(idNode.ToString()))
                        {
                            XPathNavigator uuidNode = node.SelectSingleNode(GetDiskUuid);
                            XPathNavigator nameLabelNode = node.SelectSingleNode(GetDiskNameLabel);
                            long capacity = long.Parse(node.SelectSingleNode(GetDiskCapacity).ToString());
                            yield return new Disk(this, uuidNode.ToString(), nameLabelNode.ToString(), idNode.ToString(), capacity);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Disposes of this object, freeing any owned resources.
        /// </summary>
        public void Dispose()
        {
            if (_ownership == Ownership.Dispose && _fileStream != null)
            {
                _fileStream.Dispose();
                _fileStream = null;
            }
        }
    }
}