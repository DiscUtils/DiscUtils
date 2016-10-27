#
#  Copyright (c) 2008-2010, Kenneth Bell
#
#  Permission is hereby granted, free of charge, to any person obtaining a
#  copy of this software and associated documentation files (the "Software"),
#  to deal in the Software without restriction, including without limitation
#  the rights to use, copy, modify, merge, publish, distribute, sublicense,
#  and/or sell copies of the Software, and to permit persons to whom the
#  Software is furnished to do so, subject to the following conditions:
#
#  The above copyright notice and this permission notice shall be included in
#  all copies or substantial portions of the Software.
#
#  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
#  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
#  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
#  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
#  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
#  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
#  DEALINGS IN THE SOFTWARE.
#

#
# Test script for .NET DiscUtils
# ------------------------------
#
# This script is a test harness for the PowerShell interface in DiscUtils, and
# also for the NTFS implementation in DiscUtils.  The script performs operations
# on the virtual file system, and periodically runs chkdsk to verify the filesystem
# has not been corrupted.
#

Param([switch]$All, [switch]$Ntfs, [switch]$Cmdlets, [switch]$Registry)


#
#-- Variables
#
$DiscUtilsModule = "c:\work\codeplex\discutils\trunk\utils\DiscUtils.PowerShell\bin\release\discutils.psd1"
$tempdir = "C:\temp\"
$diskfile = "C:\temp\newdisk.vhd"
$regfile = "C:\temp\newreg.hiv"
$testdrive = "Q"
$sig = 0x12345657
$verbose = $false;
$OneMB = $(1024 * 1024)

#
#-- Configure PowerShell
#
$ErrorActionPreference = "Stop"
Import-Module $DiscUtilsModule

#
#-- Define Functions
#
function Trace
{
    param($prefix, $lines)

    if($verbose)
    {
        foreach($line in $lines)
        {
            Write-Host ("$prefix" + ":" + "$line")
        }
    }
}

function AssertTrue
{
    param($value)

    if(-not $value)
    {
        Write-Error "Assert failed.${$MyInvocation}"
    }
}

function AssertInRange
{
    param($lower, $greater, $value)

    if($($lower -gt $value) -or $($greater -le $value))
    {
        Write-Error "Assert failed.  $value not in range ${lower} <= x < ${greater}.${$MyInvocation}"
    }
}

function SafeRemove
{
    param($file)

    if(Test-Path $file)
    {
        Remove-Item $file
    }
}

function InvokeDiskpart
{
    param($script)

    Trace "DISKPART" $script

    $script | out-file -encoding ASCII -filepath c:\temp\diskpartscript.txt
    $output = $(diskpart /s c:\temp\diskpartscript.txt)
    if(-not $?)
    {
        Write-Error "DISKPART script failed: $script"
        Exit
    }
}

function InvokeReg
{
    param($cmd,$p1,$p2)

    Trace "REG" "$cmd $p1 $p2"

    $output = $(reg $cmd $p1 $p2)

    if($LastExitCode -ge 1)
    {
        Write-Error "REG command failed: $cmd"
        Exit
    }
}

function LoadHive
{
    param($hive)

    InvokeReg "load" "HKU\TempHive" "$hive"

    $key = Get-Item "Registry::HKEY_USERS\TempHive"
    if(-not $?)
    {
        Write-Error "Failed to load registry hive $hive"
        Exit
    }

    $key.Close()
}

function UnloadHive
{
    InvokeReg "unload" "HKU\TempHive"
}

function AttachDisk
{
    param($disk, $drive)
    InvokeDiskpart @("select vdisk file=$disk", "attach vdisk")
    sleep -seconds 1
    InvokeDiskpart @("select vdisk file=$disk", "select partition=1", "assign letter=$drive")
}

function DetachDisk
{
    param($disk)
    InvokeDiskpart @("select vdisk file=$disk", "detach vdisk")
}

function RunChkdsk
{
    Trace "CHKDSK" "Checking $diskfile as $testdrive"

    AttachDisk $diskfile $testdrive
    $chkdskOutput = chkdsk ("$testdrive" + ":")
    if(-not $?)
    {
        $chkdskOutput | Write-Warning
        Write-Error "CHKDSK failed!"
        Exit
    }
    
    DetachDisk $diskfile
}

function CheckAdmin
{
    $account = new-object System.Security.Principal.WindowsPrincipal([System.Security.Principal.WindowsIdentity]::GetCurrent())
    if(-not $account.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator))
    {
        Write-Error "This script must be run as an administrator"
        Exit
    }
}

function CreateDisk
{
    param($size)
    New-VirtualDisk $diskfile -Type VHD-dynamic -Size $size | out-null
    New-PSDrive vd -PSProvider VirtualDisk -Root $diskfile -ReadWrite -Scope Global | out-null
    $disk = Get-Item vd:\
    Initialize-VirtualDisk -InputObject $disk -Signature $sig | out-null
    $vol = New-Volume -InputObject $disk -Type WindowsNtfs
    Format-Volume -InputObject $vol -Filesystem Ntfs -Label "foo"
}

function Checkpoint
{
    Remove-PSDrive vd -Scope Global
    RunChkdsk
    New-PSDrive vd -PSProvider VirtualDisk -Root $diskfile -ReadWrite -Scope Global | out-null
}

function CheckpointRegistry
{
    Remove-PSDrive vr

    LoadHive "${tempdir}newreg.hiv"
    UnloadHive "${tempdir}newreg.hiv"

    New-PSDrive vr -PSProvider VirtualRegistry -Root $regfile -Scope Global | out-null
}


#
#-- Main logic (Ntfs)
#
function TestNtfs
{
    $TempMountRoot = ("$testdrive" + ":\")
    $ClusterData = New-Object byte[] 4096
    $FragFileBigBlock = New-Object byte[] $(4 * $OneMB)
    $NumFiles = 500

    # Check we're elevated
    CheckAdmin


    # Clean up from aborted prior runs
    if([System.IO.Directory]::Exists($TempMountRoot))
    {
        "Detaching disk ($TempMountRoot)..."
        DetachDisk $diskfile
    }

    "Creating disk"
    CreateDisk 16MB

    Checkpoint

    "Creating TestFile.txt"
    New-Item -Type file vd:\Volume0\TestFile.txt
    Set-Content vd:\Volume0\TestFile.txt "This is a test file"

    Checkpoint

    "Creating 50 hard links"
    for($i = 0; $i -lt 50; $i++)
    {
        New-Item -type HardLink "vd:\Volume0\hardlink${i}" -SourcePath "vd:\Volume0\TestFile.txt"
    }

    Checkpoint

    "Removing hard links"
    for($i = 0; $i -lt 50; $i++)
    {
        Remove-Item "vd:\Volume0\hardlink${i}"
    }

    Checkpoint

    "Creating file.bin"
    New-Item -type file vd:\Volume0\file.bin
    Set-Content vd:\Volume0\file.bin $ClusterData

    Checkpoint

    "Removing file.bin"
    Remove-Item vd:\Volume0\file.bin

    Checkpoint

    "Creating $($NumFiles * 2) files"
    for($i = 0; $i -lt $NumFiles; $i++)
    {
        # Create some files with common prefix, and some with varying prefixes to
        # exercise directory hash tree a little.
        New-Item -type file "vd:\Volume0\file${i}.bin"
        Set-Content vd:\Volume0\file${i}.bin $ClusterData
        New-Item -type file "vd:\Volume0\${i}file.bin"
        Set-Content vd:\Volume0\${i}file.bin $ClusterData
    }

    Checkpoint

    "Creating files with short names"
    $RootDir = Get-Item 'vd:\Volume0\$Root'
    New-Item -type file "vd:\Volume0\ALongFileName.txt"
    $RootDir.FileSystem.SetShortName("\ALongFileName.txt","SHORT.TXT")

    Checkpoint

    "Creating space for fragmented file"
    #for($i = 0; $i -lt $NumFiles; $i++)
    for($i = 0; $i -lt 100; $i++)
    {
        Remove-Item "vd:\Volume0\file${i}.bin"
    }

    Checkpoint

    "Creating fragmented file"
    New-Item -type file "vd:\Volume0\fragfile.bin"
    Set-Content "vd:\Volume0\fragfile.bin" $FragFileBigBlock
    Add-Content "vd:\Volume0\fragfile.bin" $ClusterData

    Checkpoint

    "Shrinking fragmented file"
    Set-Content "vd:\Volume0\fragfile.bin" $ClusterData

    Checkpoint

    "Removing fragmented file"
    Remove-Item "vd:\Volume0\fragfile.bin"

    Checkpoint

    "Recreating fragmented file"
    New-Item -type file "vd:\Volume0\fragfile.bin"
    Set-Content "vd:\Volume0\fragfile.bin" $FragFileBigBlock
    Add-Content "vd:\Volume0\fragfile.bin" $ClusterData

    Checkpoint

    "Removing fragmented file"
    Remove-Item "vd:\Volume0\fragfile.bin"

    Checkpoint

    "Removing scattered files"
    for($i = 0; $i -lt $NumFiles; $i++)
    {
        if(Test-Path "vd:\Volume0\file${i}.bin")
        {
            Remove-Item "vd:\Volume0\file${i}.bin"
        }

        if(Test-Path "vd:\Volume0\${i}file.bin")
        {
            Remove-Item "vd:\Volume0\${i}file.bin"
        }
    }

    Checkpoint

    "Creating sparse file"
    New-Item -type file "vd:\Volume0\sparsefile.bin"
    Set-Content "vd:\Volume0\sparsefile.bin" $FragFileBigBlock
    $f = Get-Item "vd:\Volume0\sparsefile.bin"
    $f.Attributes = "SparseFile,Archive"
    $s = $f.Open( [System.IO.FileMode]::Open )
    $s.Position = 64 * 1024
    $s.Clear( 128 * 1024 )
    $s.Position = (4 * $OneMB) - (64 * 1024)
    $s.Clear(128 * 1024)
    $s.Dispose()

    Checkpoint

    "Deleting sparse file"
    Remove-Item "vd:\Volume0\sparsefile.bin"

    # Cleanup
    Remove-PSDrive vd
    RunChkdsk
}


#
#-- Main logic (Registry)
#
function TestRegistry
{
    New-VirtualRegistry $regfile
    New-PSDrive vr -PSProvider virtualregistry -Root $regfile

    CheckpointRegistry
}


#
#-- Main logic (Cmdlets)
#
function TestCmdlets
{
    "Checking New-VirtualDisk Cmdlet"

    SafeRemove "${tempdir}newdisk.vmdk"
    New-VirtualDisk "${tempdir}newdisk.vmdk" -Type VMDK-dynamic -Size 10MB | out-null
    AssertTrue $(Test-Path "${tempdir}newdisk.vmdk")
    AssertInRange 1024 $OneMB $(Get-Item "${tempdir}newdisk.vmdk").Length

    SafeRemove "${tempdir}newdisk.vmdk"
    SafeRemove "${tempdir}newdisk-flat.vmdk"
    New-VirtualDisk "${tempdir}newdisk.vmdk" -Type VMDK-fixed -Size 10MB | out-null
    AssertTrue $(Test-Path "${tempdir}newdisk.vmdk")
    AssertInRange 1 $OneMB $(Get-Item "${tempdir}newdisk.vmdk").Length
    AssertInRange $(9 * $OneMB) $(11 * $OneMB) $(Get-Item "${tempdir}newdisk-flat.vmdk").Length

    SafeRemove "${tempdir}newdisk.vhd"
    New-VirtualDisk "${tempdir}newdisk.vhd" -Type VHD-dynamic -Size 10MB | out-null
    AssertTrue $(Test-Path "${tempdir}newdisk.vhd")
    AssertInRange 1024 $OneMB $(Get-Item "${tempdir}newdisk.vhd").Length

    SafeRemove "${tempdir}newdisk.vhd"
    New-VirtualDisk "${tempdir}newdisk.vhd" -Type VHD-fixed -Size 10MB | out-null
    AssertTrue $(Test-Path "${tempdir}newdisk.vhd")
    AssertInRange $(9 * $OneMB) $(11 * $OneMB)  $(Get-Item "${tempdir}newdisk.vhd").Length
}



#
#-- Parameters
#
if($All)
{
    $Ntfs = $true;
    $Cmdlets = $true;
    $Registry = $true;
}

if($Ntfs)
{
    TestNtfs
}

if($Registry)
{
    TestRegistry
}

if($Cmdlets)
{
    TestCmdlets
}
