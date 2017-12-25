//
// Copyright (c) 2017, Quamotion
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

#if !NET20
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscUtils.Nfs
{
    public class Nfs3MountFileServer : Nfs3MountServer
    {
        private readonly Nfs3FileServer _fileServer;

        public Nfs3MountFileServer(Nfs3FileServer fileServer)
        {
            _fileServer = fileServer;
        }

        protected override Nfs3ExportResult Exports()
        {
            return new Nfs3ExportResult()
            {
                Exports = _fileServer.Mounts.Select(
                     m => new Nfs3Export()
                     {
                         DirPath = m.Key,
                         Groups = new List<string>()
                     }).ToList(),
                Status = Nfs3Status.Ok
            };
        }

        protected override Nfs3MountResult Mount(string dirPath)
        {
            if (_fileServer.Mounts.ContainsKey(dirPath))
            {
                return new Nfs3MountResult()
                {
                    AuthFlavours = new List<RpcAuthFlavour>() { RpcAuthFlavour.Null },
                    FileHandle = _fileServer.GetHandle(_fileServer.Mounts[dirPath]),
                    Status = Nfs3Status.Ok
                };
            }
            else
            {
                return new Nfs3MountResult()
                {
                    Status = Nfs3Status.NoSuchEntity
                };
            }
        }
    }
}
#endif