//
// Copyright (c) 2008-2011, Kenneth Bell
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//

//
// Symbolic names of BCD Elements taken from Geoff Chappell's website:
//  http://www.geoffchappell.com/viewer.htm?doc=notes/windows/boot/bcd/elements.htm
//
//

namespace DiscUtils.BootConfig
{
    /// <summary>
    /// Enumeration of known BCD elements.
    /// </summary>
    public enum WellKnownElement
    {
        /// <summary>
        /// Not specified.
        /// </summary>
        None = 0,

        /// <summary>
        /// Device containing the application.
        /// </summary>
        LibraryApplicationDevice = 0x11000001,

        /// <summary>
        /// Path to the application.
        /// </summary>
        LibraryApplicationPath = 0x12000002,

        /// <summary>
        /// Description of the object.
        /// </summary>
        LibraryDescription = 0x12000004,

        /// <summary>
        /// Preferred locale of the object.
        /// </summary>
        LibraryPreferredLocale = 0x12000005,

        /// <summary>
        /// Objects containing elements inherited by the object.
        /// </summary>
        LibraryInheritedObjects = 0x14000006,

        /// <summary>
        /// Upper bound on physical addresses used by Windows.
        /// </summary>
        LibraryTruncatePhysicalMemory = 0x15000007,

        /// <summary>
        /// List of objects, indicating recovery sequence.
        /// </summary>
        LibraryRecoverySequence = 0x14000008,

        /// <summary>
        /// Enables auto recovery.
        /// </summary>
        LibraryAutoRecoveryEnabled = 0x16000009,

        /// <summary>
        /// List of bad memory regions.
        /// </summary>
        LibraryBadMemoryList = 0x1700000A,

        /// <summary>
        /// Allow use of bad memory regions.
        /// </summary>
        LibraryAllowBadMemoryAccess = 0x1600000B,

        /// <summary>
        /// Policy on use of first mega-byte of physical RAM.
        /// </summary>
        /// <remarks>0 = UseNone, 1 = UseAll, 2 = UsePrivate.</remarks>
        LibraryFirstMegaBytePolicy = 0x1500000C,

        /// <summary>
        /// Debugger enabled.
        /// </summary>
        LibraryDebuggerEnabled = 0x16000010,

        /// <summary>
        /// Debugger type.
        /// </summary>
        /// <remarks>0 = Serial, 1 = 1394, 2 = USB.</remarks>
        LibraryDebuggerType = 0x15000011,

        /// <summary>
        /// Debugger serial port address.
        /// </summary>
        LibraryDebuggerSerialAddress = 0x15000012,

        /// <summary>
        /// Debugger serial port.
        /// </summary>
        LibraryDebuggerSerialPort = 0x15000013,

        /// <summary>
        /// Debugger serial port baud rate.
        /// </summary>
        LibraryDebuggerSerialBaudRate = 0x15000014,

        /// <summary>
        /// Debugger 1394 channel.
        /// </summary>
        LibraryDebugger1394Channel = 0x15000015,

        /// <summary>
        /// Debugger USB target name.
        /// </summary>
        LibraryDebuggerUsbTargetName = 0x12000016,

        /// <summary>
        /// Debugger ignores user mode exceptions.
        /// </summary>
        LibraryDebuggerIgnoreUserModeExceptions = 0x16000017,

        /// <summary>
        /// Debugger start policy.
        /// </summary>
        /// <remarks>0 = Active, 1 = AutoEnable, 2 = Disable.</remarks>
        LibraryDebuggerStartPolicy = 0x15000018,
 
        /// <summary>
        /// Debugger bus parameters for KDNET.
        /// </summary>
        LibraryDebuggerBusParameters = 0x12000019,
 
        /// <summary>
        /// Debugger host IP address for KDNET.
        /// </summary>
        LibraryDebuggerNetHostIp = 0x1500001a,
 
        /// <summary>
        /// Debugger port for KDNET.
        /// </summary>
        LibraryDebuggerNetPort = 0x1500001b,
 
        /// <summary>
        /// Use DHCP for KDNET?
        /// </summary>
        LibraryDebuggerNetDhcp = 0x1600001c,
 
        /// <summary>
        /// Debugger encryption key for KDNET.
        /// </summary>
        LibraryDebuggerNetKey = 0x1200001d,

        /// <summary>
        /// Emergency Management System enabled.
        /// </summary>
        LibraryEmergencyManagementSystemEnabled = 0x16000020,

        /// <summary>
        /// Emergency Management System serial port.
        /// </summary>
        LibraryEmergencyManagementSystemPort = 0x15000022,

        /// <summary>
        /// Emergency Management System baud rate.
        /// </summary>
        LibraryEmergencyManagementSystemBaudRate = 0x15000023,

        /// <summary>
        /// Load options.
        /// </summary>
        LibraryLoadOptions = 0x12000030,

        /// <summary>
        /// Displays advanced options.
        /// </summary>
        LibraryDisplayAdvancedOptions = 0x16000040,

        /// <summary>
        /// Displays UI to edit advanced options.
        /// </summary>
        LibraryDisplayOptionsEdit = 0x16000041,

        /// <summary>
        /// FVE (Full Volume Encryption - aka BitLocker?) KeyRing address.
        /// </summary>
        LibraryFveKeyRingAddress = 0x16000042,

        /// <summary>
        /// Device to contain Boot Status Log.
        /// </summary>
        LibraryBootStatusLogDevice = 0x11000043,

        /// <summary>
        /// Path to Boot Status Log.
        /// </summary>
        LibraryBootStatusLogFile = 0x12000044,

        /// <summary>
        /// Whether to append to the existing Boot Status Log.
        /// </summary>
        LibraryBootStatusLogAppend = 0x12000045,

        /// <summary>
        /// Disables graphics mode.
        /// </summary>
        LibraryGraphicsModeDisabled = 0x16000046,

        /// <summary>
        /// Configure access policy.
        /// </summary>
        /// <remarks>0 = default, 1 = DisallowMmConfig.</remarks>
        LibraryConfigAccessPolicy = 0x15000047,

        /// <summary>
        /// Disables integrity checks.
        /// </summary>
        LibraryDisableIntegrityChecks = 0x16000048,

        /// <summary>
        /// Allows pre-release signatures (test signing).
        /// </summary>
        LibraryAllowPrereleaseSignatures = 0x16000049,

        /// <summary>
        /// Console extended input.
        /// </summary>
        LibraryConsoleExtendedInput = 0x16000050,

        /// <summary>
        /// Initial console input.
        /// </summary>
        LibraryInitialConsoleInput = 0x15000051,

        /// <summary>
        /// Application display order.
        /// </summary>
        BootMgrDisplayOrder = 0x24000001,

        /// <summary>
        /// Application boot sequence.
        /// </summary>
        BootMgrBootSequence = 0x24000002,

        /// <summary>
        /// Default application.
        /// </summary>
        BootMgrDefaultObject = 0x23000003,

        /// <summary>
        /// User input timeout.
        /// </summary>
        BootMgrTimeout = 0x25000004,

        /// <summary>
        /// Attempt to resume from hibernated state.
        /// </summary>
        BootMgrAttemptResume = 0x26000005,

        /// <summary>
        /// The resume application.
        /// </summary>
        BootMgrResumeObject = 0x23000006,

        /// <summary>
        /// The tools display order.
        /// </summary>
        BootMgrToolsDisplayOrder = 0x24000010,

        /// <summary>
        /// Displays the boot menu.
        /// </summary>
        BootMgrDisplayBootMenu = 0x26000020,

        /// <summary>
        /// No error display.
        /// </summary>
        BootMgrNoErrorDisplay = 0x26000021,

        /// <summary>
        /// The BCD device.
        /// </summary>
        BootMgrBcdDevice = 0x21000022,

        /// <summary>
        /// The BCD file path.
        /// </summary>
        BootMgrBcdFilePath = 0x22000023,

        /// <summary>
        /// The custom actions list.
        /// </summary>
        BootMgrCustomActionsList = 0x27000030,

        /// <summary>
        /// Device containing the Operating System.
        /// </summary>
        OsLoaderOsDevice = 0x21000001,

        /// <summary>
        /// System root on the OS device.
        /// </summary>
        OsLoaderSystemRoot = 0x22000002,

        /// <summary>
        /// The resume application associated with this OS.
        /// </summary>
        OsLoaderAssociatedResumeObject = 0x23000003,

        /// <summary>
        /// Auto-detect the correct kernel &amp; HAL.
        /// </summary>
        OsLoaderDetectKernelAndHal = 0x26000010,

        /// <summary>
        /// The filename of the kernel.
        /// </summary>
        OsLoaderKernelPath = 0x22000011,

        /// <summary>
        /// The filename of the HAL.
        /// </summary>
        OsLoaderHalPath = 0x22000012,

        /// <summary>
        /// The debug transport path.
        /// </summary>
        OsLoaderDebugTransportPath = 0x22000013,

        /// <summary>
        /// NX (No-Execute) policy.
        /// </summary>
        /// <remarks>0 = OptIn, 1 = OptOut, 2 = AlwaysOff, 3 = AlwaysOn.</remarks>
        OsLoaderNxPolicy = 0x25000020,

        /// <summary>
        /// PAE policy.
        /// </summary>
        /// <remarks>0 = default, 1 = ForceEnable, 2 = ForceDisable.</remarks>
        OsLoaderPaePolicy = 0x25000021,

        /// <summary>
        /// WinPE mode.
        /// </summary>
        OsLoaderWinPeMode = 0x26000022,

        /// <summary>
        /// Disable automatic reboot on OS crash.
        /// </summary>
        OsLoaderDisableCrashAutoReboot = 0x26000024,

        /// <summary>
        /// Use the last known good settings.
        /// </summary>
        OsLoaderUseLastGoodSettings = 0x26000025,

        /// <summary>
        /// Disable integrity checks.
        /// </summary>
        OsLoaderDisableIntegrityChecks = 0x26000026,

        /// <summary>
        /// Allows pre-release signatures (test signing).
        /// </summary>
        OsLoaderAllowPrereleaseSignatures = 0x26000027,

        /// <summary>
        /// Loads all executables above 4GB boundary.
        /// </summary>
        OsLoaderNoLowMemory = 0x26000030,

        /// <summary>
        /// Excludes a given amount of memory from use by Windows.
        /// </summary>
        OsLoaderRemoveMemory = 0x25000031,

        /// <summary>
        /// Increases the User Mode virtual address space.
        /// </summary>
        OsLoaderIncreaseUserVa = 0x25000032,

        /// <summary>
        /// Size of buffer (in MB) for perfomance data logging.
        /// </summary>
        OsLoaderPerformanceDataMemory = 0x25000033,

        /// <summary>
        /// Uses the VGA display driver.
        /// </summary>
        OsLoaderUseVgaDriver = 0x26000040,

        /// <summary>
        /// Quiet boot.
        /// </summary>
        OsLoaderDisableBootDisplay = 0x26000041,

        /// <summary>
        /// Disables use of the VESA BIOS.
        /// </summary>
        OsLoaderDisableVesaBios = 0x26000042,

        /// <summary>
        /// Maximum processors in a single APIC cluster.
        /// </summary>
        OsLoaderClusterModeAddressing = 0x25000050,

        /// <summary>
        /// Forces the physical APIC to be used.
        /// </summary>
        OsLoaderUsePhysicalDestination = 0x26000051,

        /// <summary>
        /// The largest APIC cluster number the system can use.
        /// </summary>
        OsLoaderRestrictApicCluster = 0x25000052,

        /// <summary>
        /// Forces only the boot processor to be used.
        /// </summary>
        OsLoaderUseBootProcessorOnly = 0x26000060,

        /// <summary>
        /// The number of processors to be used.
        /// </summary>
        OsLoaderNumberOfProcessors = 0x25000061,

        /// <summary>
        /// Use maximum number of processors.
        /// </summary>
        OsLoaderForceMaxProcessors = 0x26000062,

        /// <summary>
        /// Processor specific configuration flags.
        /// </summary>
        OsLoaderProcessorConfigurationFlags = 0x25000063,

        /// <summary>
        /// Uses BIOS-configured PCI resources.
        /// </summary>
        OsLoaderUseFirmwarePciSettings = 0x26000070,

        /// <summary>
        /// Message Signalled Interrupt setting.
        /// </summary>
        OsLoaderMsiPolicy = 0x25000071,

        /// <summary>
        /// PCE Express Policy.
        /// </summary>
        OsLoaderPciExpressPolicy = 0x25000072,

        /// <summary>
        /// The safe boot option.
        /// </summary>
        /// <remarks>0 = Minimal, 1 = Network, 2 = DsRepair.</remarks>
        OsLoaderSafeBoot = 0x25000080,

        /// <summary>
        /// Loads the configured alternate shell during a safe boot.
        /// </summary>
        OsLoaderSafeBootAlternateShell = 0x26000081,

        /// <summary>
        /// Enables boot log.
        /// </summary>
        OsLoaderBootLogInitialization = 0x26000090,

        /// <summary>
        /// Displays diagnostic information during boot.
        /// </summary>
        OsLoaderVerboseObjectLoadMode = 0x26000091,

        /// <summary>
        /// Enables the kernel debugger.
        /// </summary>
        OsLoaderKernelDebuggerEnabled = 0x260000A0,

        /// <summary>
        /// Causes the kernal to halt early during boot.
        /// </summary>
        OsLoaderDebuggerHalBreakpoint = 0x260000A1,

        /// <summary>
        /// Enables Windows Emergency Management System.
        /// </summary>
        OsLoaderEmsEnabled = 0x260000B0,

        /// <summary>
        /// Forces a failure on boot.
        /// </summary>
        OsLoaderForceFailure = 0x250000C0,

        /// <summary>
        /// The OS failure policy.
        /// </summary>
        OsLoaderDriverLoadFailurePolicy = 0x250000C1,

        /// <summary>
        /// The OS boot status policy.
        /// </summary>
        OsLoaderBootStatusPolicy = 0x250000E0,

        /// <summary>
        /// The device containing the hibernation file.
        /// </summary>
        ResumeHiberFileDevice = 0x21000001,

        /// <summary>
        /// The path to the hibernation file.
        /// </summary>
        ResumeHiberFilePath = 0x22000002,

        /// <summary>
        /// Allows resume loader to use custom settings.
        /// </summary>
        ResumeUseCustomSettings = 0x26000003,

        /// <summary>
        /// PAE settings for resume application.
        /// </summary>
        ResumePaeMode = 0x26000004,

        /// <summary>
        /// An MS-DOS device with containing resume application.
        /// </summary>
        ResumeAssociatedDosDevice = 0x21000005,

        /// <summary>
        /// Enables debug option.
        /// </summary>
        ResumeDebugOptionEnabled = 0x26000006,

        /// <summary>
        /// The number of iterations to run.
        /// </summary>
        MemDiagPassCount = 0x25000001,

        /// <summary>
        /// The test mix.
        /// </summary>
        MemDiagTestMix = 0x25000002,

        /// <summary>
        /// The failure count.
        /// </summary>
        MemDiagFailureCount = 0x25000003,

        /// <summary>
        /// The tests to fail.
        /// </summary>
        MemDiagTestToFail = 0x25000004,

        /// <summary>
        /// BPB string.
        /// </summary>
        LoaderBpbString = 0x22000001,

        /// <summary>
        /// Causes a soft PXE reboot.
        /// </summary>
        StartupPxeSoftReboot = 0x26000001,

        /// <summary>
        /// PXE application name.
        /// </summary>
        StartupPxeApplicationName = 0x22000002,

        /// <summary>
        /// Offset of the RAM disk image.
        /// </summary>
        DeviceRamDiskImageOffset = 0x35000001,

        /// <summary>
        /// Client port for TFTP.
        /// </summary>
        DeviceRamDiskTftpClientPort = 0x35000002,

        /// <summary>
        /// Device containing the SDI file.
        /// </summary>
        DeviceRamDiskSdiDevice = 0x31000003,

        /// <summary>
        /// Path to the SDI file.
        /// </summary>
        DeviceRamDiskSdiPath = 0x32000004,

        /// <summary>
        /// Length of the RAM disk image.
        /// </summary>
        DeviceRamDiskRamDiskImageLength = 0x35000005,

        /// <summary>
        /// Exports the image as a CD.
        /// </summary>
        DeviceRamDiskExportAsCd = 0x36000006,

        /// <summary>
        /// The TFTP transfer block size.
        /// </summary>
        DeviceRamDiskTftpBlockSize = 0x35000007,

        /// <summary>
        /// The device type.
        /// </summary>
        SetupDeviceType = 0x45000001,

        /// <summary>
        /// The application relative path.
        /// </summary>
        SetupAppRelativePath = 0x42000002,

        /// <summary>
        /// The device relative path.
        /// </summary>
        SetupRamDiskDeviceRelativePath = 0x42000003,

        /// <summary>
        /// Omit OS loader elements.
        /// </summary>
        SetupOmitOsLoaderElements = 0x46000004,

        /// <summary>
        /// Recovery OS flag.
        /// </summary>
        SetupRecoveryOs = 0x46000010
    }
}