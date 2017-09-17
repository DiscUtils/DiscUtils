using System;
using System.IO;
using DiscUtils;
using DiscUtils.Vfat;

namespace MakeVFatImage
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var s = new FileStream("test.img", FileMode.Create))
            using (var f = VfatFileSystem.FormatFloppy(s, FloppyDiskType.HighDensity, null))
            {
                using (var fs = f.OpenFile("autounattend.xml", FileMode.Create))
                using (var sw = new StreamWriter(fs))
                {
                    sw.WriteLine("<xml>Hello World!</xml>");
                }
            }
        }
    }
}
