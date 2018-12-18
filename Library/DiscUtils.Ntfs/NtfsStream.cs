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

using System.Collections.Generic;
using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.Ntfs
{
    internal class NtfsStream
    {
        private readonly File _file;

        public NtfsStream(File file, NtfsAttribute attr)
        {
            _file = file;
            Attribute = attr;
        }

        public NtfsAttribute Attribute { get; }

        public AttributeType AttributeType
        {
            get { return Attribute.Type; }
        }

        public string Name
        {
            get { return Attribute.Name; }
        }

        /// <summary>
        /// Gets the content of a stream.
        /// </summary>
        /// <typeparam name="T">The stream's content structure.</typeparam>
        /// <returns>The content.</returns>
        public T GetContent<T>()
            where T : IByteArraySerializable, IDiagnosticTraceable, new()
        {
            byte[] buffer;
            using (Stream s = Open(FileAccess.Read))
            {
                buffer = StreamUtilities.ReadExact(s, (int)s.Length);
            }

            T value = new T();
            value.ReadFrom(buffer, 0);
            return value;
        }

        /// <summary>
        /// Sets the content of a stream.
        /// </summary>
        /// <typeparam name="T">The stream's content structure.</typeparam>
        /// <param name="value">The new value for the stream.</param>
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
            return Attribute.Open(access);
        }

        internal Range<long, long>[] GetClusters()
        {
            return Attribute.GetClusters();
        }

        internal StreamExtent[] GetAbsoluteExtents()
        {
            List<StreamExtent> result = new List<StreamExtent>();

            long clusterSize = _file.Context.BiosParameterBlock.BytesPerCluster;
            if (Attribute.IsNonResident)
            {
                Range<long, long>[] clusters = Attribute.GetClusters();
                foreach (Range<long, long> clusterRange in clusters)
                {
                    result.Add(new StreamExtent(clusterRange.Offset * clusterSize, clusterRange.Count * clusterSize));
                }
            }
            else
            {
                result.Add(new StreamExtent(Attribute.OffsetToAbsolutePos(0), Attribute.Length));
            }

            return result.ToArray();
        }
    }
}