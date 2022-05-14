#if !NET462
using System.Text;
#endif

namespace DiscUtils.CoreCompat
{
    internal static class EncodingHelper
    {
        private static bool _registered;

        public static void RegisterEncodings()
        {
            if (_registered)
                return;

            _registered = true;

#if !NET462
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
        }
    }
}