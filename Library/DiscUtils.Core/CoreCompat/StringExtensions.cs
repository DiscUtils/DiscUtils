#if NETCORE
using System.Globalization;

namespace DiscUtils.CoreCompat
{
    internal static class StringExtensions
    {
        public static string ToUpper(this string value, CultureInfo culture)
        {
            return value.ToUpper();
        }
    }
}

#endif