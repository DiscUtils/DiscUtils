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

namespace DiscUtils.Nfs
{
    public class Nfs3PathConfResult : Nfs3CallResult
    {
        public Nfs3PathConfResult()
        {
        }

        public Nfs3PathConfResult(XdrDataReader reader)
        {
            Status = (Nfs3Status)reader.ReadInt32();
            ObjectAttributes = new Nfs3FileAttributes(reader);

            if (Status == Nfs3Status.Ok)
            {
                LinkMax = reader.ReadUInt32();
                NameMax = reader.ReadUInt32();
                NoTrunc = reader.ReadBool();
                ChownRestricted = reader.ReadBool();
                CaseInsensitive = reader.ReadBool();
                CasePreserving = reader.ReadBool();
            }
        }

        /// <summary>
        /// Gets or sets the attributes of the object specified by object.
        /// </summary>
        public Nfs3FileAttributes ObjectAttributes { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of hard links to an object.
        /// </summary>
        public uint LinkMax { get; set; }

        /// <summary>
        /// Gets or sets the maximum length of a component of a filename.
        /// </summary>
        public uint NameMax { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the server will reject any request that
        /// includes a name longer than name_max with the error,
        /// NFS3ERR_NAMETOOLONG.If FALSE, any length name over
        /// name_max bytes will be silently truncated to name_max
        /// bytes.
        /// </summary>
        public bool NoTrunc { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether server will reject any request to change
        /// either the owner or the group associated with a file if
        /// the caller is not the privileged user. (Uid 0.)
        /// </summary>
        public bool ChownRestricted { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the server file system does not distinguish
        /// case when interpreting filenames.
        /// </summary>
        public bool CaseInsensitive { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the server file system will preserve the case
        /// of a name during a CREATE, MKDIR, MKNOD, SYMLINK,
        /// RENAME, or LINK operation.
        /// </summary>
        public bool CasePreserving { get; set; }

        public override void Write(XdrDataWriter writer)
        {
            writer.Write((int)Status);
            ObjectAttributes.Write(writer);

            if (Status == Nfs3Status.Ok)
            {
                writer.Write(LinkMax);
                writer.Write(NameMax);
                writer.Write(NoTrunc);
                writer.Write(ChownRestricted);
                writer.Write(CaseInsensitive);
                writer.Write(CasePreserving);
            }
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Nfs3PathConfResult);
        }

        public bool Equals(Nfs3PathConfResult other)
        {
            if (other == null)
            {
                return false;
            }

            return other.Status == Status
                && other.LinkMax == LinkMax
                && other.NameMax == NameMax
                && other.NoTrunc == NoTrunc
                && other.ChownRestricted == ChownRestricted
                && other.CaseInsensitive == CaseInsensitive
                && other.CasePreserving == CasePreserving;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Status, LinkMax, NameMax, NoTrunc, ChownRestricted, CaseInsensitive, CasePreserving);
        }
    }
}
