using DiscUtils.CoreCompat;
using LibraryTests;
using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: Xunit.TestFramework(nameof(LibraryTests) + "." + nameof(InitalizationClass), "LibraryTests")]

namespace LibraryTests
{
    public class InitalizationClass : XunitTestFramework
    {
        public InitalizationClass(IMessageSink messageSink)
            : base(messageSink)
        {
            EncodingHelper.RegisterEncodings();
        }
    }
}