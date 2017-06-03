using DiscUtils.Streams;

namespace DiscUtils.Vfs
{
    /// <summary>
    /// Interface implemented by classes representing files, in file systems that support multi-stream files.
    /// </summary>
    public interface IVfsFileWithStreams : IVfsFile
    {
        /// <summary>
        /// Creates a new stream.
        /// </summary>
        /// <param name="name">The name of the stream.</param>
        /// <returns>An object representing the stream.</returns>
        SparseStream CreateStream(string name);

        /// <summary>
        /// Opens an existing stream.
        /// </summary>
        /// <param name="name">The name of the stream.</param>
        /// <returns>An object representing the stream.</returns>
        /// <remarks>The implementation must not implicitly create the stream if it doesn't already
        /// exist.</remarks>
        SparseStream OpenExistingStream(string name);
    }
}