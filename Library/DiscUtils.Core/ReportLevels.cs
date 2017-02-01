using System;

namespace DiscUtils
{
    /// <summary>
    /// Flags for the amount of detail to include in a report.
    /// </summary>
    [Flags]
    public enum ReportLevels
    {
        /// <summary>
        /// Report no information.
        /// </summary>
        None = 0x00,

        /// <summary>
        /// Report informational level items.
        /// </summary>
        Information = 0x01,

        /// <summary>
        /// Report warning level items.
        /// </summary>
        Warnings = 0x02,

        /// <summary>
        /// Report error level items.
        /// </summary>
        Errors = 0x04,

        /// <summary>
        /// Report all items.
        /// </summary>
        All = 0x07
    }
}