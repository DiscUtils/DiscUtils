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

namespace DiscUtils.Iscsi
{
    [VirtualDiskTransport("iscsi")]
    internal sealed class DiskTransport : VirtualDiskTransport
    {
        private LunInfo _lunInfo;
        private Session _session;

        public override bool IsRawDisk
        {
            get { return true; }
        }

        public override void Connect(Uri uri, string username, string password)
        {
            _lunInfo = LunInfo.ParseUri(uri.OriginalString);

            Initiator initiator = new Initiator();
            initiator.SetCredentials(username, password);
            _session = initiator.ConnectTo(_lunInfo.Target);
        }

        public override VirtualDisk OpenDisk(FileAccess access)
        {
            return _session.OpenDisk(_lunInfo.Lun, access);
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
            if (disposing)
            {
                if (_session != null)
                {
                    _session.Dispose();
                }

                _session = null;
            }

            base.Dispose(disposing);
        }
    }
}