namespace DiscUtils.Udf
{
    internal enum ShortAllocationFlags
    {
        RecordedAndAllocated = 0,
        AllocatedNotRecorded = 1,
        NotRecordedNotAllocated = 2,
        NextExtentOfAllocationDescriptors = 3
    }
}