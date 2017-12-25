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

using System;
using System.Collections.Generic;

namespace DiscUtils.Nfs
{
    public class Nfs3MountServer : IRpcProgram
    {
        public int ProgramIdentifier => Nfs3Mount.ProgramIdentifier;

        public int ProgramVersion => Nfs3Mount.ProgramVersion;

        public IEnumerable<int> Procedures
        { get; } = new int[] 
        {
            (int)MountProc3.Null,
            (int)MountProc3.Mnt,
            (int)MountProc3.Export
        };

        public IRpcObject Invoke(RpcCallHeader header, XdrDataReader reader)
        {
            switch ((MountProc3)header.Proc)
            {
                case MountProc3.Null:
                    return null;

                case MountProc3.Mnt:
                    string dirPath = reader.ReadString();

                    return Mount(dirPath);

                case MountProc3.Export:

                    return Exports();

                default:
                    throw new NotImplementedException();
            }
        }

        protected virtual Nfs3MountResult Mount(string dirPath)
        {
            throw new NotImplementedException();
        }

        protected virtual Nfs3ExportResult Exports()
        {
            throw new NotImplementedException();
        }
    }
}
