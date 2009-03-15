//
// Copyright (c) 2008-2009, Kenneth Bell
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
using System.Linq;
using System.Text;

namespace DiscUtils.Ntfs
{
    internal sealed class AttributeReference : IEquatable<AttributeReference>
    {
        private FileReference  _fileRef;
        private string _name;
        private AttributeType _type;

        public AttributeReference(FileReference fileRef, string name, AttributeType type)
        {
            _fileRef = fileRef;
            _name = name;
            _type = type;
        }

        public FileReference FileReference
        {
            get { return _fileRef; }
        }

        public string Name
        {
            get { return _name; }
        }

        public AttributeType Type
        {
            get { return _type; }
        }

        public static bool operator ==(AttributeReference a, AttributeReference b)
        {
            if (a == null)
            {
                return b == null;
            }
            else
            {
                return a.Equals(b);
            }
        }

        public static bool operator !=(AttributeReference a, AttributeReference b)
        {
            if (a == null)
            {
                return b != null;
            }
            else
            {
                return !a.Equals(b);
            }
        }

        public bool Equals(AttributeReference other)
        {
            if (other == null)
            {
                return false;
            }

            return _fileRef == other._fileRef && _name == other._name && _type == other._type;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as AttributeReference);
        }

        public override int GetHashCode()
        {
            return _fileRef.GetHashCode() ^ (_name == null ? 0x4c3d1e4a : _name.GetHashCode()) ^ _type.GetHashCode();
        }

    }
}
