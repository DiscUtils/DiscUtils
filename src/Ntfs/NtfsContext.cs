//
// Copyright (c) 2008-2009, Kenneth Bell
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
    internal delegate File GetFileByRefFn(FileReference reference);
    internal delegate Directory GetDirectoryByIndexFn(long index);
    internal delegate Directory GetDirectoryByRefFn(FileReference reference);
    internal delegate File AllocateFileFn(FileRecordFlags flags);

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

        NtfsOptions Options
        {
            get;
        }

        GetFileByIndexFn GetFile
        {
            get;
        }

        GetDirectoryByIndexFn GetDirectoryByIndex
        {
            get;
        }

        GetDirectoryByRefFn GetDirectory
        {
            get;
        }

        AllocateFileFn AllocateFile
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
        private NtfsOptions _options;
        private GetFileByIndexFn _getFileByIndexFn;
        private GetDirectoryByIndexFn _getDirByIndexFn;
        private GetDirectoryByRefFn _getDirByRefFn;
        private AllocateFileFn _allocateFileFn;

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

        public NtfsOptions Options
        {
            get { return _options; }
            set { _options = value; }
        }

        public GetFileByIndexFn GetFile
        {
            get { return _getFileByIndexFn; }
            set { _getFileByIndexFn = value; }
        }

        public GetDirectoryByIndexFn GetDirectoryByIndex
        {
            get { return _getDirByIndexFn; }
            set { _getDirByIndexFn = value; }
        }

        public GetDirectoryByRefFn GetDirectory
        {
            get { return _getDirByRefFn; }
            set { _getDirByRefFn = value; }
        }

        public AllocateFileFn AllocateFile
        {
            get { return _allocateFileFn; }
            set { _allocateFileFn = value; }
        }
    }
}
