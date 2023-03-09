using System;
using System.IO;
using System.IO.Compression;

namespace LibraryTests.Helpers
{
    public static class Helpers
    {
        public static byte[] ReadAll(this Stream stream)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }

        public static Stream LoadDataFileFromGZipFile(string path)
        {
            using (var fs = File.OpenRead(path))
            using (var gz = new GZipStream(fs, CompressionMode.Decompress))
            {
                MemoryStream ms = new MemoryStream();

                try
                {
                    gz.CopyTo(ms);
                }
                catch (Exception)
                {
                    ms.Dispose();
                    throw;
                }

                ms.Seek(0, SeekOrigin.Begin);

                return ms;
            }
        }
    }
}
