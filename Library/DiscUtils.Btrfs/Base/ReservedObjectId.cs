//
// Copyright (c) 2017, Bianco Veigel
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

namespace DiscUtils.Btrfs.Base
{
    /// <summary>
    /// All root objectids between FIRST_FREE = 256ULL and LAST_FREE = -256ULL refer to file trees.
    /// </summary>
    internal enum ReservedObjectId : ulong
    {
        /// <summary>
        /// The object id that refers to the ROOT_TREE itself
        /// </summary>
        RootTree = 1,

        /// <summary>
        /// The objectid that refers to the EXTENT_TREE
        /// </summary>
        ExtentTree = 2,
            
        /// <summary>
        /// The objectid that refers to the root of the CHUNK_TREE
        /// </summary>
        ChunkTree = 3,
        
        /// <summary>
        /// The objectid that refers to the root of the DEV_TREE
        /// </summary>
        DevTree = 4,
        
        /// <summary>
        /// The objectid that refers to the global FS_TREE root
        /// </summary>
        FsTree = 5,
        
        /// <summary>
        /// The objectid that refers to the CSUM_TREE
        /// </summary>
        CsumTree = 7,
        
        /// <summary>
        /// The objectid that refers to the QUOTA_TREE
        /// </summary>
        QuotaTree = 8,
        
        /// <summary>
        /// The objectid that refers to the UUID_TREE
        /// </summary>
        UuidTree = 9,
        
        /// <summary>
        /// The objectid that refers to the FREE_SPACE_TREE
        /// </summary>
        FreeSpaceTree = 10,
        
        /// <summary>
        /// The objectid that refers to the TREE_LOG tree
        /// </summary>
        TreeLog = UInt64.MaxValue - 7UL,
        
        /// <summary>
        /// The objectid that refers to the TREE_RELOC tree
        /// </summary>
        TreeReloc = UInt64.MaxValue-8UL,
        
        /// <summary>
        /// The objectid that refers to the DATA_RELOC tree
        /// </summary>
        DataRelocTree = UInt64.MaxValue - 9UL,
        
        /// <summary>
        /// The objectid that refers to the directory within the root tree. 
        ///  </summary>
        /// <remarks>
        /// If it exists, it will have the usual items used to implement a directory associated with it
        /// There will only be a single entry called default that points to a key to be used as the root directory on the file system instead of the FS_TREE
        /// </remarks>
        RootTreeDir = 6,
        
        /// <summary>
        /// The objectid used for orphan root tracking
        /// </summary>
        Orphan = UInt64.MaxValue - 5UL,
        
        CsumItem = UInt64.MaxValue - 10UL,

        /// <summary>
        /// This objectid indicates the first available objectid in this CHUNK_TREE.
        /// In practice, it is the only objectid used in the tree.
        /// The offset field of the key is the only component used to distinguish separate CHUNK_ITEM items. 
        /// </summary>
        FirstChunkTree = 256,
    }
}
