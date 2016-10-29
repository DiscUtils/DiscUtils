using System.IO;

namespace DiscUtils.Vfs
{
    /// <summary>
    /// Delegate for instantiating a file system.
    /// </summary>
    /// <param name="stream">The stream containing the file system.</param>
    /// <param name="volumeInfo">Optional, information about the volume the file system is on.</param>
    /// <param name="parameters">Parameters for the file system.</param>
    /// <returns>A file system implementation.</returns>
    public delegate DiscFileSystem VfsFileSystemOpener(
        Stream stream, VolumeInfo volumeInfo, FileSystemParameters parameters);
}