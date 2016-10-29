namespace DiscUtils.Iscsi
{
    /// <summary>
    /// The known classes of SCSI device.
    /// </summary>
    public enum LunClass
    {
        /// <summary>
        /// Device is block storage (i.e. normal disk).
        /// </summary>
        BlockStorage = 0x00,

        /// <summary>
        /// Device is sequential access storage.
        /// </summary>
        TapeStorage = 0x01,

        /// <summary>
        /// Device is a printer.
        /// </summary>
        Printer = 0x02,

        /// <summary>
        /// Device is a SCSI processor.
        /// </summary>
        Processor = 0x03,

        /// <summary>
        /// Device is write-once storage.
        /// </summary>
        WriteOnceStorage = 0x04,

        /// <summary>
        /// Device is a CD/DVD drive.
        /// </summary>
        OpticalDisc = 0x05,

        /// <summary>
        /// Device is a scanner (obsolete).
        /// </summary>
        Scanner = 0x06,

        /// <summary>
        /// Device is optical memory (some optical discs).
        /// </summary>
        OpticalMemory = 0x07,

        /// <summary>
        /// Device is a media changer device.
        /// </summary>
        Jukebox = 0x08,

        /// <summary>
        /// Communications device (obsolete).
        /// </summary>
        Communications = 0x09,

        /// <summary>
        /// Device is a Storage Array (e.g. RAID).
        /// </summary>
        StorageArray = 0x0C,

        /// <summary>
        /// Device is Enclosure Services.
        /// </summary>
        EnclosureServices = 0x0D,

        /// <summary>
        /// Device is a simplified block device.
        /// </summary>
        SimplifiedDirectAccess = 0x0E,

        /// <summary>
        /// Device is an optical card reader/writer device.
        /// </summary>
        OpticalCard = 0x0F,

        /// <summary>
        /// Device is a Bridge Controller.
        /// </summary>
        BridgeController = 0x10,

        /// <summary>
        /// Device is an object-based storage device.
        /// </summary>
        ObjectBasedStorage = 0x11,

        /// <summary>
        /// Device is an Automation/Drive interface.
        /// </summary>
        AutomationDriveInterface = 0x12,

        /// <summary>
        /// Device is a Security Manager.
        /// </summary>
        SecurityManager = 0x13,

        /// <summary>
        /// Device is a well-known device, as defined by SCSI specifications.
        /// </summary>
        WellKnown = 0x1E,

        /// <summary>
        /// Unknown LUN class.
        /// </summary>
        Unknown = 0xFF
    }
}