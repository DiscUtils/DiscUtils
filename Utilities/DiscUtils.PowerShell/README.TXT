.NET DiscUtils PowerShell Module
================================

Copyright (c) 2008-2010, Kenneth Bell.



This folder contains the .NET DiscUtils PowerShell module.  To use this module,
use this command:

  C:\PS> Import-Module DiscUtils.psd1


The module includes two providers, 'virtualdisk' and 'virtualregistry' as well
as a number of commands.


To mount a virtual disk as a PowerShell drive do this:
  C:\PS> New-PSDrive newdrive -PSProvider virtualdisk -Root C:\disk.vhd [-ReadWrite]

To mount a registry hive as a PowerShell drive do this:
  C:\PS> New-PSDrive regdrive -PSProvider virtualregistry -Root newdrive:\Windows\System32\config\SOFTWARE [-ReadWrite]

(in this example, we're mounting a registry hive contained in the virtual
disk)


The virtualdisk and virtualregistry providers are intended to mimic the
built-in FileSystem and Registry providers.


In addition to the providers, these commands are provided:

  New-VirtualDisk
     Creates a new virtual disk file.

  Initialize-VirtualDisk
     Initializes the partition table on a virtual disk.

  New-Volume
     Creates a new volume (aka partition) on a virtual disk.

  Format-Volume
     Formats a volume (aka partition) on a virtual disk.

For more details on these providers, use the Get-Help command.
