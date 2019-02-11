using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using DiscUtils.CoreCompat;

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

        public static Stream LoadDataFile(string name)
        {
            Assembly assembly = ReflectionHelper.GetAssembly(typeof(Helpers));

            string formattedName = $"{assembly.GetName().Name}._Data.{name}";
            if (assembly.GetManifestResourceNames().Contains(formattedName))
                return assembly.GetManifestResourceStream(formattedName);

            // Try GZ
            formattedName += ".gz";

            if (assembly.GetManifestResourceNames().Contains(formattedName))
            {
                MemoryStream ms = new MemoryStream();

                using (Stream stream = assembly.GetManifestResourceStream(formattedName))
                using (GZipStream gz = new GZipStream(stream, CompressionMode.Decompress))
                    gz.CopyTo(ms);

                ms.Seek(0, SeekOrigin.Begin);

                return ms;
            }

            throw new Exception($"Unable to locate embedded resource {name}");
        }
    }
}
