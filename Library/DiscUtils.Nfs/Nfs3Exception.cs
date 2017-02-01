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
using System.IO;

#if !NETCORE
using System.Runtime.Serialization;
using System.Security.Permissions;
#endif

namespace DiscUtils.Nfs
{
    /// <summary>
    /// Exception thrown when some invalid file system data is found, indicating probably corruption.
    /// </summary>
#if !NETCORE
    [Serializable]
#endif
    public sealed class Nfs3Exception : IOException
    {
        /// <summary>
        /// Initializes a new instance of the Nfs3Exception class.
        /// </summary>
        public Nfs3Exception() {}

        /// <summary>
        /// Initializes a new instance of the Nfs3Exception class.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public Nfs3Exception(string message)
            : base(message) {}

        /// <summary>
        /// Initializes a new instance of the Nfs3Exception class.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="status">The status result of an NFS procedure.</param>
        public Nfs3Exception(string message, Nfs3Status status)
            : base(message)
        {
            NfsStatus = status;
        }

        /// <summary>
        /// Initializes a new instance of the Nfs3Exception class.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="innerException">The inner exception.</param>
        public Nfs3Exception(string message, Exception innerException)
            : base(message, innerException) {}

        /// <summary>
        /// Initializes a new instance of the Nfs3Exception class.
        /// </summary>
        /// <param name="status">The status result of an NFS procedure.</param>
        internal Nfs3Exception(Nfs3Status status)
            : base(GenerateMessage(status))
        {
            NfsStatus = status;
        }

#if !NETCORE
        /// <summary>
        /// Initializes a new instance of the Nfs3Exception class.
        /// </summary>
        /// <param name="info">The serialization info.</param>
        /// <param name="context">The streaming context.</param>
        private Nfs3Exception(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            NfsStatus = (Nfs3Status)info.GetInt32("Status");
        }
#endif

        /// <summary>
        /// Gets the NFS status code that lead to the exception.
        /// </summary>
        public Nfs3Status NfsStatus { get; } = Nfs3Status.Unknown;

#if !NETCORE
        /// <summary>
        /// Serializes this exception.
        /// </summary>
        /// <param name="info">The object to populate with serialized data.</param>
        /// <param name="context">The context for this serialization.</param>
        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Status", (int)NfsStatus);
            base.GetObjectData(info, context);
        }
#endif

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
                case Nfs3Status.IOError:
                    return "Hardware I/O error";
                case Nfs3Status.NoSuchDeviceOrAddress:
                    return "I/O error - no such device or address";
                case Nfs3Status.AccessDenied:
                    return "Permission denied";
                case Nfs3Status.FileExists:
                    return "File exists";
                case Nfs3Status.AttemptedCrossDeviceHardLink:
                    return "Attempted cross-device hard link";
                case Nfs3Status.NoSuchDevice:
                    return "No such device";
                case Nfs3Status.NotDirectory:
                    return "Not a directory";
                case Nfs3Status.IsADirectory:
                    return "Is a directory";
                case Nfs3Status.InvalidArgument:
                    return "Invalid or unsupported argument";
                case Nfs3Status.FileTooLarge:
                    return "File too large";
                case Nfs3Status.NoSpaceAvailable:
                    return "No space left on device";
                case Nfs3Status.ReadOnlyFileSystem:
                    return "Read-only file system";
                case Nfs3Status.TooManyHardLinks:
                    return "Too many hard links";
                case Nfs3Status.NameTooLong:
                    return "Name too long";
                case Nfs3Status.DirectoryNotEmpty:
                    return "Directory not empty";
                case Nfs3Status.QuotaHardLimitExceeded:
                    return "Quota hard limit exceeded";
                case Nfs3Status.StaleFileHandle:
                    return "Invalid (stale) file handle";
                case Nfs3Status.TooManyRemoteAccessLevels:
                    return "Too many levels of remote access";
                case Nfs3Status.BadFileHandle:
                    return "Illegal NFS file handle";
                case Nfs3Status.UpdateSynchronizationError:
                    return "Update synchronization error";
                case Nfs3Status.StaleCookie:
                    return "Read directory cookie stale";
                case Nfs3Status.NotSupported:
                    return "Operation is not supported";
                case Nfs3Status.TooSmall:
                    return "Buffer or request is too small";
                case Nfs3Status.ServerFault:
                    return "Server fault";
                case Nfs3Status.BadType:
                    return "Server doesn't support object type";
                case Nfs3Status.SlowJukebox:
                    return "Unable to complete in timely fashion";
                default:
                    return "Unknown error: " + status;
            }
        }
    }
}