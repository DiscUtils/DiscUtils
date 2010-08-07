//
// Copyright (c) 2008-2010, Kenneth Bell
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

using System.IO;

namespace DiscUtils.Ntfs
{
    internal delegate File GetFileByIndexFn(long index);
    internal delegate File GetFileByRefFn(FileRecordReference reference);
    internal delegate Directory GetDirectoryByIndexFn(long index);
    internal delegate Directory GetDirectoryByRefFn(FileRecordReference reference);
    internal delegate File AllocateFileFn(FileRecordFlags flags);
    internal delegate void ForgetFileFn(File file);

    internal interface INtfsContext
    {
        Stream RawStream
        {
            get;
        }

        AttributeDefinitions AttributeDefinitions
        {
            get;
        }

        UpperCase UpperCase
        {
            get;
        }

        BiosParameterBlock BiosParameterBlock
        {
            get;
        }

        MasterFileTable Mft
        {
            get;
        }

        ClusterBitmap ClusterBitmap
        {
            get;
        }

        SecurityDescriptors SecurityDescriptors
        {
            get;
        }

        ObjectIds ObjectIds
        {
            get;
        }

        ReparsePoints ReparsePoints
        {
            get;
        }

        Quotas Quotas
        {
            get;
        }

        NtfsOptions Options
        {
            get;
        }

        GetFileByIndexFn GetFileByIndex
        {
            get;
        }

        GetFileByRefFn GetFileByRef
        {
            get;
        }

        GetDirectoryByIndexFn GetDirectoryByIndex
        {
            get;
        }

        GetDirectoryByRefFn GetDirectoryByRef
        {
            get;
        }

        AllocateFileFn AllocateFile
        {
            get;
        }

        ForgetFileFn ForgetFile
        {
            get;
        }

        bool ReadOnly
        {
            get;
        }
    }

    internal sealed class NtfsContext : INtfsContext
    {
        private Stream _rawStream;
        private AttributeDefinitions _attrDefs;
        private UpperCase _upperCase;
        private BiosParameterBlock _bpb;
        private MasterFileTable _mft;
        private ClusterBitmap _bitmap;
        private SecurityDescriptors _securityDescriptors;
        private ObjectIds _objectIds;
        private ReparsePoints _reparsePoints;
        private Quotas _quotas;
        private NtfsOptions _options;
        private GetFileByIndexFn _getFileByIndexFn;
        private GetFileByRefFn _getFileByRefFn;
        private GetDirectoryByIndexFn _getDirByIndexFn;
        private GetDirectoryByRefFn _getDirByRefFn;
        private AllocateFileFn _allocateFileFn;
        private ForgetFileFn _forgetFileFn;
        private bool _readOnly;

        public Stream RawStream
        {
            get { return _rawStream; }
            set { _rawStream = value; }
        }

        public AttributeDefinitions AttributeDefinitions
        {
            get { return _attrDefs; }
            set { _attrDefs = value; }
        }

        public UpperCase UpperCase
        {
            get { return _upperCase; }
            set { _upperCase = value; }
        }

        public BiosParameterBlock BiosParameterBlock
        {
            get { return _bpb; }
            set { _bpb = value; }
        }

        public MasterFileTable Mft
        {
            get { return _mft; }
            set { _mft = value; }
        }

        public ClusterBitmap ClusterBitmap
        {
            get { return _bitmap; }
            set { _bitmap = value; }
        }

        public SecurityDescriptors SecurityDescriptors
        {
            get { return _securityDescriptors; }
            set { _securityDescriptors = value; }
        }

        public ObjectIds ObjectIds
        {
            get { return _objectIds; }
            set { _objectIds = value; }
        }

        public ReparsePoints ReparsePoints
        {
            get { return _reparsePoints; }
            set { _reparsePoints = value; }
        }

        public Quotas Quotas
        {
            get { return _quotas; }
            set { _quotas = value; }
        }

        public NtfsOptions Options
        {
            get { return _options; }
            set { _options = value; }
        }

        public GetFileByIndexFn GetFileByIndex
        {
            get { return _getFileByIndexFn; }
            set { _getFileByIndexFn = value; }
        }

        public GetFileByRefFn GetFileByRef
        {
            get { return _getFileByRefFn; }
            set { _getFileByRefFn = value; }
        }

        public GetDirectoryByIndexFn GetDirectoryByIndex
        {
            get { return _getDirByIndexFn; }
            set { _getDirByIndexFn = value; }
        }

        public GetDirectoryByRefFn GetDirectoryByRef
        {
            get { return _getDirByRefFn; }
            set { _getDirByRefFn = value; }
        }

        public AllocateFileFn AllocateFile
        {
            get { return _allocateFileFn; }
            set { _allocateFileFn = value; }
        }

        public ForgetFileFn ForgetFile
        {
            get { return _forgetFileFn; }
            set { _forgetFileFn = value; }
        }

        public bool ReadOnly
        {
            get { return _readOnly; }
            set { _readOnly = value; }
        }
    }
}
