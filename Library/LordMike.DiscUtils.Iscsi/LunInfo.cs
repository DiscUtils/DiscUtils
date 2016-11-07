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
using System.Globalization;

namespace DiscUtils.Iscsi
{
    /// <summary>
    /// Provides information about an iSCSI LUN.
    /// </summary>
    public class LunInfo
    {
        internal LunInfo(TargetInfo targetInfo, long lun, LunClass type, bool removable, string vendor, string product,
                         string revision)
        {
            Target = targetInfo;
            Lun = lun;
            DeviceType = type;
            Removable = removable;
            VendorId = vendor;
            ProductId = product;
            ProductRevision = revision;
        }

        /// <summary>
        /// Gets the type (or class) of this device.
        /// </summary>
        public LunClass DeviceType { get; }

        /// <summary>
        /// Gets the Logical Unit Number of this device.
        /// </summary>
        public long Lun { get; }

        /// <summary>
        /// Gets the product id (name) for this device.
        /// </summary>
        public string ProductId { get; }

        /// <summary>
        /// Gets the product revision for this device.
        /// </summary>
        public string ProductRevision { get; }

        /// <summary>
        /// Gets a value indicating whether this Lun has removable media.
        /// </summary>
        public bool Removable { get; }

        /// <summary>
        /// Gets info about the target hosting this LUN.
        /// </summary>
        public TargetInfo Target { get; }

        /// <summary>
        /// Gets the vendor id (registered name) for this device.
        /// </summary>
        public string VendorId { get; }

        /// <summary>
        /// Parses a URI referring to a LUN.
        /// </summary>
        /// <param name="uri">The URI to parse.</param>
        /// <returns>The LUN info.</returns>
        /// <remarks>
        /// Note the LUN info is incomplete, only as much of the information as is encoded
        /// into the URL is available.
        /// </remarks>
        public static LunInfo ParseUri(string uri)
        {
            return ParseUri(new Uri(uri));
        }

        /// <summary>
        /// Parses a URI referring to a LUN.
        /// </summary>
        /// <param name="uri">The URI to parse.</param>
        /// <returns>The LUN info.</returns>
        /// <remarks>
        /// Note the LUN info is incomplete, only as much of the information as is encoded
        /// into the URL is available.
        /// </remarks>
        public static LunInfo ParseUri(Uri uri)
        {
            string address;
            int port;
            string targetGroupTag = string.Empty;
            string targetName = string.Empty;
            ulong lun = 0;

            if (uri.Scheme != "iscsi")
            {
                ThrowInvalidURI(uri.OriginalString);
            }

            address = uri.Host;
            port = uri.Port;
            if (uri.Port == -1)
            {
                port = TargetAddress.DefaultPort;
            }

            string[] uriSegments = uri.Segments;
            if (uriSegments.Length == 2)
            {
                targetName = uriSegments[1].Replace("/", string.Empty);
            }
            else if (uriSegments.Length == 3)
            {
                targetGroupTag = uriSegments[1].Replace("/", string.Empty);
                targetName = uriSegments[2].Replace("/", string.Empty);
            }
            else
            {
                ThrowInvalidURI(uri.OriginalString);
            }

            TargetInfo targetInfo = new TargetInfo(targetName,
                new[] { new TargetAddress(address, port, targetGroupTag) });

            foreach (string queryElem in uri.Query.Substring(1).Split('&'))
            {
                if (queryElem.StartsWith("LUN=", StringComparison.OrdinalIgnoreCase))
                {
                    lun = ulong.Parse(queryElem.Substring(4), CultureInfo.InvariantCulture);
                    if (lun < 256)
                    {
                        lun = lun << (6 * 8);
                    }
                }
            }

            return new LunInfo(targetInfo, (long)lun, LunClass.Unknown, false, string.Empty, string.Empty, string.Empty);
        }

        /// <summary>
        /// Gets the LUN as a string.
        /// </summary>
        /// <returns>The LUN in string form.</returns>
        public override string ToString()
        {
            if (((ulong)Lun & 0xFF00000000000000) == 0)
            {
                return (Lun >> (6 * 8)).ToString(CultureInfo.InvariantCulture);
            }
            return Lun.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Gets the URIs corresponding to this LUN.
        /// </summary>
        /// <returns>An array of URIs as strings.</returns>
        /// <remarks>Multiple URIs are returned because multiple targets may serve the same LUN.</remarks>
        public string[] GetUris()
        {
            List<string> results = new List<string>();
            foreach (TargetAddress targetAddress in Target.Addresses)
            {
                results.Add(targetAddress.ToUri() + "/" + Target.Name + "?LUN=" + ToString());
            }

            return results.ToArray();
        }

        private static void ThrowInvalidURI(string uri)
        {
            throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Not a valid iSCSI URI: {0}", uri),
                nameof(uri));
        }
    }
}