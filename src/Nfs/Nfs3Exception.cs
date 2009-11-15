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

using System;
using System.IO;
using System.Runtime.Serialization;

namespace DiscUtils.Nfs
{
    /// <summary>
    /// Exception thrown when some invalid file system data is found, indicating probably corruption.
    /// </summary>
    [Serializable]
    public class Nfs3Exception : IOException
    {

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public Nfs3Exception()
        {
        }

        /// <summary>
        /// Creates a new instance for a server-indicated error
        /// </summary>
        /// <param name="status">The status result of an NFS procedure.</param>
        internal Nfs3Exception(Nfs3Status status)
            : base(GenerateMessage(status))
        {
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public Nfs3Exception(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="message">The exception message</param>
        /// <param name="innerException">The inner exception</param>
        public Nfs3Exception(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Deserializes an instance.
        /// </summary>
        /// <param name="info">The serialization info</param>
        /// <param name="context">The streaming context</param>
        protected Nfs3Exception(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        private static string GenerateMessage(Nfs3Status status)
        {
            switch (status)
            {
                case Nfs3Status.Ok:
                    return "OK";
                case Nfs3Status.NotOwner:
                    return "Not owner";
                case Nfs3Status.NoSuchEntity:
                    return "No such file or directory";
                case Nfs3Status.IoError:
                    return "Hardware I/O error";
                case Nfs3Status.NFS3ERR_NXIO:
                    return "I/O error - no such device or address";
                case Nfs3Status.AccessDenied:
                    return "Permission denied";
                case Nfs3Status.NFS3ERR_EXIST:
                    return "File exists";
                case Nfs3Status.NFS3ERR_XDEV:
                    return "Attempted cross-device hard link";
                case Nfs3Status.NFS3ERR_NODEV:
                    return "No such device";
                case Nfs3Status.NotDirectory:
                    return "Not a directory";
                case Nfs3Status.NFS3ERR_ISDIR:
                    return "Is a directory";
                case Nfs3Status.InvalidArgument:
                    return "Invalid or unsupported argument";
                case Nfs3Status.NFS3ERR_FBIG:
                    return "File too large";
                case Nfs3Status.NFS3ERR_NOSPC:
                    return "No space left on device";
                case Nfs3Status.NFS3ERR_ROFS:
                    return "Read-only file system";
                case Nfs3Status.NFS3ERR_MLINK:
                    return "Too many hard links";
                case Nfs3Status.NameTooLong:
                    return "Name too long";
                case Nfs3Status.NFS3ERR_NOTEMPTY:
                    return "Directory not empty";
                case Nfs3Status.NFS3ERR_DQUOT:
                    return "Quota hard limit exceeded";
                case Nfs3Status.NFS3ERR_STALE:
                    return "Invalid (stale) file handle";
                case Nfs3Status.NFS3ERR_REMOTE:
                    return "Too many levels of remote access";
                case Nfs3Status.NFS3ERR_BADHANDLE:
                    return "Illegal NFS file handle";
                case Nfs3Status.NFS3ERR_NOT_SYNC:
                    return "Update synchronization error";
                case Nfs3Status.NFS3ERR_BAD_COOKIE:
                    return "Read directory cookie stale";
                case Nfs3Status.NotSupported:
                    return "Operation is not supported";
                case Nfs3Status.NFS3ERR_TOOSMALL:
                    return "Buffer or request is too small";
                case Nfs3Status.ServerFault:
                    return "Server fault";
                case Nfs3Status.NFS3ERR_BADTYPE:
                    return "Server doesn't support object type";
                case Nfs3Status.NFS3ERR_JUKEBOX:
                    return "Unable to complete in timely fashion";
                default:
                    return "Unknown error: " + status;
            }
        }
    }
}
