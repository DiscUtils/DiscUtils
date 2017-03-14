namespace DiscUtils.Udf
{
    internal enum TagIdentifier : ushort
    {
        None = 0,
        PrimaryVolumeDescriptor = 1,
        AnchorVolumeDescriptorPointer = 2,
        VolumeDescriptorPointer = 3,
        ImplementationUseVolumeDescriptor = 4,
        PartitionDescriptor = 5,
        LogicalVolumeDescriptor = 6,
        UnallocatedSpaceDescriptor = 7,
        TerminatingDescriptor = 8,
        LogicalVolumeIntegrityDescriptor = 9,

        FileSetDescriptor = 256,
        FileIdentifierDescriptor = 257,
        AllocationExtentDescriptor = 258,
        IndirectEntry = 259,
        TerminalEntry = 260,
        FileEntry = 261,
        ExtendedAttributeHeaderDescriptor = 262,
        UnallocatedSpaceEntry = 263,
        SpaceBitmapDescriptor = 264,
        PartitionIntegrityEntry = 265,
        ExtendedFileEntry = 266
    }
}