//
// Copyright (c) 2008-2013, Kenneth Bell
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
    using System.Collections.Generic;
    using System.IO;

    internal sealed class LogEntry
    {
        public const int LogSectorSize = (int)(4 * Sizes.OneKiB);

        private LogEntryHeader _header;
        private List<Descriptor> _descriptors = new List<Descriptor>();
        private long _position;

        private LogEntry(long position, LogEntryHeader header, List<Descriptor> descriptors)
        {
            _position = position;
            _header = header;
            _descriptors = descriptors;
        }

        public ulong SequenceNumber
        {
            get { return _header.SequenceNumber; }
        }

        public uint Tail
        {
            get { return _header.Tail; }
        }

        public long Position
        {
            get { return _position; }
        }

        public Guid LogGuid
        {
            get { return _header.LogGuid; }
        }

        public ulong FlushedFileOffset
        {
            get { return _header.FlushedFileOffset; }
        }

        public ulong LastFileOffset
        {
            get { return _header.LastFileOffset; }
        }

        public bool IsEmpty
        {
            get { return _descriptors.Count == 0; }
        }

        public IEnumerable<Range<ulong, ulong>> ModifiedExtents
        {
            get
            {
                foreach (var descriptor in _descriptors)
                {
                    yield return new Range<ulong, ulong>(descriptor.FileOffset, descriptor.FileLength);
                }
            }
        }

        public static bool TryRead(Stream logStream, out LogEntry entry)
        {
            long position = logStream.Position;

            byte[] sectorBuffer = new byte[LogSectorSize];
            Utilities.ReadFully(logStream, sectorBuffer, 0, sectorBuffer.Length);

            uint sig = Utilities.ToUInt32LittleEndian(sectorBuffer, 0);
            if (sig != LogEntryHeader.LogEntrySignature)
            {
                entry = null;
                return false;
            }

            LogEntryHeader header = new LogEntryHeader();
            header.ReadFrom(sectorBuffer, 0);

            if (!header.IsValid || header.EntryLength > logStream.Length)
            {
                entry = null;
                return false;
            }

            byte[] logEntryBuffer = new byte[header.EntryLength];
            Array.Copy(sectorBuffer, logEntryBuffer, LogSectorSize);

            Utilities.ReadFully(logStream, logEntryBuffer, LogSectorSize, logEntryBuffer.Length - LogSectorSize);

            Utilities.WriteBytesLittleEndian((int)0, logEntryBuffer, 4);
            if (header.Checksum != Crc32LittleEndian.Compute(Crc32Algorithm.Castagnoli, logEntryBuffer, 0, (int)header.EntryLength))
            {
                entry = null;
                return false;
            }

            int dataPos = Utilities.RoundUp(((int)header.DescriptorCount * 32) + 64, LogSectorSize);

            List<Descriptor> descriptors = new List<Descriptor>();
            for (int i = 0; i < header.DescriptorCount; ++i)
            {
                int offset = (i * 32) + 64;
                Descriptor descriptor;

                uint descriptorSig = Utilities.ToUInt32LittleEndian(logEntryBuffer, offset);
                switch (descriptorSig)
                {
                    case Descriptor.ZeroDescriptorSignature:
                        descriptor = new ZeroDescriptor();
                        break;
                    case Descriptor.DataDescriptorSignature:
                        descriptor = new DataDescriptor(logEntryBuffer, dataPos);
                        dataPos += LogSectorSize;
                        break;
                    default:
                        entry = null;
                        return false;
                }

                descriptor.ReadFrom(logEntryBuffer, offset);
                if (!descriptor.IsValid(header.SequenceNumber))
                {
                    entry = null;
                    return false;
                }

                descriptors.Add(descriptor);
            }

            entry = new LogEntry(position, header, descriptors);
            return true;
        }

        private abstract class Descriptor : IByteArraySerializable
        {
            public const uint ZeroDescriptorSignature = 0x6F72657A;
            public const uint DataDescriptorSignature = 0x63736564;

            public ulong FileOffset;
            public ulong SequenceNumber;

            public int Size
            {
                get { return 32; }
            }

            public abstract ulong FileLength { get; }

            public abstract int ReadFrom(byte[] buffer, int offset);

            public abstract void WriteTo(byte[] buffer, int offset);

            public abstract bool IsValid(ulong sequenceNumber);
        }

        private sealed class ZeroDescriptor : Descriptor
        {
            public ulong ZeroLength;

            public override ulong FileLength
            {
                get { return ZeroLength; }
            }

            public override int ReadFrom(byte[] buffer, int offset)
            {
                ZeroLength = Utilities.ToUInt64LittleEndian(buffer, offset + 8);
                FileOffset = Utilities.ToUInt64LittleEndian(buffer, offset + 16);
                SequenceNumber = Utilities.ToUInt64LittleEndian(buffer, offset + 24);

                return 32;
            }

            public override void WriteTo(byte[] buffer, int offset)
            {
                throw new NotImplementedException();
            }

            public override bool IsValid(ulong sequenceNumber)
            {
                return SequenceNumber == sequenceNumber;
            }
        }

        private sealed class DataDescriptor : Descriptor
        {
            public uint TrailingBytes;
            public ulong LeadingBytes;

            private byte[] _data;
            private int _offset;

            public DataDescriptor(byte[] data, int offset)
            {
                _data = data;
                _offset = offset;
            }

            public override ulong FileLength
            {
                get { return LogSectorSize; }
            }

            public override int ReadFrom(byte[] buffer, int offset)
            {
                TrailingBytes = Utilities.ToUInt32LittleEndian(buffer, offset + 4);
                LeadingBytes = Utilities.ToUInt64LittleEndian(buffer, offset + 8);
                FileOffset = Utilities.ToUInt64LittleEndian(buffer, offset + 16);
                SequenceNumber = Utilities.ToUInt64LittleEndian(buffer, offset + 24);

                return 32;
            }

            public override void WriteTo(byte[] buffer, int offset)
            {
                throw new NotImplementedException();
            }

            public override bool IsValid(ulong sequenceNumber)
            {
                return SequenceNumber == sequenceNumber
                    && _offset + LogSectorSize <= _data.Length
                    && Utilities.ToUInt32LittleEndian(_data, _offset + LogSectorSize - 4) == (sequenceNumber & 0xFFFFFFFF)
                    && Utilities.ToUInt32LittleEndian(_data, _offset + 4) == ((sequenceNumber >> 32) & 0xFFFFFFFF);
            }
        }
    }
}
