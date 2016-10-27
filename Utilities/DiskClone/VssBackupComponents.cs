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

using System;
using System.Runtime.InteropServices;

namespace DiskClone
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("665c1d5f-c218-414d-a05d-7fef5f9d5c86")]
    public interface IVssBackupComponents
    {
        void GetWriterComponentsCount(out uint pcComponents);

        void GetWriteComponents(uint iWriter, out IntPtr ppWriter);

        void InitializeForBackup(
            [MarshalAs(UnmanagedType.BStr)]string bstrXml
            );

        void SetBackupState(
            bool bSelectComponents,
            bool bBackupBootableSystemState,
            int backupType,
            bool bPartialFileSupport);

        void InitializeForRestore();
        //STDMETHOD(InitializeForRestore)
        //    (
        //    __in BSTR bstrXML
        //    ) = 0;

        void SetRestoreState();
        //// set state describing restore
        //STDMETHOD(SetRestoreState)
        //    (
        //    __in VSS_RESTORE_TYPE restoreType
        //    ) = 0;

        void GatherWriterMetadata(out IVssAsync async);
        //// gather writer metadata
        //STDMETHOD(GatherWriterMetadata)
        //    (
        //    __out IVssAsync **pAsync
        //    ) = 0;

        void GetWriterMetadataCount();
        //// get count of writers with metadata
        //STDMETHOD(GetWriterMetadataCount)
        //    (
        //    __out UINT *pcWriters
        //    ) = 0;

        void GetWriterMetadata();
        //// get writer metadata for a specific writer
        //STDMETHOD(GetWriterMetadata)
        //    (
        //    __in UINT iWriter,
        //    __out VSS_ID *pidInstance,
        //    __out IVssExamineWriterMetadata **ppMetadata
        //    ) = 0;

        void FreeWriterMetadata();
        //// free writer metadata
        //STDMETHOD(FreeWriterMetadata)() = 0;

        void AddComponent();
        //// add a component to the BACKUP_COMPONENTS document
        //STDMETHOD(AddComponent)
        //    (
        //    __in VSS_ID instanceId,
        //    __in VSS_ID writerId,
        //    __in VSS_COMPONENT_TYPE ct,
        //    __in LPCWSTR wszLogicalPath,
        //    __in LPCWSTR wszComponentName
        //    ) = 0;

        void PrepareForBackup(out IVssAsync async);
        //// dispatch PrepareForBackup event to writers
        //STDMETHOD(PrepareForBackup)
        //    (
        //    __out IVssAsync **ppAsync
        //    ) = 0;

        void AbortBackup();
        //// abort the backup
        //STDMETHOD(AbortBackup)() = 0;

        void GatherWriterStatus();
        //// dispatch the Identify event so writers can expose their metadata
        //STDMETHOD(GatherWriterStatus)
        //    (
        //    __out IVssAsync **pAsync
        //    ) = 0;

        void GetWriterStatusCount();
        //// get count of writers with status
        //STDMETHOD(GetWriterStatusCount)
        //    (
        //    __out UINT *pcWriters
        //    ) = 0;

        void FreeWriterStatus();
        //STDMETHOD(FreeWriterStatus)() = 0;

        void GetWriterStatus();
        //STDMETHOD(GetWriterStatus)
        //    (
        //    __in UINT iWriter,
        //    __out VSS_ID *pidInstance,
        //    __out VSS_ID *pidWriter,
        //    __out BSTR *pbstrWriter,
        //    __out VSS_WRITER_STATE *pnStatus,
        //    __out HRESULT *phResultFailure
        //    ) = 0;

        void SetBackupSucceeded();
        //// indicate whether backup succeeded on a component
        //STDMETHOD(SetBackupSucceeded)
        //    (
        //    __in VSS_ID instanceId,
        //    __in VSS_ID writerId,
        //    __in VSS_COMPONENT_TYPE ct,
        //    __in LPCWSTR wszLogicalPath,
        //    __in LPCWSTR wszComponentName,
        //    __in bool bSucceded
        //    ) = 0;

        void SetBackupOptions();
        //// set backup options for the writer
        //STDMETHOD(SetBackupOptions)
        //    (
        //    __in VSS_ID writerId,
        //    __in VSS_COMPONENT_TYPE ct,
        //    __in LPCWSTR wszLogicalPath,
        //    __in LPCWSTR wszComponentName,
        //    __in LPCWSTR wszBackupOptions
        //    ) = 0;

        void SetSelectedForRestore();
        //// indicate that a given component is selected to be restored
        //STDMETHOD(SetSelectedForRestore)
        //    (
        //    __in VSS_ID writerId,
        //    __in VSS_COMPONENT_TYPE ct,
        //    __in LPCWSTR wszLogicalPath,
        //    __in LPCWSTR wszComponentName,
        //    __in bool bSelectedForRestore
        //    ) = 0;

        void SetRestoreOptions();
        //// set restore options for the writer
        //STDMETHOD(SetRestoreOptions)
        //    (
        //    __in VSS_ID writerId,
        //    __in VSS_COMPONENT_TYPE ct,
        //    __in LPCWSTR wszLogicalPath,
        //    __in LPCWSTR wszComponentName,
        //    __in LPCWSTR wszRestoreOptions
        //    ) = 0;

        void SetAdditionalRestores();
        //// indicate that additional restores will follow
        //STDMETHOD(SetAdditionalRestores)
        //    (
        //    __in VSS_ID writerId,
        //    __in VSS_COMPONENT_TYPE ct,
        //    __in LPCWSTR wszLogicalPath,
        //    __in LPCWSTR wszComponentName,
        //    __in bool bAdditionalRestores
        //    ) = 0;


        void SetPreviousBackupStamp();
        //// set the backup stamp that the differential or incremental
        //// backup is based on
        //STDMETHOD(SetPreviousBackupStamp)
        //    (
        //    __in VSS_ID writerId,
        //    __in VSS_COMPONENT_TYPE ct,
        //    __in LPCWSTR wszLogicalPath,
        //    __in LPCWSTR wszComponentName,
        //    __in LPCWSTR wszPreviousBackupStamp
        //    ) = 0;


        void SaveAsXML();
        //// save BACKUP_COMPONENTS document as XML string
        //STDMETHOD(SaveAsXML)
        //    (
        //    __in BSTR *pbstrXML
        //    ) = 0;

        void BackupComplete(out IVssAsync async);
        //// signal BackupComplete event to the writers
        //STDMETHOD(BackupComplete)
        //    (
        //    __out IVssAsync **ppAsync
        //    ) = 0;

        void AddAlternativeLocationMapping();
        //// add an alternate mapping on restore
        //STDMETHOD(AddAlternativeLocationMapping)
        //    (
        //    __in VSS_ID writerId,
        //    __in VSS_COMPONENT_TYPE componentType,
        //    __in LPCWSTR wszLogicalPath,
        //    __in LPCWSTR wszComponentName,
        //    __in LPCWSTR wszPath,
        //    __in LPCWSTR wszFilespec,
        //    __in bool bRecursive,
        //    __in LPCWSTR wszDestination
        //    ) = 0;

        void AddRestoreSubcomponent();
        //// add a subcomponent to be restored
        //STDMETHOD(AddRestoreSubcomponent)
        //    (
        //    __in VSS_ID writerId,
        //    __in VSS_COMPONENT_TYPE componentType,
        //    __in LPCWSTR wszLogicalPath,
        //    __in LPCWSTR wszComponentName,
        //    __in LPCWSTR wszSubComponentLogicalPath,
        //    __in LPCWSTR wszSubComponentName,
        //    __in bool bRepair
        //    ) = 0;

        void SetFileRestoreStatus();
        //// requestor indicates whether files were successfully restored
        //STDMETHOD(SetFileRestoreStatus)
        //    (
        //    __in VSS_ID writerId,
        //    __in VSS_COMPONENT_TYPE ct,
        //    __in LPCWSTR wszLogicalPath,
        //    __in LPCWSTR wszComponentName,
        //    __in VSS_FILE_RESTORE_STATUS status
        //    ) = 0;

        void AddNewTarget();
        //// add a new location target for a file to be restored
        //STDMETHOD(AddNewTarget)
        //    (
        //    __in VSS_ID writerId,
        //    __in VSS_COMPONENT_TYPE ct,
        //    __in LPCWSTR wszLogicalPath,
        //    __in LPCWSTR wszComponentName,
        //    __in LPCWSTR wszPath,
        //    __in LPCWSTR wszFileName, 
        //    __in bool bRecursive,
        //    __in LPCWSTR wszAlternatePath
        //    ) = 0;

        void SetRangesFilePath();
        //// add a new location for the ranges file in case it was restored to
        //// a different location
        //STDMETHOD(SetRangesFilePath)
        //    (
        //    __in VSS_ID writerId,
        //    __in VSS_COMPONENT_TYPE ct,
        //    __in LPCWSTR wszLogicalPath,
        //    __in LPCWSTR wszComponentName, 
        //    __in UINT iPartialFile,
        //    __in LPCWSTR wszRangesFile
        //    ) = 0;

        void PreRestore();
        //// signal PreRestore event to the writers
        //STDMETHOD(PreRestore)
        //    (
        //    __out IVssAsync **ppAsync
        //    ) = 0;

        void PostRestore();
        //// signal PostRestore event to the writers
        //STDMETHOD(PostRestore)
        //    (
        //    __out IVssAsync **ppAsync
        //    ) = 0;

        void SetContext(int context);
        //// Called to set the context for subsequent snapshot-related operations
        //STDMETHOD(SetContext)
        //    (
        //    __in LONG lContext
        //    ) = 0;

        void StartSnapshotSet(out Guid snapshotSetId);
        //// start a snapshot set
        //STDMETHOD(StartSnapshotSet)
        //    (
        //    __out VSS_ID *pSnapshotSetId
        //    ) = 0;

        void AddToSnapshotSet(
            string pwszVolumeName,
            Guid providerId,
            out Guid snapShotId
            );

        void DoSnapshotSet(out IVssAsync async);

        void DeleteSnapshots(
            Guid sourceObjectId,
            int eSourceObjectType,
            [MarshalAs(UnmanagedType.Bool)]bool bForceDelete,
            out long plDeletedSnapshots,
            out Guid pNondeletedSnapshotId
            );

        void ImportSnapshots(out IVssAsync async);

        void BreakSnapshotSet(Guid snapshotSetId);

        void GetSnapshotProperties(
            Guid snapshotId,
            IntPtr pProperties);

        //STDMETHOD(Query)
        //    (
        //    __in VSS_ID        QueriedObjectId,
        //    __in VSS_OBJECT_TYPE    eQueriedObjectType,
        //    __in VSS_OBJECT_TYPE    eReturnedObjectsType,
        //    __in IVssEnumObject     **ppEnum
        //    ) = 0;

        //STDMETHOD(IsVolumeSupported)
        //    (
        //    __in VSS_ID ProviderId,
        //    __in_z VSS_PWSZ pwszVolumeName,
        //    __in BOOL * pbSupportedByThisProvider
        //    ) = 0;

        //STDMETHOD(DisableWriterClasses)
        //    (
        //    __in const VSS_ID *rgWriterClassId,
        //    __in UINT cClassId
        //    ) = 0;

        //STDMETHOD(EnableWriterClasses)
        //    (
        //    __in const VSS_ID *rgWriterClassId,
        //    __in UINT cClassId
        //    ) = 0;

        //STDMETHOD(DisableWriterInstances)
        //    (
        //    __in const VSS_ID *rgWriterInstanceId,
        //    __in UINT cInstanceId
        //    ) = 0;

        //// called to expose a snapshot
        //STDMETHOD(ExposeSnapshot)
        //    (
        //    __in VSS_ID SnapshotId,
        //    __in_z VSS_PWSZ wszPathFromRoot,
        //    __in LONG lAttributes,
        //    __in_z VSS_PWSZ wszExpose,
        //    __out_z VSS_PWSZ *pwszExposed
        //    ) = 0;

        //STDMETHOD(RevertToSnapshot)
        //    (
        //    __in VSS_ID SnapshotId,
        //    __in BOOL bForceDismount
        //    ) = 0;

        //STDMETHOD(QueryRevertStatus)
        //    (
        //    __in_z VSS_PWSZ pwszVolume,
        //    __out IVssAsync **ppAsync
        //    ) = 0;

    }
}
