using System.IO;
using DiscUtils.Fat;
using DiscUtils.Vfat;
using Xunit;

namespace LibraryTests.Vfat
{
	public class VfatFileSystemTest
	{
		[Fact]
		public void MakeAutounattend()
		{
			var xml = "<xml>Hello World!</xml>";

			using (var s = new MemoryStream())
			{
				using (var f = VfatFileSystem.FormatFloppy(s, DiscUtils.FloppyDiskType.HighDensity, null))
				{
					using (var fs = f.OpenFile("autounattend.xml", FileMode.Create))
					using (var sw = new StreamWriter(fs))
					{
						sw.WriteLine(xml);
					}
				}

				s.Seek(0, SeekOrigin.Begin);

				// TODO: For now we use FatFileSystem to read back the file data.
				using (var f = new FatFileSystem(s))
				{
					using (var fs = f.OpenFile("AUTOUN~1.XML", FileMode.Open, FileAccess.Read))
					using (var sr = new StreamReader(fs))
					{
						var data = sr.ReadLine();
						Assert.Equal(data, xml);
					}
				}
			}
		}
	}
}
