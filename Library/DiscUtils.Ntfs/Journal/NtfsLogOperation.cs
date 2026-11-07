//
// Copyright (c) 2026, DiscUtils contributors
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

namespace DiscUtils.Ntfs.Journal;

/// <summary>
/// The operations NTFS writes into its log, as redo and undo actions of a log record.
/// </summary>
internal enum NtfsLogOperation : ushort
{
    Noop = 0x00,
    CompensationLogRecord = 0x01,
    InitializeFileRecordSegment = 0x02,
    DeallocateFileRecordSegment = 0x03,
    WriteEndOfFileRecordSegment = 0x04,
    CreateAttribute = 0x05,
    DeleteAttribute = 0x06,
    UpdateResidentValue = 0x07,
    UpdateNonresidentValue = 0x08,
    UpdateMappingPairs = 0x09,
    DeleteDirtyClusters = 0x0A,
    SetNewAttributeSizes = 0x0B,
    AddIndexEntryRoot = 0x0C,
    DeleteIndexEntryRoot = 0x0D,
    AddIndexEntryAllocation = 0x0E,
    DeleteIndexEntryAllocation = 0x0F,
    WriteEndOfIndexBuffer = 0x10,
    SetIndexEntryVcnRoot = 0x11,
    SetIndexEntryVcnAllocation = 0x12,
    UpdateFileNameRoot = 0x13,
    UpdateFileNameAllocation = 0x14,
    SetBitsInNonresidentBitMap = 0x15,
    ClearBitsInNonresidentBitMap = 0x16,
    HotFix = 0x17,
    EndTopLevelAction = 0x18,
    PrepareTransaction = 0x19,
    CommitTransaction = 0x1A,
    ForgetTransaction = 0x1B,
    OpenNonresidentAttribute = 0x1C,
    OpenAttributeTableDump = 0x1D,
    AttributeNamesDump = 0x1E,
    DirtyPageTableDump = 0x1F,
    TransactionTableDump = 0x20,
    UpdateRecordDataRoot = 0x21,
    UpdateRecordDataAllocation = 0x22,
    UpdateRelativeDataInIndex = 0x23,
    UpdateRelativeDataInIndex2 = 0x24,
    ZeroEndOfFileRecord = 0x25,
}

/// <summary>
/// What kind of on-disk structure an operation changes.
/// </summary>
internal enum NtfsLogTarget
{
    /// <summary>The operation changes nothing on disk (bookkeeping, transaction state, table dumps).</summary>
    None,

    /// <summary>A file record of the master file table.</summary>
    FileRecord,

    /// <summary>An index buffer (INDX) of an $INDEX_ALLOCATION attribute.</summary>
    IndexBuffer,

    /// <summary>The bytes of a non-resident attribute (bitmaps, or any attribute for UpdateNonresidentValue).</summary>
    NonResident,

    /// <summary>An operation whose meaning DiscUtils does not know.</summary>
    Unknown,
}

internal static class NtfsLogOperations
{
    /// <summary>
    /// Gets which structure an operation changes when it is applied as a redo or undo action.
    /// </summary>
    public static NtfsLogTarget TargetOf(NtfsLogOperation op)
    {
        switch (op)
        {
            case NtfsLogOperation.Noop:
            case NtfsLogOperation.CompensationLogRecord:
            case NtfsLogOperation.DeleteDirtyClusters:
            case NtfsLogOperation.HotFix:
            case NtfsLogOperation.EndTopLevelAction:
            case NtfsLogOperation.PrepareTransaction:
            case NtfsLogOperation.CommitTransaction:
            case NtfsLogOperation.ForgetTransaction:
            case NtfsLogOperation.OpenNonresidentAttribute:
            case NtfsLogOperation.OpenAttributeTableDump:
            case NtfsLogOperation.AttributeNamesDump:
            case NtfsLogOperation.DirtyPageTableDump:
            case NtfsLogOperation.TransactionTableDump:
                return NtfsLogTarget.None;

            case NtfsLogOperation.InitializeFileRecordSegment:
            case NtfsLogOperation.DeallocateFileRecordSegment:
            case NtfsLogOperation.WriteEndOfFileRecordSegment:
            case NtfsLogOperation.CreateAttribute:
            case NtfsLogOperation.DeleteAttribute:
            case NtfsLogOperation.UpdateResidentValue:
            case NtfsLogOperation.UpdateMappingPairs:
            case NtfsLogOperation.SetNewAttributeSizes:
            case NtfsLogOperation.AddIndexEntryRoot:
            case NtfsLogOperation.DeleteIndexEntryRoot:
            case NtfsLogOperation.SetIndexEntryVcnRoot:
            case NtfsLogOperation.UpdateFileNameRoot:
            case NtfsLogOperation.UpdateRecordDataRoot:
            case NtfsLogOperation.ZeroEndOfFileRecord:
                return NtfsLogTarget.FileRecord;

            case NtfsLogOperation.AddIndexEntryAllocation:
            case NtfsLogOperation.DeleteIndexEntryAllocation:
            case NtfsLogOperation.WriteEndOfIndexBuffer:
            case NtfsLogOperation.SetIndexEntryVcnAllocation:
            case NtfsLogOperation.UpdateFileNameAllocation:
            case NtfsLogOperation.UpdateRecordDataAllocation:
                return NtfsLogTarget.IndexBuffer;

            case NtfsLogOperation.UpdateNonresidentValue:
            case NtfsLogOperation.SetBitsInNonresidentBitMap:
            case NtfsLogOperation.ClearBitsInNonresidentBitMap:
                return NtfsLogTarget.NonResident;

            default:
                return NtfsLogTarget.Unknown;
        }
    }
}
