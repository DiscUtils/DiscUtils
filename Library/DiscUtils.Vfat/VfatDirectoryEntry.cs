using System.IO;
using DiscUtils.Fat;
using DiscUtils.Streams;

namespace DiscUtils.Vfat
{
    internal class VfatDirectoryEntry : DirectoryEntry
    {
        private VfatFileName _fileName;
        private int _part;

        public VfatDirectoryEntry(int part, VfatFileSystemOptions options, VfatFileName name, FatType fatVariant)
            : base(options, name, (FatAttributes)0x0f, fatVariant)
        {
            _part = part;
            _fileName = name;
        }

        internal override void WriteTo(Stream stream)
        {
            byte sequenceNo = (byte)(_part + 1);

            if (_part == _fileName.LastPart)
                sequenceNo |= 0x40; //The sequence number also has bit 6(0x40) set this means the last LFN entry

            byte[] buffer = new byte[32];
            buffer[0] = sequenceNo; //Sequence Number
            _fileName.GetPart(_part, buffer);
            buffer[0x0B] = Attr; //Attributes (always 0x0F)
            buffer[0x0C] = 0x00; //Type (always 0x00 for VFAT LFN)
            buffer[0x0D] = _fileName.Checksum(); //Checksum of DOS file name
            buffer[0x1A] = 0x00; buffer[0x1B] = 0x00; //First cluster(always 0x0000)

            stream.Write(buffer, 0, buffer.Length);
        }

    }
}
