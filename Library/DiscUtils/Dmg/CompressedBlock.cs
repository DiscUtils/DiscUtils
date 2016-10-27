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

namespace DiscUtils.Dmg
{
    using System;
    using System.Collections.Generic;

    internal class CompressedBlock : IByteArraySerializable
    {
        public uint Signature;
        public uint InfoVersion;
        public long FirstSector;
        public long SectorCount;
        public ulong DataStart;
        public uint DecompressBufferRequested;
        public uint BlocksDescriptor;
        public UdifChecksum CheckSum;
        public List<CompressedRun> Runs;

        public int Size
        {
            get { throw new NotImplementedException(); }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            Signature = Utilities.ToUInt32BigEndian(buffer, offset + 0);
            InfoVersion = Utilities.ToUInt32BigEndian(buffer, offset + 4);
            FirstSector = Utilities.ToInt64BigEndian(buffer, offset + 8);
            SectorCount = Utilities.ToInt64BigEndian(buffer, offset + 16);
            DataStart = Utilities.ToUInt64BigEndian(buffer, offset + 24);
            DecompressBufferRequested = Utilities.ToUInt32BigEndian(buffer, offset + 32);
            BlocksDescriptor = Utilities.ToUInt32BigEndian(buffer, offset + 36);

            CheckSum = Utilities.ToStruct<UdifChecksum>(buffer, offset + 60);

            Runs = new List<CompressedRun>();
            int numRuns = Utilities.ToInt32BigEndian(buffer, offset + 200);
            for (int i = 0; i < numRuns; ++i)
            {
                Runs.Add(Utilities.ToStruct<CompressedRun>(buffer, offset + 204 + (i * 40)));
            }

            return 0;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }
    }
}
