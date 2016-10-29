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

namespace DiscUtils.Iscsi
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// Provides information about an iSCSI LUN.
    /// </summary>
    public class LunInfo
    {
        private TargetInfo _targetInfo;
        private long _lun;
        private LunClass _deviceType;
        private bool _removable;
        private string _vendorId;
        private string _productId;
        private string _productRevision;

        internal LunInfo(TargetInfo targetInfo, long lun, LunClass type, bool removable, string vendor, string product,
            string revision)
        {
            _targetInfo = targetInfo;
            _lun = lun;
            _deviceType = type;
            _removable = removable;
            _vendorId = vendor;
            _productId = product;
            _productRevision = revision;
        }

        /// <summary>
        /// Gets info about the target hosting this LUN.
        /// </summary>
        public TargetInfo Target
        {
            get { return _targetInfo; }
        }

        /// <summary>
        /// Gets the Logical Unit Number of this device.
        /// </summary>
        public long Lun
        {
            get { return _lun; }
        }

        /// <summary>
        /// Gets the type (or class) of this device.
        /// </summary>
        public LunClass DeviceType
        {
            get { return _deviceType; }
        }

        /// <summary>
        /// Gets a value indicating whether this Lun has removable media.
        /// </summary>
        public bool Removable
        {
            get { return _removable; }
        }

        /// <summary>
        /// Gets the vendor id (registered name) for this device.
        /// </summary>
        public string VendorId
        {
            get { return _vendorId; }
        }

        /// <summary>
        /// Gets the product id (name) for this device.
        /// </summary>
        public string ProductId
        {
            get { return _productId; }
        }

        /// <summary>
        /// Gets the product revision for this device.
        /// </summary>
        public string ProductRevision
        {
            get { return _productRevision; }
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
                new TargetAddress[] {new TargetAddress(address, port, targetGroupTag)});

            foreach (var queryElem in uri.Query.Substring(1).Split('&'))
            {
                if (queryElem.StartsWith("LUN=", StringComparison.OrdinalIgnoreCase))
                {
                    lun = ulong.Parse(queryElem.Substring(4), CultureInfo.InvariantCulture);
                    if (lun < 256)
                    {
                        lun = lun << (6*8);
                    }
                }
            }

            return new LunInfo(targetInfo, (long) lun, LunClass.Unknown, false, string.Empty, string.Empty, string.Empty);
        }

        /// <summary>
        /// Gets the LUN as a string.
        /// </summary>
        /// <returns>The LUN in string form.</returns>
        public override string ToString()
        {
            if ((((ulong) _lun) & 0xFF00000000000000) == 0)
            {
                return (_lun >> (6*8)).ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                return _lun.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Gets the URIs corresponding to this LUN.
        /// </summary>
        /// <returns>An array of URIs as strings.</returns>
        /// <remarks>Multiple URIs are returned because multiple targets may serve the same LUN.</remarks>
        public string[] GetUris()
        {
            List<string> results = new List<string>();
            foreach (var targetAddress in _targetInfo.Addresses)
            {
                results.Add(targetAddress.ToUri().ToString() + "/" + _targetInfo.Name + "?LUN=" + ToString());
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