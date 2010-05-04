//
// Copyright (c) 2008-2010, Kenneth Bell
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
using System.Security.AccessControl;
using System.Security.Principal;

namespace DiscUtils.Ntfs
{
    internal sealed class SecurityDescriptor : IByteArraySerializable, IDiagnosticTraceable
    {
        private RawSecurityDescriptor _securityDescriptor;

        public SecurityDescriptor()
        {
        }

        public SecurityDescriptor(RawSecurityDescriptor secDesc)
        {
            _securityDescriptor = secDesc;
        }

        public RawSecurityDescriptor Descriptor
        {
            get { return _securityDescriptor; }
            set { _securityDescriptor = value; }
        }

        public uint CalcHash()
        {
            byte[] buffer = new byte[Size];
            WriteTo(buffer, 0);
            uint hash = 0;
            for (int i = 0; i < buffer.Length / 4; ++i)
            {
                hash = Utilities.ToUInt32LittleEndian(buffer, i * 4) + ((hash << 3) | (hash >> 29));
            }
            return hash;
        }

        #region IByteArraySerializable Members

        public int ReadFrom(byte[] buffer, int offset)
        {
            _securityDescriptor = new RawSecurityDescriptor(buffer, offset);
            return _securityDescriptor.BinaryLength;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            // Write out the security descriptor manually because on NTFS the DACL is written
            // before the Owner & Group.  Writing the components in the same order means the
            // hashes will match for identical Security Descriptors.

            ControlFlags controlFlags = _securityDescriptor.ControlFlags;
            buffer[offset + 0x00] = 1;
            buffer[offset + 0x01] = _securityDescriptor.ResourceManagerControl;
            Utilities.WriteBytesLittleEndian((ushort)controlFlags, buffer, offset + 0x02);

            // Blank out offsets, will fill later
            for(int i = 0x04; i < 0x14; ++i)
            {
                buffer[offset + i] = 0;
            }

            int pos = 0x14;

            RawAcl discAcl = _securityDescriptor.DiscretionaryAcl;
            if ((controlFlags & ControlFlags.DiscretionaryAclPresent) != 0 && discAcl != null)
            {
                Utilities.WriteBytesLittleEndian(pos, buffer, offset + 0x10);
                discAcl.GetBinaryForm(buffer, offset + pos);
                pos += _securityDescriptor.DiscretionaryAcl.BinaryLength;
            }
            else
            {
                Utilities.WriteBytesLittleEndian((int)0, buffer, offset + 0x10);
            }

            RawAcl sysAcl = _securityDescriptor.SystemAcl;
            if ((controlFlags & ControlFlags.SystemAclPresent) != 0 && sysAcl != null)
            {
                Utilities.WriteBytesLittleEndian(pos, buffer, offset + 0x0C);
                sysAcl.GetBinaryForm(buffer, offset + pos);
                pos += _securityDescriptor.SystemAcl.BinaryLength;
            }
            else
            {
                Utilities.WriteBytesLittleEndian((int)0, buffer, offset + 0x0C);
            }

            Utilities.WriteBytesLittleEndian(pos, buffer, offset + 0x04);
            _securityDescriptor.Owner.GetBinaryForm(buffer, offset + pos);
            pos += _securityDescriptor.Owner.BinaryLength;

            Utilities.WriteBytesLittleEndian(pos, buffer, offset + 0x08);
            _securityDescriptor.Group.GetBinaryForm(buffer, offset + pos);
            pos += _securityDescriptor.Group.BinaryLength;

            if (pos != _securityDescriptor.BinaryLength)
            {
                throw new IOException("Failed to write Security Descriptor correctly");
            }
        }

        public int Size
        {
            get
            {
                return _securityDescriptor.BinaryLength;
            }
        }

        #endregion

        #region IDiagnosticTracer Members

        public void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "Descriptor: " + _securityDescriptor.GetSddlForm(AccessControlSections.All));
        }

        #endregion

        #region SID inheritance calculation
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
                            newFlags &= ~(AceFlags.ContainerInherit | AceFlags.ObjectInherit | AceFlags.NoPropagateInherit);
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

        #endregion
    }
}
