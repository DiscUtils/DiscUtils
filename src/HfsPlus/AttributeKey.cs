using System;
using System.Collections.Generic;
using System.Text;

namespace DiscUtils.HfsPlus
{
    internal class AttributeKey : BTreeKey
    {
        private ushort _keyLength;
        private ushort _pad;
        private CatalogNodeId _fileId;
        private uint _startBlock;
        private string _name;

        public AttributeKey()
        {
        }

        public AttributeKey(CatalogNodeId nodeId, string name)
        {
            _fileId = nodeId;
            _name = name;
        }

        public CatalogNodeId FileId
        {
            get { return _fileId; }
        }

        public string Name
        {
            get { return _name; }
        }

        public override int Size
        {
            get { throw new NotImplementedException(); }
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            _keyLength = Utilities.ToUInt16BigEndian(buffer, offset + 0);
            _pad = Utilities.ToUInt16BigEndian(buffer, offset + 2);
            _fileId = new CatalogNodeId(Utilities.ToUInt32BigEndian(buffer, offset + 4));
            _startBlock = Utilities.ToUInt32BigEndian(buffer, offset + 8);
            _name = HfsPlusUtilities.ReadUniStr255(buffer, offset + 12);

            return _keyLength + 2;
        }

        public override void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public override int CompareTo(BTreeKey other)
        {
            return CompareTo(other as AttributeKey);
        }

        public int CompareTo(AttributeKey other)
        {
            if (other == null)
            {
                throw new ArgumentNullException("other");
            }

            if (_fileId != other._fileId)
            {
                return _fileId < other._fileId ? -1 : 1;
            }

            return HfsPlusUtilities.FastUnicodeCompare(_name, other._name);
        }

        public override string ToString()
        {
            return _name + " (" + _fileId + ")";
        }

    }
}
