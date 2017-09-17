using System;
using System.Text;
using DiscUtils.Fat;
using DiscUtils.Internal;


namespace DiscUtils.Vfat
{
    internal class VfatFileName : FileName
    {
        private byte[] _bytes;
        private byte _lastPart;

        public static VfatFileName FromPath(string path, VfatFileSystem system)
        {
            return FromName(Utilities.GetFileFromPath(path), system, 1);
        }

        public static VfatFileName FromName(string name, VfatFileSystem system,  int index)
        {
            var options = (VfatFileSystemOptions)system.FatOptions;

            var ext = Utilities.GetExtFromPath(name);

            var primaryName = string.Format("{0}~{1}{2}", name.Substring(0, 6).ToUpperInvariant(), index, ext.ToUpperInvariant());

            return new VfatFileName(primaryName, name, options.PrimaryEncoding, options.SecondaryEncoding);
        }

        public VfatFileName(string primaryName, string secondaryName, Encoding primaryEncoding, Encoding secondaryEncoding)
            : base(primaryName, primaryEncoding)
        {
            byte[] bytes = secondaryEncoding.GetBytes(secondaryName + "\u0000");

            // Integer division intentional
            _lastPart = (byte)(bytes.Length / 32);

            // We want the array bigger than we need
            _bytes = new byte[(_lastPart + 1) * 32];

            for (int i = 0; i < _bytes.Length; ++i) _bytes[i] = 0xff;

            Array.Copy(bytes, _bytes, bytes.Length);
        }

        public byte LastPart { get { return _lastPart; } }

        public void GetPart(int part, byte[] data)
        {
            var pos = part * 26; // Each part contains 10+12+4 = 26 bytes.
            Array.Copy(_bytes, pos, data, 0x01, 10);
            Array.Copy(_bytes, pos + 10, data, 0x0e, 12);
            Array.Copy(_bytes, pos + 22, data, 0x1c, 4);
        }

        public byte Checksum()
        {
            byte sum = 0;
            foreach (byte b in Raw)
                sum = (byte)(((sum & 1) << 7) + (sum >> 1) + b);

            return sum;
        }
    }
}
