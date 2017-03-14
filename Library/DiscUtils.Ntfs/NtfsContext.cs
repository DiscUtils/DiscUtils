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

using System.IO;

namespace DiscUtils.Ntfs
{
    internal sealed class NtfsContext : INtfsContext
    {
        public Stream RawStream { get; set; }

        public AttributeDefinitions AttributeDefinitions { get; set; }

        public UpperCase UpperCase { get; set; }

        public BiosParameterBlock BiosParameterBlock { get; set; }

        public MasterFileTable Mft { get; set; }

        public ClusterBitmap ClusterBitmap { get; set; }

        public SecurityDescriptors SecurityDescriptors { get; set; }

        public ObjectIds ObjectIds { get; set; }

        public ReparsePoints ReparsePoints { get; set; }

        public Quotas Quotas { get; set; }

        public NtfsOptions Options { get; set; }

        public GetFileByIndexFn GetFileByIndex { get; set; }

        public GetFileByRefFn GetFileByRef { get; set; }

        public GetDirectoryByIndexFn GetDirectoryByIndex { get; set; }

        public GetDirectoryByRefFn GetDirectoryByRef { get; set; }

        public AllocateFileFn AllocateFile { get; set; }

        public ForgetFileFn ForgetFile { get; set; }

        public bool ReadOnly { get; set; }
    }
}