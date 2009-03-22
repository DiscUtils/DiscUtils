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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace DiscUtils.Diagnostics
{
    /// <summary>
    /// Delegate that represents an individual (replayable) activity.
    /// </summary>
    /// <param name="fs">The file system instance to perform the activity on</param>
    /// <param name="context">Contextual information shared by all activities during a 'run'</param>
    /// <returns>A return value that is made available after the activity is run</returns>
    /// <remarks>The <c>context</c> information is reset (i.e. empty) at the start of a particular
    /// replay.  It's purpose is to enable multiple activites that operate in sequence to co-ordinate.</remarks>
    public delegate object Activity<T>(T fs, Dictionary<string, object> context)
        where T : DiscFileSystem, IDiagnosticTraceable;

    /// <summary>
    /// Class that wraps a DiscFileSystem, validating file system integrity.
    /// </summary>
    /// <typeparam name="T">The concrete type of file system to validate.</typeparam>
    public class ValidatingFileSystem<T> : DiscFileSystem
        where T : DiscFileSystem, IDiagnosticTraceable
    {
        internal delegate Stream StreamOpenFn(T fs);

        private Stream _baseStream;
        private SnapshotStream _snapStream;
        private T _liveTarget;
        private Dictionary<string, object> _activityContext;

        private int _checkpointPeriod = 1;

        /// <summary>
        /// Activities get logged here until a checkpoint is hit, so we can replay between
        /// checkpoints.
        /// </summary>
        private List<Activity<T>> _checkpointBuffer;

        /// <summary>
        /// Flag set when a validation failure is observed, preventing further file system activity.
        /// </summary>
        private bool _lockdown;

        /// <summary>
        /// The exception (if any) that indicated the file system was corrupt.
        /// </summary>
        private Exception _failureException;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="stream">A stream containing an existing (valid) file system.</param>
        /// <remarks>The new instance does not take ownership of the stream.</remarks>
        public ValidatingFileSystem(Stream stream)
        {
            _baseStream = stream;
            _snapStream = new SnapshotStream(stream, Ownership.None);

            _liveTarget = CreateFileSystem(_snapStream);
            _activityContext = new Dictionary<string, object>();

            _checkpointBuffer = new List<Activity<T>>();

            // Take a snapshot, to preserve the stream state before we perform
            // an operation (assumption is that merely creating a file system object
            // (above) is not significant...
            _snapStream.Snapshot();
        }

        /// <summary>
        /// Disposes of this instance, forcing a checkpoint if one is outstanding.
        /// </summary>
        /// <param name="disposing"><c>true</c> if disposing, else <c>false</c></param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Checkpoint();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Gets and sets how often an automatic checkpoint occurs.
        /// </summary>
        /// <remarks>The number here represents the number of distinct file system operations.
        /// Each method/property access on DiscFileSystem or a stream retrieved from DiscFileSystem
        /// counts as an operation.</remarks>
        public int CheckpointInterval
        {
            get { return _checkpointPeriod; }
            set { _checkpointPeriod = value; }
        }

        /// <summary>
        /// Verifies the file system integrity.
        /// </summary>
        /// <remarks>This method may place this object into "lock-down", where no further
        /// changes are permitted (if corruption is detected).  Unlike Checkpoint, this method
        /// doesn't cause the snapshot to be re-taken.</remarks>
        public void Verify()
        {
            _snapStream.Freeze();

            // Note the trace stream means that we can guarantee no further stream access after
            // the file system object is disposed - when we dispose it, it forcibly severes the
            // connection to the snapshot stream.
            using (TracingStream traceStream = new TracingStream(_snapStream, Ownership.None))
            using (T testFs = CreateFileSystem(traceStream))
            {
                try
                {
                    // For now, a diagnostic dump has to pass for validation...
                    using (TextWriter writer = new NullTextWriter())
                    {
                        testFs.Dump(writer, "");
                    }
                }
                catch (Exception e)
                {
                    _lockdown = true;

                    _failureException = e;

                    Debugger.Break();

                    throw new CorruptFileSystemException("File system failed verification", e);
                }
            }

            _snapStream.Thaw();
        }

        /// <summary>
        /// Verifies the file system integrity (as seen on disk), and resets the disk checkpoint.
        /// </summary>
        /// <remarks>This method is automatically invoked according to the CheckpointInterval property,
        /// but can be called manually as well.</remarks>
        public void Checkpoint()
        {
            Verify();

            // Since the file system is OK, reset the snapshot (keeping changes).
            _snapStream.ForgetSnapshot();
            _snapStream.Snapshot();

            _checkpointBuffer.Clear();
        }

        /// <summary>
        /// Generates a diagnostic report by replaying file system activities since the last
        /// checkpoint.
        /// </summary>
        public ReplayReport ReplayFromLastCheckpoint()
        {
            using (SnapshotStream replayCapture = new SnapshotStream(_baseStream, Ownership.None))
            {
                // Preserve the base stream
                replayCapture.Snapshot();

                // Use tracing to capture changes to the stream
                using (TracingStream ts = new TracingStream(replayCapture, Ownership.None))
                using (T replayFs = CreateFileSystem(ts))
                {
                    Dictionary<string, object> replayContext = new Dictionary<string, object>();

                    int replayEventsProcessed = 0;
                    Exception replayException = null;

                    ts.Start();
                    try
                    {
                        for (int i = 0; i < _checkpointBuffer.Count; ++i)
                        {
                            replayEventsProcessed++;
                            object retVal = _checkpointBuffer[i](replayFs, replayContext);
                            Verify();
                        }
                    }
                    catch (Exception e)
                    {
                        replayException = e;

                        // TODO - useful stuff (dump streams, etc).
                        Debugger.Break();

                    }
                    ts.Stop();

                    // TODO - useful stuff (dump streams, etc).
                    Debugger.Break();

                    ReplayReport report = new ReplayReport(
                        _failureException,
                        replayException,
                        ts,
                        _checkpointBuffer.Count,
                        replayEventsProcessed);

                    return report;
                }
            }
        }

        /// <summary>
        /// Used to perform filesystem activities that are exposed in addition to those in the DiscFileSystem class.
        /// </summary>
        /// <param name="activity">The activity to perform, as a delegate</param>
        /// <returns>The value returned from the activity delegate</returns>
        /// <remarks>The supplied activity may be executed multiple times, against multiple instances of the
        /// file system if a replay is requested.  Always drive the file system object supplied as a parameter and
        /// do not persist references to that object.</remarks>
        public object PerformActivity(Activity<T> activity)
        {
            if (_lockdown)
            {
                throw new InvalidOperationException("Validator in lock-down, file system corruption has been detected.");
            }

            _checkpointBuffer.Add(activity);

            bool doCheckpoint = false;
            try
            {
                object retVal = activity(_liveTarget, _activityContext);
                doCheckpoint = true;
                return retVal;
            }
            finally
            {
                // If a checkpoint is due...
                if (_checkpointBuffer.Count >= _checkpointPeriod)
                {
                    // We only do a full checkpoint, if the activity didn't throw an exception.  Otherwise,
                    // we'll discard all replay info just when the caller might want it.  Instead, just do a
                    // verify until (and unless), an activity that doesn't throw an exception happens.
                    if (doCheckpoint)
                    {
                        Checkpoint();
                    }
                    else
                    {
                        Verify();
                    }
                }
            }
        }

        private static T CreateFileSystem(Stream stream)
        {
            return (T)typeof(T).GetConstructor(new Type[] { typeof(Stream) }).Invoke(new object[] { stream });
        }


        #region DiscFileSystem Implementation
        /// <summary>
        /// Provides a friendly description of the file system type.
        /// </summary>
        public override string FriendlyName
        {
            get {
                Activity<T> fn = delegate(T fs, Dictionary<string, object> context)
                {
                    return fs.FriendlyName;
                };

                return (string)PerformActivity(fn);
            }
        }

        /// <summary>
        /// Indicates whether the file system is read-only or read-write.
        /// </summary>
        /// <returns>true if the file system is read-write.</returns>
        public override bool CanWrite
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Gets the root directory of the file system.
        /// </summary>
        public override DiscDirectoryInfo Root
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Copies an existing file to a new file.
        /// </summary>
        /// <param name="sourceFile">The source file</param>
        /// <param name="destinationFile">The destination file</param>
        public override void CopyFile(string sourceFile, string destinationFile)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Copies an existing file to a new file, allowing overwriting of an existing file.
        /// </summary>
        /// <param name="sourceFile">The source file</param>
        /// <param name="destinationFile">The destination file</param>
        /// <param name="overwrite">Whether to permit over-writing of an existing file.</param>
        public override void CopyFile(string sourceFile, string destinationFile, bool overwrite)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a directory.
        /// </summary>
        /// <param name="path">The path of the new directory</param>
        public override void CreateDirectory(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Deletes a directory.
        /// </summary>
        /// <param name="path">The path of the directory to delete.</param>
        public override void DeleteDirectory(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Deletes a file.
        /// </summary>
        /// <param name="path">The path of the file to delete.</param>
        public override void DeleteFile(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Indicates if a directory exists.
        /// </summary>
        /// <param name="path">The path to test</param>
        /// <returns>true if the directory exists</returns>
        public override bool DirectoryExists(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Indicates if a file exists.
        /// </summary>
        /// <param name="path">The path to test</param>
        /// <returns>true if the file exists</returns>
        public override bool FileExists(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Indicates if a file or directory exists.
        /// </summary>
        /// <param name="path">The path to test</param>
        /// <returns>true if the file or directory exists</returns>
        public override bool Exists(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the names of subdirectories in a specified directory.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <returns>Array of directories.</returns>
        public override string[] GetDirectories(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the names of subdirectories in a specified directory matching a specified
        /// search pattern.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <param name="searchPattern">The search string to match against.</param>
        /// <returns>Array of directories matching the search pattern.</returns>
        public override string[] GetDirectories(string path, string searchPattern)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the names of subdirectories in a specified directory matching a specified
        /// search pattern, using a value to determine whether to search subdirectories.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <param name="searchPattern">The search string to match against.</param>
        /// <param name="searchOption">Indicates whether to search subdirectories.</param>
        /// <returns>Array of directories matching the search pattern.</returns>
        public override string[] GetDirectories(string path, string searchPattern, SearchOption searchOption)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the names of files in a specified directory.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <returns>Array of files.</returns>
        public override string[] GetFiles(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the names of files in a specified directory.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <param name="searchPattern">The search string to match against.</param>
        /// <returns>Array of files matching the search pattern.</returns>
        public override string[] GetFiles(string path, string searchPattern)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the names of files in a specified directory matching a specified
        /// search pattern, using a value to determine whether to search subdirectories.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <param name="searchPattern">The search string to match against.</param>
        /// <param name="searchOption">Indicates whether to search subdirectories.</param>
        /// <returns>Array of files matching the search pattern.</returns>
        public override string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the names of all files and subdirectories in a specified directory.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <returns>Array of files and subdirectories matching the search pattern.</returns>
        public override string[] GetFileSystemEntries(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the names of files and subdirectories in a specified directory matching a specified
        /// search pattern.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <param name="searchPattern">The search string to match against.</param>
        /// <returns>Array of files and subdirectories matching the search pattern.</returns>
        public override string[] GetFileSystemEntries(string path, string searchPattern)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Moves a directory.
        /// </summary>
        /// <param name="sourceDirectoryName">The directory to move.</param>
        /// <param name="destinationDirectoryName">The target directory name.</param>
        public override void MoveDirectory(string sourceDirectoryName, string destinationDirectoryName)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Moves a file.
        /// </summary>
        /// <param name="sourceName">The file to move.</param>
        /// <param name="destinationName">The target file name.</param>
        public override void MoveFile(string sourceName, string destinationName)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Moves a file, allowing an existing file to be overwritten.
        /// </summary>
        /// <param name="sourceName">The file to move.</param>
        /// <param name="destinationName">The target file name.</param>
        /// <param name="overwrite">Whether to permit a destination file to be overwritten</param>
        public override void MoveFile(string sourceName, string destinationName, bool overwrite)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Opens the specified file.
        /// </summary>
        /// <param name="path">The full path of the file to open.</param>
        /// <param name="mode">The file mode for the created stream.</param>
        /// <returns>The new stream.</returns>
        public override Stream OpenFile(string path, FileMode mode)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Opens the specified file.
        /// </summary>
        /// <param name="path">The full path of the file to open.</param>
        /// <param name="mode">The file mode for the created stream.</param>
        /// <param name="access">The access permissions for the created stream.</param>
        /// <returns>The new stream.</returns>
        public override Stream OpenFile(string path, FileMode mode, FileAccess access)
        {
            // This delegate can be used at any time the wrapper needs it, if it's in a 'replay' but the real file open isn't.
            StreamOpenFn reopenFn = delegate(T fs)
            {
                return fs.OpenFile(path, mode, access);
            };

            ValidatingFileSystemWrapperStream<T> wrapper = new ValidatingFileSystemWrapperStream<T>(this, reopenFn);

            Activity<T> activity = delegate(T fs, Dictionary<string, object> context)
            {
                Stream s = fs.OpenFile(path, mode, access);
                wrapper.SetNativeStream(context, s);
                return s;
            };

            PerformActivity(activity);

            return wrapper;
        }

        /// <summary>
        /// Gets the attributes of a file or directory.
        /// </summary>
        /// <param name="path">The file or directory to inspect</param>
        /// <returns>The attributes of the file or directory</returns>
        public override FileAttributes GetAttributes(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets the attributes of a file or directory.
        /// </summary>
        /// <param name="path">The file or directory to change</param>
        /// <param name="newValue">The new attributes of the file or directory</param>
        public override void SetAttributes(string path, FileAttributes newValue)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the creation time (in local time) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory</param>
        /// <returns>The creation time.</returns>
        public override DateTime GetCreationTime(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets the creation time (in local time) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <param name="newTime">The new time to set.</param>
        public override void SetCreationTime(string path, DateTime newTime)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the creation time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <returns>The creation time.</returns>
        public override DateTime GetCreationTimeUtc(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets the creation time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <param name="newTime">The new time to set.</param>
        public override void SetCreationTimeUtc(string path, DateTime newTime)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the last access time (in local time) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory</param>
        /// <returns>The last access time</returns>
        public override DateTime GetLastAccessTime(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets the last access time (in local time) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <param name="newTime">The new time to set.</param>
        public override void SetLastAccessTime(string path, DateTime newTime)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the last access time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory</param>
        /// <returns>The last access time</returns>
        public override DateTime GetLastAccessTimeUtc(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets the last access time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <param name="newTime">The new time to set.</param>
        public override void SetLastAccessTimeUtc(string path, DateTime newTime)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the last modification time (in local time) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory</param>
        /// <returns>The last write time</returns>
        public override DateTime GetLastWriteTime(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets the last modification time (in local time) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <param name="newTime">The new time to set.</param>
        public override void SetLastWriteTime(string path, DateTime newTime)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the last modification time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory</param>
        /// <returns>The last write time</returns>
        public override DateTime GetLastWriteTimeUtc(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets the last modification time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <param name="newTime">The new time to set.</param>
        public override void SetLastWriteTimeUtc(string path, DateTime newTime)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the length of a file.
        /// </summary>
        /// <param name="path">The path to the file</param>
        /// <returns>The length in bytes</returns>
        public override long GetFileLength(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets an object representing a possible file.
        /// </summary>
        /// <param name="path">The file path</param>
        /// <returns>The representing object</returns>
        /// <remarks>The file does not need to exist</remarks>
        public override DiscFileInfo GetFileInfo(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets an object representing a possible directory.
        /// </summary>
        /// <param name="path">The directory path</param>
        /// <returns>The representing object</returns>
        /// <remarks>The directory does not need to exist</remarks>
        public override DiscDirectoryInfo GetDirectoryInfo(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets an object representing a possible file system object (file or directory).
        /// </summary>
        /// <param name="path">The file system path</param>
        /// <returns>The representing object</returns>
        /// <remarks>The file system object does not need to exist</remarks>
        public override DiscFileSystemInfo GetFileSystemInfo(string path)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
