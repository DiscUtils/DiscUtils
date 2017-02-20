namespace System
{
    /// <summary>
    /// DateTimeOffset extension methods
    /// </summary>
    public static class DateTimeOffsetExtensions
    {
        /// <summary>
        /// The Epoch common to most (all?) Unix systems.
        /// </summary>
        public static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        public static DateTimeOffset FromUnixTimeSeconds(this long seconds)
        {
#if NETCORE
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
#else
            DateTimeOffset dateTimeOffset = new DateTimeOffset(DateTimeOffsetExtensions.UnixEpoch);
            dateTimeOffset = dateTimeOffset.AddSeconds(seconds);
            return dateTimeOffset;
#endif
        }

#if !NETCORE
        public static long ToUnixTimeSeconds(this DateTimeOffset dateTimeOffset)
        {
            long unixTimeStampInTicks = (dateTimeOffset.ToUniversalTime() - DateTimeOffsetExtensions.UnixEpoch).Ticks;
            return unixTimeStampInTicks / TimeSpan.TicksPerSecond;
        }
#endif
    }
}