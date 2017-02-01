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
using System.IO;
using DiscUtils.Internal;

namespace DiscUtils.OpticalDiscSharing
{
    [VirtualDiskTransport("ods")]
    internal sealed class DiscTransport : VirtualDiskTransport
    {
        private string _disk;
        private OpticalDiscServiceClient _odsClient;
        private OpticalDiscService _service;

        public override bool IsRawDisk
        {
            get { return true; }
        }

        public override void Connect(Uri uri, string username, string password)
        {
            string domain = uri.Host;
            string[] pathParts = Uri.UnescapeDataString(uri.AbsolutePath).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string instance = pathParts[0];
            string volName = pathParts[1];

            _odsClient = new OpticalDiscServiceClient();
            foreach (OpticalDiscService service in _odsClient.LookupServices(domain))
            {
                if (service.DisplayName == instance)
                {
                    _service = service;
                    _service.Connect(Environment.UserName, Environment.MachineName, 30);

                    foreach (DiscInfo disk in _service.AdvertisedDiscs)
                    {
                        if (disk.VolumeLabel == volName)
                        {
                            _disk = disk.Name;
                        }
                    }
                }
            }

            if (_disk == null)
            {
                throw new FileNotFoundException("No such disk", uri.ToString());
            }
        }

        public override VirtualDisk OpenDisk(FileAccess access)
        {
            return _service.OpenDisc(_disk);
        }

        public override FileLocator GetFileLocator()
        {
            throw new NotImplementedException();
        }

        public override string GetFileName()
        {
            throw new NotImplementedException();
        }

        public override string GetExtraInfo()
        {
            throw new NotImplementedException();
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (_odsClient != null)
                {
                    _odsClient.Dispose();
                    _odsClient = null;
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}