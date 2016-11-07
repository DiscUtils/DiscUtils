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

using System;

namespace DiscUtils.Vhdx
{
    internal sealed class MetadataEntryKey : IEquatable<MetadataEntryKey>
    {
        private Guid _itemId;

        public MetadataEntryKey(Guid itemId, bool isUser)
        {
            _itemId = itemId;
            IsUser = isUser;
        }

        public bool IsUser { get; }

        public Guid ItemId
        {
            get { return _itemId; }
        }

        public bool Equals(MetadataEntryKey other)
        {
            if (other == null)
            {
                return false;
            }

            return _itemId == other._itemId && IsUser == other.IsUser;
        }

        public static bool operator ==(MetadataEntryKey x, MetadataEntryKey y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (((object)x == null) || ((object)y == null))
            {
                return false;
            }

            return x._itemId == y._itemId && x.IsUser == y.IsUser;
        }

        public static bool operator !=(MetadataEntryKey x, MetadataEntryKey y)
        {
            return !(x == y);
        }

        public static MetadataEntryKey FromEntry(MetadataEntry entry)
        {
            return new MetadataEntryKey(entry.ItemId, (entry.Flags & MetadataEntryFlags.IsUser) != 0);
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
            return _itemId.GetHashCode() ^ (IsUser ? 0x3C13A5 : 0);
        }

        public override string ToString()
        {
            return _itemId + (IsUser ? " - User" : " - System");
        }
    }
}