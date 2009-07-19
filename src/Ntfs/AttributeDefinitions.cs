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

using System.Collections.Generic;
using System.IO;

namespace DiscUtils.Ntfs
{
    internal sealed class AttributeDefinitions
    {
        private Dictionary<AttributeType, AttributeDefinitionRecord> _attrDefs;

        public AttributeDefinitions(File file)
        {
            _attrDefs = new Dictionary<AttributeType, AttributeDefinitionRecord>();

            byte[] buffer = new byte[AttributeDefinitionRecord.Size];
            using (Stream s = file.OpenStream(AttributeType.Data, null, FileAccess.Read))
            {
                while(Utilities.ReadFully(s, buffer, 0, buffer.Length) == buffer.Length)
                {
                    AttributeDefinitionRecord record = new AttributeDefinitionRecord();
                    record.Read(buffer, 0);

                    // NULL terminator record
                    if (record.Type != AttributeType.None)
                    {
                        _attrDefs.Add(record.Type, record);
                    }
                }
            }
        }

        internal bool CanBeNonResident(AttributeType attributeType)
        {
            AttributeDefinitionRecord record;
            if (_attrDefs.TryGetValue(attributeType, out record))
            {
                return (record.Flags & AttributeTypeFlags.CanBeNonResident) != 0;
            }
            return false;
        }

        internal bool IsIndexed(AttributeType attributeType)
        {
            AttributeDefinitionRecord record;
            if (_attrDefs.TryGetValue(attributeType, out record))
            {
                return (record.Flags & AttributeTypeFlags.Indexed) != 0;
            }
            return false;
        }
    }
}
