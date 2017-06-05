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

using System.IO;
using System.Security.AccessControl;
using DiscUtils.Streams;

namespace DiscUtils.Ntfs
{
    internal sealed class SecurityDescriptor : IByteArraySerializable, IDiagnosticTraceable
    {
        public SecurityDescriptor() {}

        public SecurityDescriptor(RawSecurityDescriptor secDesc)
        {
            Descriptor = secDesc;
        }

        public RawSecurityDescriptor Descriptor { get; set; }

        public int Size
        {
            get { return Descriptor.BinaryLength; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            Descriptor = new RawSecurityDescriptor(buffer, offset);
            return Descriptor.BinaryLength;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            // Write out the security descriptor manually because on NTFS the DACL is written
            // before the Owner & Group.  Writing the components in the same order means the
            // hashes will match for identical Security Descriptors.
            ControlFlags controlFlags = Descriptor.ControlFlags;
            buffer[offset + 0x00] = 1;
            buffer[offset + 0x01] = Descriptor.ResourceManagerControl;
            EndianUtilities.WriteBytesLittleEndian((ushort)controlFlags, buffer, offset + 0x02);

            // Blank out offsets, will fill later
            for (int i = 0x04; i < 0x14; ++i)
            {
                buffer[offset + i] = 0;
            }

            int pos = 0x14;

            RawAcl discAcl = Descriptor.DiscretionaryAcl;
            if ((controlFlags & ControlFlags.DiscretionaryAclPresent) != 0 && discAcl != null)
            {
                EndianUtilities.WriteBytesLittleEndian(pos, buffer, offset + 0x10);
                discAcl.GetBinaryForm(buffer, offset + pos);
                pos += Descriptor.DiscretionaryAcl.BinaryLength;
            }
            else
            {
                EndianUtilities.WriteBytesLittleEndian(0, buffer, offset + 0x10);
            }

            RawAcl sysAcl = Descriptor.SystemAcl;
            if ((controlFlags & ControlFlags.SystemAclPresent) != 0 && sysAcl != null)
            {
                EndianUtilities.WriteBytesLittleEndian(pos, buffer, offset + 0x0C);
                sysAcl.GetBinaryForm(buffer, offset + pos);
                pos += Descriptor.SystemAcl.BinaryLength;
            }
            else
            {
                EndianUtilities.WriteBytesLittleEndian(0, buffer, offset + 0x0C);
            }

            EndianUtilities.WriteBytesLittleEndian(pos, buffer, offset + 0x04);
            Descriptor.Owner.GetBinaryForm(buffer, offset + pos);
            pos += Descriptor.Owner.BinaryLength;

            EndianUtilities.WriteBytesLittleEndian(pos, buffer, offset + 0x08);
            Descriptor.Group.GetBinaryForm(buffer, offset + pos);
            pos += Descriptor.Group.BinaryLength;

            if (pos != Descriptor.BinaryLength)
            {
                throw new IOException("Failed to write Security Descriptor correctly");
            }
        }

        public void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "Descriptor: " + Descriptor.GetSddlForm(AccessControlSections.All));
        }

        public uint CalcHash()
        {
            byte[] buffer = new byte[Size];
            WriteTo(buffer, 0);
            uint hash = 0;
            for (int i = 0; i < buffer.Length / 4; ++i)
            {
                hash = EndianUtilities.ToUInt32LittleEndian(buffer, i * 4) + ((hash << 3) | (hash >> 29));
            }

            return hash;
        }

        internal static RawSecurityDescriptor CalcNewObjectDescriptor(RawSecurityDescriptor parent, bool isContainer)
        {
            RawAcl sacl = InheritAcl(parent.SystemAcl, isContainer);
            RawAcl dacl = InheritAcl(parent.DiscretionaryAcl, isContainer);

            return new RawSecurityDescriptor(parent.ControlFlags, parent.Owner, parent.Group, sacl, dacl);
        }

        private static RawAcl InheritAcl(RawAcl parentAcl, bool isContainer)
        {
            AceFlags inheritTest = isContainer ? AceFlags.ContainerInherit : AceFlags.ObjectInherit;

            RawAcl newAcl = null;
            if (parentAcl != null)
            {
                newAcl = new RawAcl(parentAcl.Revision, parentAcl.Count);
                foreach (GenericAce ace in parentAcl)
                {
                    if ((ace.AceFlags & inheritTest) != 0)
                    {
                        GenericAce newAce = ace.Copy();

                        AceFlags newFlags = ace.AceFlags;
                        if ((newFlags & AceFlags.NoPropagateInherit) != 0)
                        {
                            newFlags &=
                                ~(AceFlags.ContainerInherit | AceFlags.ObjectInherit | AceFlags.NoPropagateInherit);
                        }

                        newFlags &= ~AceFlags.InheritOnly;
                        newFlags |= AceFlags.Inherited;

                        newAce.AceFlags = newFlags;
                        newAcl.InsertAce(newAcl.Count, newAce);
                    }
                }
            }

            return newAcl;
        }
    }
}