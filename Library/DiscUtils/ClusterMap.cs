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

namespace DiscUtils
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Enumeration of possible cluster roles.
    /// </summary>
    /// <remarks>A cluster may be in more than one role.</remarks>
    [Flags]
    public enum ClusterRoles
    {
        /// <summary>
        /// Unknown, or unspecified role.
        /// </summary>
        None     = 0x00,

        /// <summary>
        /// Cluster is free.
        /// </summary>
        Free     = 0x01,

        /// <summary>
        /// Cluster is in use by a normal file.
        /// </summary>
        DataFile = 0x02,

        /// <summary>
        /// Cluster is in use by a system file.
        /// </summary>
        /// <remarks>This isn't a file marked with the 'system' attribute,
        /// rather files that form part of the file system namespace but also
        /// form part of the file system meta-data.</remarks>
        SystemFile = 0x04,

        /// <summary>
        /// Cluster is in use for meta-data.
        /// </summary>
        Metadata = 0x08,

        /// <summary>
        /// Cluster contains the boot region.
        /// </summary>
        BootArea = 0x10,

        /// <summary>
        /// Cluster is marked bad.
        /// </summary>
        Bad      = 0x20,
    }

    /// <summary>
    /// Class that identifies the role of each cluster in a file system.
    /// </summary>
    public sealed class ClusterMap
    {
        private ClusterRoles[] _clusterToRole;
        private object[] _clusterToFileId;
        private Dictionary<object, string[]> _fileIdToPaths;

        internal ClusterMap(ClusterRoles[] clusterToRole, object[] clusterToFileId, Dictionary<object, string[]> fileIdToPaths)
        {
            _clusterToRole = clusterToRole;
            _clusterToFileId = clusterToFileId;
            _fileIdToPaths = fileIdToPaths;
        }

        /// <summary>
        /// Gets the role of a cluster within the file system.
        /// </summary>
        /// <param name="cluster">The cluster to inspect.</param>
        /// <returns>The clusters role (or roles).</returns>
        public ClusterRoles GetRole(long cluster)
        {
            if (_clusterToRole == null || _clusterToRole.Length < cluster)
            {
                return ClusterRoles.None;
            }
            else
            {
                return _clusterToRole[cluster];
            }
        }

        /// <summary>
        /// Converts a cluster to a list of file names.
        /// </summary>
        /// <param name="cluster">The cluster to inspect.</param>
        /// <returns>A list of paths that map to the cluster.</returns>
        /// <remarks>A list is returned because on file systems with the notion of
        /// hard links, a cluster may correspond to multiple directory entries.</remarks>
        public string[] ClusterToPaths(long cluster)
        {
            if ((GetRole(cluster) & (ClusterRoles.DataFile | ClusterRoles.SystemFile)) != 0)
            {
                object fileId = _clusterToFileId[cluster];
                return _fileIdToPaths[fileId];
            }
            else
            {
                return new string[0];
            }
        }
    }
}
