using System.IO;

namespace DiscUtils.Ntfs
{
    internal interface INtfsContext
    {
        Stream RawStream { get; }

        AttributeDefinitions AttributeDefinitions { get; }

        UpperCase UpperCase { get; }

        BiosParameterBlock BiosParameterBlock { get; }

        MasterFileTable Mft { get; }

        ClusterBitmap ClusterBitmap { get; }

        SecurityDescriptors SecurityDescriptors { get; }

        ObjectIds ObjectIds { get; }

        ReparsePoints ReparsePoints { get; }

        Quotas Quotas { get; }

        NtfsOptions Options { get; }

        GetFileByIndexFn GetFileByIndex { get; }

        GetFileByRefFn GetFileByRef { get; }

        GetDirectoryByIndexFn GetDirectoryByIndex { get; }

        GetDirectoryByRefFn GetDirectoryByRef { get; }

        AllocateFileFn AllocateFile { get; }

        ForgetFileFn ForgetFile { get; }

        bool ReadOnly { get; }
    }
}