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
using System.IO;
using System.Linq;

namespace DiscUtils.Ntfs
{
    internal class NtfsStream
    {
        private File _file;
        private AttributeType _attrType;
        private string _name;
        private IList<AttributeReference> _attrs;

        public NtfsStream(File file, AttributeType attrType, string name, IList<AttributeReference> attrs)
        {
            _file = file;
            _attrType = attrType;
            _name = name;
            _attrs = attrs;
        }

        public NtfsStream(File file, AttributeType attrType, string name, AttributeReference attr)
        {
            _file = file;
            _attrType = attrType;
            _name = name;
            _attrs = new AttributeReference[] { attr };
        }

        public AttributeType AttributeType
        {
            get { return _attrType; }
        }

        public string Name
        {
            get { return _name; }
        }

        public AttributeReference[] GetAttributes()
        {
            return _attrs.ToArray();
        }

        /// <summary>
        /// Gets the content of a stream.
        /// </summary>
        /// <typeparam name="T">The stream's content structure</typeparam>
        /// <returns>The content.</returns>
        public T GetContent<T>()
            where T : IByteArraySerializable, IDiagnosticTraceable, new()
        {
            byte[] buffer;
            using (Stream s = Open(FileAccess.Read))
            {
                buffer = Utilities.ReadFully(s, (int)s.Length);
            }

            T value = new T();
            value.ReadFrom(buffer, 0);
            return value;
        }
        
        /// <summary>
        /// Sets the content of a stream.
        /// </summary>
        /// <typeparam name="T">The stream's content structure</typeparam>
        /// <param name="value">The new value for the stream</param>
        public void SetContent<T>(T value)
            where T : IByteArraySerializable, IDiagnosticTraceable, new()
        {
            byte[] buffer = new byte[value.Size];
            value.WriteTo(buffer, 0);
            using (Stream s = Open(FileAccess.Write))
            {
                s.Write(buffer, 0, buffer.Length);
                s.SetLength(buffer.Length);
            }
        }

        public SparseStream Open(FileAccess access)
        {
            List<SparseStream> subStreams = new List<SparseStream>();
            foreach (var attrRef in _attrs)
            {
                subStreams.Add(_file.GetAttribute(attrRef).OpenRaw(access));
            }

            AttributeRecord firstAttrRecord = _file.GetAttribute(_attrs[0]).Record;
            if (firstAttrRecord.IsNonResident || _attrs.Count > 1)
            {
                NonResidentAttributeRecord baseRecord = (NonResidentAttributeRecord)firstAttrRecord;
                return new NonResidentAttributeStream(_file, access, baseRecord, subStreams.ToArray());
            }
            else
            {
                return subStreams[0];
            }
        }

        internal Range<long, long>[] GetClusters()
        {
            List<Range<long, long>> ranges = new List<Range<long, long>>();

            foreach (var attrRef in _attrs)
            {
                ranges.AddRange(_file.GetAttribute(attrRef).GetClusters());
            }

            return ranges.ToArray();
        }

        internal StreamExtent[] GetAbsoluteExtents()
        {
            List<StreamExtent> result = new List<StreamExtent>();

            long clusterSize = _file.Context.BiosParameterBlock.BytesPerCluster;

            foreach (var attrRef in _attrs)
            {
                NtfsAttribute attr = _file.GetAttribute(attrRef);
                if (attr.IsNonResident)
                {
                    Range<long, long>[] clusters = attr.GetClusters();
                    foreach (var clusterRange in clusters)
                    {
                        result.Add(new StreamExtent(clusterRange.Offset * clusterSize, clusterRange.Count * clusterSize));
                    }
                    return result.ToArray();
                }
                else
                {
                    result.Add(new StreamExtent(attr.OffsetToAbsolutePos(0), attr.Length));
                }
            }

            return result.ToArray();
        }
    }
}
