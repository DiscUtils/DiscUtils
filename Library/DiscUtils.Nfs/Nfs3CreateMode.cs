//
// Copyright (c) 2017, Quamotion
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
namespace DiscUtils.Nfs
{
    public enum Nfs3CreateMode
    {
        /// <summary>
        /// UNCHECKED
        /// means that the file should be created without checking
        /// for the existence of a duplicate file in the same
        /// directory. In this case, how.obj_attributes is a sattr3
        /// describing the initial attributes for the file.
        /// </summary>
        Unchecked = 0,

        /// <summary>
        /// GUARDED
        /// specifies that the server should check for the presence
        /// of a duplicate file before performing the create and
        /// should fail the request with NFS3ERR_EXIST if a
        /// duplicate file exists. If the file does not exist, the
        /// request is performed as described for UNCHECKED.
        /// </summary>
        Guarded = 1,

        /// <summary>
        /// EXCLUSIVE specifies that the server is to follow
        /// exclusive creation semantics, using the verifier to
        /// ensure exclusive creation of the target. No attributes
        /// may be provided in this case, since the server may use
        /// the target file metadata to store the createverf3
        /// verifier.
        /// </summary>
        Exclusive = 2
    }
}
