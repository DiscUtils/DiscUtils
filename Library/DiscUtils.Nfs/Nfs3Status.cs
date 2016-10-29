namespace DiscUtils.Nfs
{
    /// <summary>
    /// NFS status codes.
    /// </summary>
    public enum Nfs3Status
    {
        /// <summary>
        /// Indicates the call completed successfully.
        /// </summary>
        Ok = 0,

        /// <summary>
        /// The operation was not allowed because the caller is either not a
        /// privileged user (root) or not the owner of the target of the operation.
        /// </summary>
        NotOwner = 1,

        /// <summary>
        /// The file or directory name specified does not exist.
        /// </summary>
        NoSuchEntity = 2,

        /// <summary>
        /// A hard error (for example, a disk error) occurred while processing
        /// the requested operation.
        /// </summary>
        IOError = 5,

        /// <summary>
        /// No such device or address.
        /// </summary>
        NoSuchDeviceOrAddress = 6,

        /// <summary>
        /// The caller does not have the correct permission to perform the requested
        /// operation. Contrast this with NotOwner, which restricts itself to owner
        /// or privileged user permission failures.
        /// </summary>
        AccessDenied = 13,

        /// <summary>
        /// The file specified already exists.
        /// </summary>
        FileExists = 17,

        /// <summary>
        /// Attempt to do a cross-device hard link.
        /// </summary>
        AttemptedCrossDeviceHardLink = 18,

        /// <summary>
        /// No such device.
        /// </summary>
        NoSuchDevice = 19,

        /// <summary>
        /// The caller specified a non-directory in a directory operation.
        /// </summary>
        NotDirectory = 20,

        /// <summary>
        /// The caller specified a directory in a non-directory operation.
        /// </summary>
        IsADirectory = 21,

        /// <summary>
        /// Invalid argument or unsupported argument for an operation.
        /// </summary>
        InvalidArgument = 22,

        /// <summary>
        /// The operation would have caused a file to grow beyond the server's
        /// limit.
        /// </summary>
        FileTooLarge = 27,

        /// <summary>
        /// The operation would have caused the server's file system to exceed its
        /// limit.
        /// </summary>
        NoSpaceAvailable = 28,

        /// <summary>
        /// A modifying operation was attempted on a read-only file system.
        /// </summary>
        ReadOnlyFileSystem = 30,

        /// <summary>
        /// Too many hard links.
        /// </summary>
        TooManyHardLinks = 31,

        /// <summary>
        /// The filename in an operation was too long.
        /// </summary>
        NameTooLong = 63,

        /// <summary>
        /// An attempt was made to remove a directory that was not empty.
        /// </summary>
        DirectoryNotEmpty = 66,

        /// <summary>
        /// The user's resource limit on the server has been exceeded.
        /// </summary>
        QuotaHardLimitExceeded = 69,

        /// <summary>
        /// The file referred to no longer exists or access to it has been revoked.
        /// </summary>
        StaleFileHandle = 70,

        /// <summary>
        /// The file handle given in the arguments referred to a file on a non-local
        /// file system on the server.
        /// </summary>
        TooManyRemoteAccessLevels = 71,

        /// <summary>
        /// The file handle failed internal consistency checks.
        /// </summary>
        BadFileHandle = 10001,

        /// <summary>
        /// Update synchronization mismatch was detected during a SETATTR operation.
        /// </summary>
        UpdateSynchronizationError = 10002,

        /// <summary>
        /// Directory enumeration cookie is stale.
        /// </summary>
        StaleCookie = 10003,

        /// <summary>
        /// Operation is not supported.
        /// </summary>
        NotSupported = 10004,

        /// <summary>
        /// Buffer or request is too small.
        /// </summary>
        TooSmall = 10005,

        /// <summary>
        /// An error occurred on the server which does not map to any of the legal NFS
        /// version 3 protocol error values.
        /// </summary>
        ServerFault = 10006,

        /// <summary>
        /// An attempt was made to create an object of a type not supported by the
        /// server.
        /// </summary>
        BadType = 10007,

        /// <summary>
        /// The server initiated the request, but was not able to complete it in a
        /// timely fashion.
        /// </summary>
        /// <remarks>
        /// The client should wait and then try the request with a new RPC transaction ID.
        /// For example, this error should be returned from a server that supports
        /// hierarchical storage and receives a request to process a file that has been
        /// migrated. In this case, the server should start the immigration process and
        /// respond to client with this error.
        /// </remarks>
        SlowJukebox = 10008,

        /// <summary>
        /// An unknown error occured.
        /// </summary>
        Unknown = -1
    }
}