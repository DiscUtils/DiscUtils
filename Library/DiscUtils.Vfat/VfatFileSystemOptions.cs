using System.Text;
using DiscUtils.Fat;

namespace DiscUtils.Vfat
{
    public class VfatFileSystemOptions : FatFileSystemOptions
    {
        public Encoding PrimaryEncoding { get; set; }
        public Encoding SecondaryEncoding { get; set; }

        public VfatFileSystemOptions()
        {
            PrimaryEncoding = Encoding.GetEncoding(437);
            SecondaryEncoding = Encoding.GetEncoding("ucs-2");
        }
    }
}
