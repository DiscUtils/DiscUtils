//
// Copyright (c) 2013, Adam Bridge
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

namespace DiscUtils.Ewf
{
    class EWFStream : SparseStream
    {
        List<ChunkInfo> _chunkInfos = new List<ChunkInfo>();
        long[] _chunkStarts; // array for the starting offsets for each chunk - used for quick find of chunk
        int _bytesPerChunk = -1;

        byte[] _currentChunk; // holds the (decompressed) chunk in memory
        int _currentChunkIndex; // pointer to the current chunk in the ChunkInfo collection
        int _currentChunkPointer; // position of the chunk "stream"

        long _length = -1; // Total number of bytes in stream

        public EWFStream(string fileName)
        {
            DiskType = VirtualDiskClass.None; // Set a default
            Sections = new Dictionary<string, object>();
            Files = new List<string>();

            Files.Add(fileName);

            ResolveFileChain();
            PopulateChunkInfo();

            _currentChunk = getChunk(0, out _currentChunkIndex);
        }

        private Dictionary<string, object> Sections { get; set; }

        /// <summary>
        /// Gets the specified section as read from the E01 file.
        /// </summary>
        /// <param name="SectionName">The section sought.</param>
        /// <returns>The section object or null if section not found.</returns>
        public object GetSection(string SectionName)
        {
            return Sections.ContainsKey(SectionName) ? Sections[SectionName] : null;
        }

        /// <summary>
        /// The list of segment files (e01, e02, ...) that make up the disk.
        /// </summary>
        public List<string> Files { get; private set; }

        public string MD5 { get; private set; }

        public VirtualDiskClass DiskType { get; private set; }

        public override IEnumerable<StreamExtent> Extents
        {
            get { return new StreamExtent[] { new StreamExtent(0, Length) }; }
        }

        /// <summary>
        /// Whether or not the stream can be read.
        /// </summary>
        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Whether or not the stream is seekable.
        /// </summary>
        public override bool CanSeek
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Returns true if the stream can be written to, false otherwise.
        /// </summary>
        /// <remarks>EWF support is currently read-only, so this will always return false.</remarks>
        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Flush the contents of the stream to disk.
        /// </summary>
        /// <remarks>EWF support is currently read-only, so this will throw <code>NotImplementedException</code>.</remarks>
        public override void Flush()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Total number of bytes in the stream.
        /// </summary>
        public override long Length
        {
            get { return _length; }
        }

        /// <summary>
        /// Returns current position in the stream.
        /// </summary>
        public override long Position { get; set; }

        /// <summary>
        /// Get the bytes of the chunk that contains a specified offset.
        /// </summary>
        /// <param name="start">The position in the stream for which bytes are wanted.</param>
        /// <param name="currentChunkIndex">Stores the index of the chunk.</param>
        /// <returns>byte array containing the bytes of the chunk.</returns>
        private byte[] getChunk(long start, out int currentChunkIndex)
        {
            currentChunkIndex = mapRequestToChunkInfo(start, out _currentChunkPointer);

            byte[] buff = new byte[_chunkInfos[currentChunkIndex].BytesInChunk];
            byte[] emulatedBytes = new byte[_bytesPerChunk];

            using (FileStream fs = File.OpenRead(Files[_chunkInfos[currentChunkIndex].FileIndex]))
            {
                fs.Seek(_chunkInfos[currentChunkIndex].FileOffset, SeekOrigin.Begin);
                fs.Read(buff, 0, _chunkInfos[currentChunkIndex].BytesInChunk);

                if (_chunkInfos[_currentChunkIndex].IsCompressed) // If compressed...
                {
                    using (MemoryStream ms = new MemoryStream(buff, false)) // ...decompress...
                    {
                        DiscUtils.Compression.ZlibStream zlib = new Compression.ZlibStream(ms, System.IO.Compression.CompressionMode.Decompress, true);
                        zlib.Read(emulatedBytes, 0, _bytesPerChunk);
                    }
                }
                else // ...no need.
                {
                    emulatedBytes = buff;
                }
            }

            return emulatedBytes; // decompressed (or not) bytes
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count == 0 || Position == _length)
                return 0;

            if (count < 0)
                throw new ArgumentException("requested bytes cannot be less than 0");

            if (offset + count > buffer.Length)
                throw new ArgumentException("buffer is not big enough for the requested bytes");

            int read = 0;
            int bytesLeft = count;

            while (bytesLeft > 0)
            {
                // Is requested position in current chunk?
                long end = _chunkInfos[_currentChunkIndex].Start + _chunkInfos[_currentChunkIndex].Length;
                long start = _chunkInfos[_currentChunkIndex].Start;
                if (!(Position >= start && Position < end))
                    _currentChunk = getChunk(Position, out _currentChunkIndex); // If not, get it

                int toCopy = _currentChunk.Length - _currentChunkPointer; // bytes left in chunk
                if (toCopy > bytesLeft) toCopy = bytesLeft; // adjust so it's not more than was actually requested

                Array.Copy(_currentChunk, _currentChunkPointer, buffer, offset, toCopy);

                read += toCopy; // Increase bytes read on this request
                bytesLeft -= toCopy; // Decrease bytes left to read on this request 
                Position += toCopy; // Increase the position of the stream
                _currentChunkPointer += toCopy; // Increase the position of the current chunk
            }

            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;

                case SeekOrigin.Current:
                    Position += offset;
                    break;

                case SeekOrigin.End:
                    Position = _length + offset;
                    break;
            }

            return Position;
        }

        public override void SetLength(long value)
        {
            _length = value;
        }

        /// <summary>
        /// Writes contents of byte array to the EWFStream.
        /// </summary>
        /// <param name="buffer">array of bytes from which to write</param>
        /// <param name="offset">offset in <c>buffer</c> from where to start writing</param>
        /// <param name="count">number of bytes to write</param>
        /// <remarks>EWF support is currently read-only, so this will throw <code>NotImplementedException</code>.</remarks>        
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        private int mapRequestToChunkInfo(long start, out int currentChunkStartFrom)
        {
            if (start < 0)
                throw new IOException("attempt to read before start of stream");

            int result = -1;

            int i = Array.BinarySearch<long>(_chunkStarts, start);

            if (i < 0)
            {
                i = ~i;
                result = (i == _chunkStarts.Length) ? _chunkStarts.Length - 1 : (i - 1);
                currentChunkStartFrom = (int)(start - _chunkInfos[result].Start);
            }
            else
            {
                result = i;
                currentChunkStartFrom = 0;
            }

            return result;
        }

        private void ResolveFileChain()
        {
            int i = 0;
            bool gotDone = false;

            do
            {
                using (FileStream fs = File.OpenRead(Files[i]))
                {
                    try
                    {
                        byte[] buff = new byte[13];
                        fs.Read(buff, 0, 13);
                        EWFHeader header = new EWFHeader(buff);
                        if (header.SegmentNumber != i + 1)
                            throw new ArgumentException(string.Format("invalid segment order: got {0}, should be {1}", header.SegmentNumber, i + 1));

                        buff = new byte[76];
                        fs.Seek(-76, SeekOrigin.End);
                        fs.Read(buff, 0, 76);
                        SectionDescriptor secDescriptor = new SectionDescriptor(buff, fs.Length - 76);
                        if (secDescriptor.SectionType == SECTION_TYPE.next)
                        {
                            Files.Add(Utils.CalcNextFilename(Files[i]));
                            i++;
                        }
                        else if (secDescriptor.SectionType == SECTION_TYPE.done)
                        {
                            gotDone = true;
                        }
                        else
                        {
                            throw new Exception(string.Format("unexpected final section: {0}", secDescriptor.SectionType));
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(string.Format("error whilst processing {0}", Files[i]), ex);
                    }
                }
            } while (!gotDone);
        }

        private byte[] readSection(SectionDescriptor sd, FileStream fs)
        {
            byte[] buff = new byte[sd.NextSectionOffset - (int)fs.Position];
            fs.Read(buff, 0, sd.NextSectionOffset - (int)fs.Position);
            return buff;
        }

        private void PopulateChunkInfo()
        {
            long thisChunk = 0;

            for (int fileCount = 0; fileCount < Files.Count; fileCount++)
            {
                using (FileStream fs = File.OpenRead(Files[fileCount]))
                {
                    bool gotTable, gotHeader, gotHeader2;
                    gotTable = gotHeader = gotHeader2 = false;
                    int endOfSectors = -1;

                    fs.Seek(13, SeekOrigin.Begin); // Skip file header

                    while (fs.Position < fs.Length)
                    {
                        byte[] buff = new byte[76];
                        fs.Read(buff, 0, 76);
                        SectionDescriptor sd;
                        try
                        {
                            sd = new SectionDescriptor(buff, fs.Position - 76);
                        }
                        catch (Exception ex)
                        {
                            throw new Exception(Files[fileCount], ex);
                        }

                        switch (sd.SectionType)
                        {
                            case SECTION_TYPE.header:
                                break;
                            case SECTION_TYPE.header2:
                                break;
                            case SECTION_TYPE.ltypes:
                                break;
                            case SECTION_TYPE.ltree:
                                break;
                            case SECTION_TYPE.session:
                                break;
                            case SECTION_TYPE.error2:
                                break;
                            case SECTION_TYPE.incomplete:
                                break;
                            case SECTION_TYPE.disk:
                                break;
                            case SECTION_TYPE.volume:
                                break;
                            case SECTION_TYPE.sectors:
                                break;
                            case SECTION_TYPE.table:
                                break;
                            case SECTION_TYPE.table2:
                                break;
                            case SECTION_TYPE.next:
                                break;
                            case SECTION_TYPE.data:
                                break;
                            case SECTION_TYPE.digest:
                                break;
                            case SECTION_TYPE.hash:
                                break;
                            case SECTION_TYPE.done:
                                break;
                            default:
                                break;
                        }

                        switch (sd.SectionType)
                        {
                            case SECTION_TYPE.header:
                                Sections.Add("header", new Section.Header2(readSection(sd, fs)));
                                break;

                            case SECTION_TYPE.header2:
                                Sections.Add("header2", new Section.Header2(readSection(sd, fs)));
                                break;

                            case SECTION_TYPE.disk:
                            case SECTION_TYPE.data:
                            case SECTION_TYPE.volume:
                                // Calculate bytes per chunk
                                buff = new byte[sd.NextSectionOffset - (int)fs.Position];
                                fs.Read(buff, 0, sd.NextSectionOffset - (int)fs.Position);
                                Section.Volume VolumeSection = new Section.Volume(buff);
                                Sections.Add("volume", VolumeSection);
                                _bytesPerChunk = VolumeSection.SectorsPerChunk * VolumeSection.BytesPerSector;

                                switch (VolumeSection.MediaType)
                                {
                                    case MEDIA_TYPE.Disc:
                                        DiskType = VirtualDiskClass.OpticalDisk;
                                        break;
                                    case MEDIA_TYPE.Fixed:
                                        DiskType = VirtualDiskClass.HardDisk;
                                        break;
                                    default:
                                        DiskType = VirtualDiskClass.None;
                                        break;
                                }

                                if (_length < 0)
                                    _length = (long)VolumeSection.BytesPerSector * (long)VolumeSection.SectorCount;

                                break;

                            case SECTION_TYPE.table:
                            case SECTION_TYPE.table2:
                                // Calculate the chunks
                                if (gotTable) // Don't read both versions of the table...
                                { // ...so skip the bytes
                                    fs.Seek(sd.NextSectionOffset - (int)fs.Position, SeekOrigin.Current);
                                }
                                else
                                {
                                    if (_bytesPerChunk < 0)
                                        throw new Exception(string.Format("table/table2 section before disk/data/volume, in {0}", Files[fileCount]));

                                    bool volumeCompression = ((Section.Volume)GetSection("volume")).Compression != COMPRESSION.None;

                                    buff = new byte[sd.NextSectionOffset - fs.Position];
                                    fs.Read(buff, 0, sd.NextSectionOffset - (int)fs.Position);
                                    Section.Table table = new Section.Table(buff);
                                    Sections.Add("table", table);
                                    for (int i = 0; i < table.Entries.Count; i++)
                                    {
                                        Section.Table.TableEntry entry = table.Entries[i];
                                        if (_chunkInfos.Count == 261)
                                            Console.WriteLine("!!!");
                                        int bytesInChunk = i == table.Entries.Count - 1 ? endOfSectors - entry.Offset : table.Entries[i + 1].Offset - entry.Offset;
                                        _chunkInfos.Add(new ChunkInfo(
                                            fileCount,
                                            entry.Offset,
                                            _bytesPerChunk * thisChunk++,
                                            _bytesPerChunk,
                                            volumeCompression | entry.Compressed,
                                            bytesInChunk));
                                    }
                                }
                                gotTable = !gotTable;
                                break;

                            case SECTION_TYPE.next:
                            case SECTION_TYPE.done:
                                fs.Seek(76, SeekOrigin.Current);
                                break;

                            case SECTION_TYPE.sectors:
                                endOfSectors = sd.NextSectionOffset;
                                fs.Seek(sd.NextSectionOffset - (int)fs.Position, SeekOrigin.Current);
                                break;

                            case SECTION_TYPE.digest:
                                if (MD5 == null)
                                {
                                    buff = new byte[sd.NextSectionOffset - fs.Position];
                                    fs.Read(buff, 0, sd.NextSectionOffset - (int)fs.Position);
                                    Section.Digest digest = new Section.Digest(buff);
                                    MD5 = digest.MD5;
                                }
                                else
                                    fs.Seek(sd.NextSectionOffset - (int)fs.Position, SeekOrigin.Current);
                                break;

                            case SECTION_TYPE.hash:
                                if (MD5 == null)
                                {
                                    buff = new byte[sd.NextSectionOffset - fs.Position];
                                    fs.Read(buff, 0, sd.NextSectionOffset - (int)fs.Position);
                                    Section.Hash hash = new Section.Hash(buff);
                                    MD5 = hash.MD5;
                                }
                                else
                                    fs.Seek(sd.NextSectionOffset - (int)fs.Position, SeekOrigin.Current);
                                break;

                            default: // Don't care about any other sections
                                fs.Seek(sd.NextSectionOffset - (int)fs.Position, SeekOrigin.Current);
                                break;
                        }
                    }
                }
            }

            if (_chunkInfos.Count > 0)
            {
                _chunkStarts = new long[_chunkInfos.Count];
                int i = 0;
                foreach (ChunkInfo ci in _chunkInfos)
                {
                    _chunkStarts[i++] = ci.Start;
                }
            }
        }
    }
}
