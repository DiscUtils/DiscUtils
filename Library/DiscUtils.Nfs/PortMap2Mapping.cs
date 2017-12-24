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
    /// <summary> 
    /// A mapping of(program, version, network ID) to address
    ///
    /// The network identifier(r_netid):
    /// This is a string that represents a local identification for a
    /// network.This is defined by a system administrator based on local
    /// conventions, and cannot be depended on to have the same value on
    /// every system.
    /// </summary>
    public class PortMap2Mapping
    {
        public PortMap2Mapping()
        {
        }

        internal PortMap2Mapping(XdrDataReader reader)
        {
            Program = reader.ReadInt32();
            Version = reader.ReadInt32();
            Protocol = (PortMap2Protocol)reader.ReadUInt32();
            Port = reader.ReadUInt32();
        }

        public int Program { get; set; }

        public int Version { get; set; }

        public PortMap2Protocol Protocol { get; set; }

        public uint Port { get; set; }

        internal void Write(XdrDataWriter writer)
        {
            writer.Write(Program);
            writer.Write(Version);
            writer.Write((uint)Protocol);
            writer.Write(Port);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PortMap2Mapping);
        }

        public bool Equals(PortMap2Mapping other)
        {
            if (other == null)
            {
                return false;
            }

            return other.Program == Program
                && other.Version == Version
                && other.Protocol == Protocol
                && other.Port == Port;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Program, Version, Protocol, Port);
        }
    }
}
