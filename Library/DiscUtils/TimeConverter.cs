using System;

namespace DiscUtils
{
    /// <summary>
    /// Converts a time to/from UTC.
    /// </summary>
    /// <param name="time">The time to convert.</param>
    /// <param name="toUtc"><c>true</c> to convert FAT time to UTC, <c>false</c> to convert UTC to FAT time.</param>
    /// <returns>The converted time.</returns>
    public delegate DateTime TimeConverter(DateTime time, bool toUtc);
}