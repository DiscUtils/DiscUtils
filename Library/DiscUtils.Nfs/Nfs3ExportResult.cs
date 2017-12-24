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
    public sealed class Nfs3ExportResult : Nfs3CallResult
    {
        internal Nfs3ExportResult(XdrDataReader reader)
        {
            Exports = new List<Nfs3Export>();
            while (reader.ReadBool())
            {
                Exports.Add(new Nfs3Export(reader));
            }
        }

        public Nfs3ExportResult()
        {
        }

        public List<Nfs3Export> Exports { get; set; }

        public override void Write(XdrDataWriter writer)
        {
            foreach (var export in Exports)
            {
                writer.Write(true);
                export.Write(writer);
            }

            writer.Write(false);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Nfs3ExportResult);
        }

        public bool Equals(Nfs3ExportResult other)
        {
            if (other == null)
            {
                return false;
            }

            if (other.Exports == null || Exports == null)
            {
                return false;
            }

            if (other.Exports.Count != Exports.Count)
            {
                return false;
            }

            for (int i = 0; i < Exports.Count; i++)
            {
                if (!object.Equals(other.Exports[i], Exports[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Exports);
        }
    }
}
