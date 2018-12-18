//
// Copyright (c) 2008-2011, Kenneth Bell
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
using System.IO;

namespace DiscUtils.Nfs
{
    public class RpcCallHeader
    {
        public RpcCallHeader()
        {
        }

        public RpcCallHeader(XdrDataReader reader)
        {
            RpcVersion = reader.ReadUInt32();
            Program = reader.ReadUInt32();
            Version = reader.ReadUInt32();
            Proc = reader.ReadInt32();
            Credentials = new RpcAuthentication(reader);
            Verifier = new RpcAuthentication(reader);
        }

        public RpcAuthentication Credentials { get; set; }

        public int Proc { get; set; }

        public uint Program { get; set; }

        public uint RpcVersion { get; set; }

        public RpcAuthentication Verifier { get; set; }

        public uint Version { get; set; }

        public void Write(XdrDataWriter writer)
        {
            writer.Write(RpcVersion);
            writer.Write(Program);
            writer.Write(Version);
            writer.Write((uint)Proc);
            Credentials.Write(writer);
            Verifier.Write(writer);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RpcCallHeader);
        }

        public bool Equals(RpcCallHeader other)
        {
            if (other == null)
            {
                return false;
            }

            return other.Credentials.Equals(Credentials)
                && other.Proc == Proc
                && other.Program == Program
                && other.RpcVersion == RpcVersion
                && other.Verifier.Equals(Verifier)
                && other.Version == Version;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Credentials, Proc, Program, RpcVersion, Verifier, Version);
        }
    }
}
