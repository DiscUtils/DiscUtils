//
// Copyright (c) 2008-2012, Kenneth Bell
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

namespace DiscUtils.Vhdx
{
    using System;

    internal sealed class MetadataEntryKey : IEquatable<MetadataEntryKey>
    {
        private Guid _itemId;
        private bool _isUser;

        public MetadataEntryKey(Guid itemId, bool isUser)
        {
            _itemId = itemId;
            _isUser = isUser;
        }

        public Guid ItemId
        {
            get { return _itemId; }
        }

        public bool IsUser
        {
            get { return _isUser; }
        }

        public static bool operator ==(MetadataEntryKey x, MetadataEntryKey y)
        {
            if (Object.ReferenceEquals(x, y))
            {
                return true;
            }

            if (((object)x == null) || ((object)y == null))
            {
                return false;
            }

            return x._itemId == y._itemId && x._isUser == y._isUser;
        }

        public static bool operator !=(MetadataEntryKey x, MetadataEntryKey y)
        {
            return !(x == y);
        }

        public static MetadataEntryKey FromEntry(MetadataEntry entry)
        {
            return new MetadataEntryKey(entry.ItemId, (entry.Flags & MetadataEntryFlags.IsUser) != 0);
        }

        public bool Equals(MetadataEntryKey other)
        {
            if (other == null)
            {
                return false;
            }

            return _itemId == other._itemId && _isUser == other._isUser;
        }

        public override bool Equals(object other)
        {
            MetadataEntryKey otherKey = other as MetadataEntryKey;
            if (otherKey != null)
            {
                return Equals(otherKey);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return _itemId.GetHashCode() ^ (_isUser ? 0x3C13A5 : 0);
        }

        public override string ToString()
        {
            return _itemId.ToString() + (_isUser ? " - User" : " - System");
        }
    }
}
