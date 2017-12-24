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
    public class RpcAuthentication
    {
        private readonly byte[] _body;
        private readonly RpcAuthFlavour _flavour;

        public RpcAuthentication()
        {
            _body = new byte[400];
        }

        public RpcAuthentication(XdrDataReader reader)
        {
            _flavour = (RpcAuthFlavour)reader.ReadInt32();
            _body = reader.ReadBuffer(400);
        }

        public RpcAuthentication(RpcCredentials credential)
        {
            _flavour = credential.AuthFlavour;

            MemoryStream ms = new MemoryStream();
            XdrDataWriter writer = new XdrDataWriter(ms);
            credential.Write(writer);
            _body = ms.ToArray();
        }

        public static RpcAuthentication Null()
        {
            return new RpcAuthentication(new RpcNullCredentials());
        }

        public void Write(XdrDataWriter writer)
        {
            writer.Write((int)_flavour);
            writer.WriteBuffer(_body);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RpcAuthentication);
        }

        public bool Equals(RpcAuthentication other)
        {
            if (other == null)
            {
                return false;
            }
            
            return other._flavour == _flavour;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_flavour);
        }
    }
}