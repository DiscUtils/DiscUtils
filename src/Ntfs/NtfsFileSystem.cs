//
// Copyright (c) 2008, Kenneth Bell
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

using System.IO;

namespace DiscUtils.Ntfs
{
    /// <summary>
    /// Class for accessing NTFS file systems.
    /// </summary>
    public class NtfsFileSystem
    {
        private NtfsOptions _options;

        private Stream _stream;

        private BiosParameterBlock _bpb;

        private MasterFileTable _mft;

        /// <summary>
        /// Creates a new instance from a stream.
        /// </summary>
        /// <param name="stream">The stream containing the NTFS file system</param>
        public NtfsFileSystem(Stream stream)
        {
            _options = new NtfsOptions();

            _stream = stream;


            _stream.Position = 0;
            byte[] bytes = Utilities.ReadFully(_stream, 512);

            _bpb = BiosParameterBlock.FromBytes(bytes, 0, bytes.Length);

            _stream.Position = _bpb.MftCluster * _bpb.SectorsPerCluster * _bpb.BytesPerSector;
            byte[] mftSelfRecordData = Utilities.ReadFully(_stream, _bpb.MftRecordSize * _bpb.SectorsPerCluster * _bpb.BytesPerSector);
            FileRecord mftSelfRecord = new FileRecord(_bpb.BytesPerSector);
            mftSelfRecord.FromBytes(mftSelfRecordData, 0);

            _mft = new MasterFileTable(this, mftSelfRecord);
        }

        /// <summary>
        /// Gets the options that control how the file system is interpreted.
        /// </summary>
        public NtfsOptions Options
        {
            get { return _options; }
        }

        /// <summary>
        /// Opens the Master File Table as a raw stream.
        /// </summary>
        /// <returns></returns>
        public Stream OpenMasterFileTable()
        {
            return _mft.OpenAttribute(AttributeType.Data);
        }

        internal Stream RawStream
        {
            get { return _stream; }
        }

        internal BiosParameterBlock BiosParameterBlock
        {
            get { return _bpb; }
        }

        internal MasterFileTable MasterFileTable
        {
            get { return _mft; }
        }

        internal long BytesPerCluster
        {
            get { return _bpb.BytesPerSector * _bpb.SectorsPerCluster; }
        }

        /// <summary>
        /// Writes a diagnostic dump of key NTFS structures.
        /// </summary>
        /// <param name="writer">The writer to receive the dump.</param>
        public void Dump(TextWriter writer)
        {
            writer.WriteLine("NTFS File System Dump");
            writer.WriteLine("=====================");

            _mft.Dump(writer, "");

            writer.WriteLine();
            writer.WriteLine("DIRECTORY TREE");
            writer.WriteLine(@"\ (5)");
            DumpDirectory(_mft.GetDirectory(5), writer, "");  // 5 = Root Dir
        }

        private void DumpDirectory(Directory dir, TextWriter writer, string indent)
        {
            foreach (File file in dir.GetMembers())
            {
                Directory asDir = file as Directory;
                writer.WriteLine(indent + "+-" + file.ToString() + " (" + file.MasterFileTableIndex + ")");

                // Recurse - but avoid infinite recursion via the root dir...
                if (asDir != null && file.MasterFileTableIndex != 5)
                {
                    DumpDirectory(asDir, writer, indent + "| ");
                }
            }
        }
    }
}
