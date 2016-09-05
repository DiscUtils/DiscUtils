# Project Description

[![Build status](https://ci.appveyor.com/api/projects/status/s8xnp94r6ywiwpp8?svg=true)](https://ci.appveyor.com/project/qmfrederik/discutils)

DiscUtils is a .NET library to read and write ISO files and Virtual Machine disk files (VHD, VDI, XVA, VMDK, etc). DiscUtils is developed in C# with no native code (or P/Invoke).

# Project Status

Implementation of the ISO, UDF, FAT and NTFS file systems is now fairly stable. VHD, XVA, VMDK and VDI disk formats are implemented, as well as read/write Registry support. The library also includes a simple iSCSI initiator, for accessing disks via iSCSI and an NFS client implementation.

# How to use the Library

Here's a few really simple examples.

## How to create a new ISO:

``` 
CDBuilder builder = new CDBuilder();
builder.UseJoliet = true;
builder.VolumeIdentifier = "A_SAMPLE_DISK";
builder.AddFile(@"Folder\Hello.txt", Encoding.ASCII.GetBytes("Hello World!"));
builder.Build(@"C:\temp\sample.iso");
``` 

You can add files as byte arrays (shown above), as files from the Windows filesystem, or as a Stream. By using a different form of Build, you can get a Stream to the ISO file, rather than writing it to the Windows filesystem.


## How to extract a file from an ISO:

``` 
using (FileStream isoStream = File.Open(@"C:\temp\sample.iso"))
{
  CDReader cd = new CDReader(isoStream, true);
  Stream fileStream = cd.OpenFile(@"Folder\Hello.txt", FileMode.Open);
  // Use fileStream...
}
``` 

You can also browse through the directory hierarchy, starting at cd.Root.

## How to create a virtual hard disk:

``` 
long diskSize = 30 * 1024 * 1024; //30MB
using (Stream vhdStream = File.Create(@"C:\TEMP\mydisk.vhd"))
{
    Disk disk = Disk.InitializeDynamic(vhdStream, diskSize);
    BiosPartitionTable.Initialize(disk, WellKnownPartitionType.WindowsFat);
    using (FatFileSystem fs = FatFileSystem.FormatPartition(disk, 0, null))
    {
        fs.CreateDirectory(@"TestDir\CHILD");
        // do other things with the file system...
    }
}
``` 

As with ISOs, you can browse the file system, starting at fs.Root.


## How to create a virtual floppy disk:

``` 
using (FileStream fs = File.Create(@"myfloppy.vfd"))
{
    using (FatFileSystem floppy = FatFileSystem.FormatFloppy(fs, FloppyDiskType.HighDensity, "MY FLOPPY  "))
    {
        using (Stream s = floppy.OpenFile("foo.txt", FileMode.Create))
        {
            // Use stream...
        }
    }
}
``` 

Again, start browsing the file system at floppy.Root.
