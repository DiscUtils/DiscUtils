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
#if !NET20
using System.Linq;
#endif

namespace DiscUtils.Nfs
{
    public sealed class Nfs3MountResult : Nfs3CallResult
    {
        internal Nfs3MountResult(XdrDataReader reader)
        {
            Status = (Nfs3Status)reader.ReadInt32();

            if (Status == Nfs3Status.Ok)
            {
                FileHandle = new Nfs3FileHandle(reader);
                int numAuthFlavours = reader.ReadInt32();
                AuthFlavours = new List<RpcAuthFlavour>(numAuthFlavours);
                for (int i = 0; i < numAuthFlavours; ++i)
                {
                    AuthFlavours.Add((RpcAuthFlavour)reader.ReadInt32());
                }
            }
            else
            {
                throw new Nfs3Exception(Status);
            }
        }

        public Nfs3MountResult()
        {
        }

        public List<RpcAuthFlavour> AuthFlavours { get; set; }

        public Nfs3FileHandle FileHandle { get; set; }

        public override void Write(XdrDataWriter writer)
        {
            writer.Write((int)Status);

            if (Status == Nfs3Status.Ok)
            {
                FileHandle.Write(writer);

                writer.Write(AuthFlavours.Count);
                for (int i = 0; i < AuthFlavours.Count; i++)
                {
                    writer.Write((int)AuthFlavours[i]);
                }
            }
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Nfs3MountResult);
        }

        public bool Equals(Nfs3MountResult other)
        {
            if (other == null)
            {
                return false;
            }

            return other.Status == Status
#if !NET20
                && Enumerable.SequenceEqual(other.AuthFlavours, AuthFlavours)
#endif
                && object.Equals(other.FileHandle, FileHandle);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Status, FileHandle, AuthFlavours);
        }
    }
}