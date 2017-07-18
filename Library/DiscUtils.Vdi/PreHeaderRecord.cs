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

using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.Vdi
{
    internal class PreHeaderRecord
    {
        public const uint VdiSignature = 0xbeda107f;
        public const int Size = 72;

        public string FileInfo;
        public uint Signature;
        public FileVersion Version;

        public static PreHeaderRecord Initialized()
        {
            PreHeaderRecord result = new PreHeaderRecord();
            result.FileInfo = "<<< Sun xVM VirtualBox Disk Image >>>\n";
            result.Signature = VdiSignature;
            result.Version = new FileVersion(0x00010001);
            return result;
        }

        public int Read(byte[] buffer, int offset)
        {
            FileInfo = EndianUtilities.BytesToString(buffer, offset + 0, 64).TrimEnd('\0');
            Signature = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 64);
            Version = new FileVersion(EndianUtilities.ToUInt32LittleEndian(buffer, offset + 68));
            return Size;
        }

        public void Read(Stream s)
        {
            byte[] buffer = StreamUtilities.ReadExact(s, 72);
            Read(buffer, 0);
        }

        public void Write(Stream s)
        {
            byte[] buffer = new byte[Size];
            Write(buffer, 0);
            s.Write(buffer, 0, buffer.Length);
        }

        public void Write(byte[] buffer, int offset)
        {
            EndianUtilities.StringToBytes(FileInfo, buffer, offset + 0, 64);
            EndianUtilities.WriteBytesLittleEndian(Signature, buffer, offset + 64);
            EndianUtilities.WriteBytesLittleEndian(Version.Value, buffer, offset + 68);
        }
    }
}