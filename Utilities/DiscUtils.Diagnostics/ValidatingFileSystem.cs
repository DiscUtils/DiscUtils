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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using DiscUtils.Streams;

namespace DiscUtils.Diagnostics
{
    /// <summary>
    /// Delegate that represents an individual (replayable) activity.
    /// </summary>
    /// <param name="fs">The file system instance to perform the activity on</param>
    /// <param name="context">Contextual information shared by all activities during a 'run'</param>
    /// <typeparam name="TFileSystem">The concrete type of the file system the action is performed on.</typeparam>
    /// <returns>A return value that is made available after the activity is run</returns>
    /// <remarks>The <c>context</c> information is reset (i.e. empty) at the start of a particular
    /// replay.  It's purpose is to enable multiple activites that operate in sequence to co-ordinate.</remarks>
    public delegate object Activity<TFileSystem>(TFileSystem fs, Dictionary<string, object> context)
        where TFileSystem : DiscFileSystem, IDiagnosticTraceable;

    /// <summary>
    /// Enumeration of stream views that can be requested.
    /// </summary>
    public enum StreamView
    {
        /// <summary>
        /// The current state of the stream under test.
        /// </summary>
        Current = 0,

        /// <summary>
        /// The state of the stream at the last good checkpoint.
        /// </summary>
        LastCheckpoint = 1
    }

    /// <summary>
    /// Class that wraps a DiscFileSystem, validating file system integrity.
    /// </summary>
    /// <typeparam name="TFileSystem">The concrete type of file system to validate.</typeparam>
    /// <typeparam name="TChecker">The concrete type of the file system checker.</typeparam>
    public class ValidatingFileSystem<TFileSystem, TChecker> : DiscFileSystem
        where TFileSystem : DiscFileSystem, IDiagnosticTraceable
        where TChecker : DiscFileSystemChecker
    {
        internal delegate SparseStream StreamOpenFn(TFileSystem fs);

        private Stream _baseStream;

        //-------------------------------------
        // CONFIG

        /// <summary>
        /// How often a check point is run (in number of 'activities').
        /// </summary>
        private int _checkpointPeriod = 1;

        /// <summary>
        /// Indicates if a read/write trace should run all the time.
        /// </summary>
        private bool _runGlobalTrace = false;

        /// <summary>
        /// Indicates whether to capture full stack traces when doing a global trace.
        /// </summary>
        private bool _globalTraceCaptureStackTraces = false;


        //-------------------------------------
        // INITIALIZED STATE

        private SnapshotStream _snapStream;
        private TFileSystem _liveTarget;
        private bool _initialized;
        private Dictionary<string, object> _activityContext;
        private TracingStream _globalTrace;

        /// <summary>
        /// The random number generator used to generate seeds for checkpoint-specific generators.
        /// </summary>
        private Random _masterRng;



        //-------------------------------------
        // RUNNING STATE

        /// <summary>
        /// Activities get logged here until a checkpoint is hit, so we can replay between
        /// checkpoints.
        /// </summary>
        private List<Activity<TFileSystem>> _checkpointBuffer;

        /// <summary>
        /// The random number generator seed value (set at checkpoint).
        /// </summary>
        private int _checkpointRngSeed;

        /// <summary>
        /// The last verification report generated at a scheduled checkpoint.
        /// </summary>
        private string _lastCheckpointReport;

        /// <summary>
        /// Flag set when a validation failure is observed, preventing further file system activity.
        /// </summary>
        private bool _lockdown;

        /// <summary>
        /// The exception (if any) that indicated the file system was corrupt.
        /// </summary>
        private Exception _failureException;

        /// <summary>
        /// The total number of events carried out before lock-down occured.
        /// </summary>
        private long _totalEventsBeforeLockDown;



        private int _numScheduledCheckpoints;


        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="stream">A stream containing an existing (valid) file system.</param>
        /// <remarks>The new instance does not take ownership of the stream.</remarks>
        public ValidatingFileSystem(Stream stream)
        {
            _baseStream = stream;
        }

        /// <summary>
        /// Disposes of this instance, forcing a checkpoint if one is outstanding.
        /// </summary>
        /// <param name="disposing"><c>true</c> if disposing, else <c>false</c></param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    CheckpointAndThrow();
                }
                finally
                {
                    if (_globalTrace != null)
                    {
                        _globalTrace.Dispose();
                    }
                }
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
        /// Gets and sets whether an inter-checkpoint trace should be run (useful for non-reproducible failures).
        /// </summary>
        public bool RunGlobalIOTrace
        {
            get { return _runGlobalTrace; }
            set { _runGlobalTrace = value; }
        }

        /// <summary>
        /// Gets and sets whether a global I/O trace should be run (useful for non-reproducible failures).
        /// </summary>
        public bool GlobalIOTraceCapturesStackTraces
        {
            get { return _globalTraceCaptureStackTraces; }
            set { _globalTraceCaptureStackTraces = value; }
        }

        /// <summary>
        /// Gets access to a view of the stream being validated, forcing 'lock-down'.
        /// </summary>
        /// <param name="view">The view to open.</param>
        /// <param name="readOnly">Whether to fail changes to the stream.</param>
        /// <returns>The new stream, the caller must dispose.</returns>
        /// <remarks>Always use this method to access the stream, rather than keeping
        /// a reference to the stream passed to the constructor.  This method never
        /// lets changes through to the underlying stream, so ensures the integrity
        /// of the underlying stream.  Any changes made to the returned stream are held
        /// as a private delta and discarded when the stream is disposed.</remarks>
        public Stream OpenStreamView(StreamView view, bool readOnly)
        {
            // Prevent further changes.
            _lockdown = true;

            Stream s;

            // Perversely, the snap stream has the current view (squirrelled away in it's
            // delta).  The base stream is actually the stream state back at the last checkpoint.
            if (view == StreamView.Current)
            {
                s = _snapStream;
            }
            else
            {
                s = _baseStream;
            }

            // Return a protective wrapping stream, so the original stream is preserved.
            SnapshotStream snapStream = new SnapshotStream(s, Ownership.None);
            snapStream.Snapshot();
            if (readOnly)
            {
                snapStream.Freeze();
            }
            return snapStream;
        }

        /// <summary>
        /// Verifies the file system integrity.
        /// </summary>
        /// <param name="reportOutput">The destination for the verification report, or <c>null</c></param>
        /// <param name="levels">The amount of detail to include in the report (if not <c>null</c>)</param>
        /// <returns><c>true</c> if the file system is OK, else <c>false</c>.</returns>
        /// <remarks>This method may place this object into "lock-down", where no further
        /// changes are permitted (if corruption is detected).  Unlike Checkpoint, this method
        /// doesn't cause the snapshot to be re-taken.</remarks>
        public bool Verify(TextWriter reportOutput, ReportLevels levels)
        {
            bool ok = true;
            _snapStream.Freeze();

            // Note the trace stream means that we can guarantee no further stream access after
            // the file system object is disposed - when we dispose it, it forcibly severes the
            // connection to the snapshot stream.
            using (TracingStream traceStream = new TracingStream(_snapStream, Ownership.None))
            {
                try
                {
                    if (!DoVerify(traceStream, reportOutput, levels))
                    {
                        ok = false;
                    }
                }
                catch (Exception e)
                {
                    _failureException = e;
                    ok = false;
                }
            }

            if (ok)
            {
                _snapStream.Thaw();
                return true;
            }
            else
            {
                _lockdown = true;
                if (_runGlobalTrace)
                {
                    _globalTrace.Stop();
                    _globalTrace.WriteToFile(null);
                }
                return false;
            }
        }

        /// <summary>
        /// Verifies the file system integrity (as seen on disk), and resets the disk checkpoint.
        /// </summary>
        /// <param name="reportOutput">The destination for the verification report, or <c>null</c></param>
        /// <param name="levels">The amount of detail to include in the report (if not <c>null</c>)</param>
        /// <remarks>This method is automatically invoked according to the CheckpointInterval property,
        /// but can be called manually as well.</remarks>
        public bool Checkpoint(TextWriter reportOutput, ReportLevels levels)
        {
            if (!Verify(reportOutput, levels))
            {
                return false;
            }

            // Since the file system is OK, reset the snapshot (keeping changes).
            _snapStream.ForgetSnapshot();
            _snapStream.Snapshot();

            _checkpointBuffer.Clear();

            // Set the file system's RNG to a known, but unpredictable, state.
            _checkpointRngSeed = _masterRng.Next();
            _liveTarget.Options.RandomNumberGenerator = new Random(_checkpointRngSeed);

            // Reset the global trace stream - no longer interested in what it captured.
            if (_runGlobalTrace)
            {
                _globalTrace.Reset(_runGlobalTrace);
            }

            return true;
        }

        /// <summary>
        /// Generates a diagnostic report by replaying file system activities since the last
        /// checkpoint.
        /// </summary>
        public ReplayReport ReplayFromLastCheckpoint()
        {
            if (!DoReplayAndVerify(0))
            {
                throw new ValidatingFileSystemException("Previous checkpoint now shows as invalid, the underlying storage stream may be broken");
            }

            // TODO - do full replay, check for failure - is this reproducible?

            // Binary chop for activity that causes failure
            int lowPoint = 0;
            int highPoint = _checkpointBuffer.Count;

            int midPoint = highPoint / 2;
            while (highPoint - lowPoint > 1)
            {
                if (DoReplayAndVerify(midPoint))
                {
                    // This was OK, so must be mid-point or higher
                    lowPoint = midPoint;
                }
                else
                {
                    // Failed, so must be below mid-point
                    highPoint = midPoint;
                }
                midPoint = lowPoint + ((highPoint - lowPoint) / 2);
            }

            // Replay again, up to lowPoint - capturing all info desired

            using (SnapshotStream replayCapture = new SnapshotStream(_baseStream, Ownership.None))
            {
                // Preserve the base stream
                replayCapture.Snapshot();

                // Use tracing to capture changes to the stream
                using (TracingStream ts = new TracingStream(replayCapture, Ownership.None))
                {
                    Exception replayException = null;

                    StringWriter preVerificationReport = new StringWriter(CultureInfo.InvariantCulture);

                    try
                    {
                        using (TFileSystem replayFs = CreateFileSystem(ts))
                        {
                            // Re-init the RNG to it's state when the checkpoint started, so we get reproducibility.
                            replayFs.Options.RandomNumberGenerator = new Random(_checkpointRngSeed);

                            Dictionary<string, object> replayContext = new Dictionary<string, object>();

                            for (int i = 0; i < lowPoint - 1; ++i)
                            {
                                _checkpointBuffer[i](replayFs, replayContext);
                            }

                            DoVerify(ts, preVerificationReport, ReportLevels.All);

                            ts.CaptureStackTraces = true;
                            ts.Start();
                            _checkpointBuffer[lowPoint](replayFs, replayContext);
                            ts.Stop();
                        }

                    }
                    catch (Exception e)
                    {
                        replayException = e;
                    }

                    StringWriter verificationReport = new StringWriter(CultureInfo.InvariantCulture);
                    bool failedVerificationOnReplay = DoVerify(ts, verificationReport, ReportLevels.All);

                    return new ReplayReport(
                        _failureException,
                        replayException,
                        _globalTrace,
                        ts,
                        _checkpointBuffer.Count,
                        lowPoint + 1,
                        _totalEventsBeforeLockDown,
                        preVerificationReport.GetStringBuilder().ToString(),
                        failedVerificationOnReplay,
                        verificationReport.GetStringBuilder().ToString(),
                        _lastCheckpointReport);
                }
            }
        }

        /// <summary>
        /// Indicates if we're in lock-down (i.e. corruption has been detected).
        /// </summary>
        internal bool InLockdown
        {
            get { return _lockdown; }
        }

        /// <summary>
        /// Replays a specified number of activities.
        /// </summary>
        /// <param name="activityCount">Number of activities to replay</param>
        private bool DoReplayAndVerify(int activityCount)
        {
            using (SnapshotStream replayCapture = new SnapshotStream(_baseStream, Ownership.None))
            {
                // Preserve the base stream
                replayCapture.Snapshot();

                try
                {
                    using (TFileSystem replayFs = CreateFileSystem(replayCapture))
                    {
                        // Re-init the RNG to it's state when the checkpoint started, so we get reproducibility.
                        replayFs.Options.RandomNumberGenerator = new Random(_checkpointRngSeed);

                        Dictionary<string, object> replayContext = new Dictionary<string, object>();

                        for (int i = 0; i < activityCount; ++i)
                        {
                            _checkpointBuffer[i](replayFs, replayContext);
                        }

                        return DoVerify(replayCapture, null, ReportLevels.None);
                    }
                }
                catch
                {
                    return false;
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
        public object PerformActivity(Activity<TFileSystem> activity)
        {
            if (_lockdown)
            {
                throw new InvalidOperationException("Validator in lock-down, file system corruption has been detected.");
            }

            if (!_initialized)
            {
                Initialize();
            }

            _totalEventsBeforeLockDown++;
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
                    // Roll over the on-disk trace
                    if (_runGlobalTrace)
                    {
                        _globalTrace.WriteToFile(string.Format(CultureInfo.InvariantCulture, @"C:\temp\working\trace{0:X3}.log", _numScheduledCheckpoints++));
                    }

                    // We only do a full checkpoint, if the activity didn't throw an exception.  Otherwise,
                    // we'll discard all replay info just when the caller might want it.  Instead, just do a
                    // verify until (and unless), an activity that doesn't throw an exception happens.
                    if (doCheckpoint)
                    {
                        CheckpointAndThrow();
                    }
                    else
                    {
                        VerifyAndThrow();
                    }
                }
            }
        }

        private void Initialize()
        {
            if (_initialized)
            {
                throw new InvalidOperationException();
            }

            _snapStream = new SnapshotStream(_baseStream, Ownership.None);
            Stream focusStream = _snapStream;

            _masterRng = new Random(56456456);

            if (_runGlobalTrace)
            {
                _globalTrace = new TracingStream(_snapStream, Ownership.None);
                _globalTrace.CaptureStackTraces = _globalTraceCaptureStackTraces;
                _globalTrace.Reset(_runGlobalTrace);
                _globalTrace.WriteToFile(string.Format(CultureInfo.InvariantCulture, @"C:\temp\working\trace{0:X3}.log", _numScheduledCheckpoints++));
                focusStream = _globalTrace;
            }

            _checkpointRngSeed = _masterRng.Next();

            _activityContext = new Dictionary<string, object>();

            _checkpointBuffer = new List<Activity<TFileSystem>>();

            _liveTarget = CreateFileSystem(focusStream);
            _liveTarget.Options.RandomNumberGenerator = new Random(_checkpointRngSeed);

            // Take a snapshot, to preserve the stream state before we perform
            // an operation (assumption is that merely creating a file system object
            // (above) is not significant...
            _snapStream.Snapshot();

            _initialized = true;

            // Preliminary test, lets make sure we think the file system's good before we start...
            VerifyAndThrow();
        }

        private static bool DoVerify(Stream s, TextWriter w, ReportLevels levels)
        {
            TChecker checker = CreateChecker(s);

            if (w != null)
            {
                return checker.Check(w, levels);
            }
            else
            {
                using (NullTextWriter nullWriter = new NullTextWriter())
                {
                    return checker.Check(nullWriter, ReportLevels.None);
                }
            }
        }

        private void CheckpointAndThrow()
        {
            using (StringWriter writer = new StringWriter(CultureInfo.InvariantCulture))
            {
                bool passed = Checkpoint(writer, ReportLevels.Errors);

                _lastCheckpointReport = writer.GetStringBuilder().ToString();

                if (!passed)
                {
                    throw new ValidatingFileSystemException("File system failed verification", _failureException);
                }
            }
        }

        private void VerifyAndThrow()
        {
            using (StringWriter writer = new StringWriter(CultureInfo.InvariantCulture))
            {
                bool passed = Verify(writer, ReportLevels.Errors);

                _lastCheckpointReport = writer.GetStringBuilder().ToString();

                if (!passed)
                {
                    throw new ValidatingFileSystemException("File system failed verification", _failureException);
                }
            }
        }

        private static TFileSystem CreateFileSystem(Stream stream)
        {
            try
            {
#if NET40
                return (TFileSystem)typeof(TFileSystem).GetConstructor(new Type[] { typeof(Stream) }).Invoke(new object[] { stream });
#else
                return (TFileSystem)typeof(TFileSystem).GetTypeInfo().GetConstructor(new Type[] { typeof(Stream) }).Invoke(new object[] { stream });
#endif
            }
            catch (TargetInvocationException tie)
            {
#if NET40
                FieldInfo remoteStackTraceString = typeof(Exception).GetField("_remoteStackTraceString", BindingFlags.Instance | BindingFlags.NonPublic);
                remoteStackTraceString.SetValue(tie.InnerException, tie.InnerException.StackTrace + Environment.NewLine);

                throw tie.InnerException;
#else
                FieldInfo remoteStackTraceString = typeof(Exception).GetTypeInfo().GetField("_remoteStackTraceString", BindingFlags.Instance | BindingFlags.NonPublic);
                remoteStackTraceString.SetValue(tie.InnerException, tie.InnerException.StackTrace + Environment.NewLine);

                throw tie.InnerException; 
#endif
            }
        }

        private static TChecker CreateChecker(Stream stream)
        {
            try
            {
#if NET40
                return (TChecker)typeof(TChecker).GetConstructor(new Type[] { typeof(Stream) }).Invoke(new object[] { stream });
#else
                return (TChecker)typeof(TChecker).GetTypeInfo().GetConstructor(new Type[] { typeof(Stream) }).Invoke(new object[] { stream });
#endif
            }
            catch (TargetInvocationException tie)
            {
#if NET40
                FieldInfo remoteStackTraceString = typeof(Exception).GetField("_remoteStackTraceString", BindingFlags.Instance | BindingFlags.NonPublic);
                remoteStackTraceString.SetValue(tie.InnerException, tie.InnerException.StackTrace + Environment.NewLine);

                throw tie.InnerException;
#else
                FieldInfo remoteStackTraceString = typeof(Exception).GetTypeInfo().GetField("_remoteStackTraceString", BindingFlags.Instance | BindingFlags.NonPublic);
                remoteStackTraceString.SetValue(tie.InnerException, tie.InnerException.StackTrace + Environment.NewLine);

                throw tie.InnerException;
#endif
            }
        }

        #region DiscFileSystem Implementation
        /// <summary>
        /// Provides a friendly description of the file system type.
        /// </summary>
        public override string FriendlyName
        {
            get
            {
                Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
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
            get
            {
                Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
                {
                    return fs.CanWrite;
                };

                return (bool)PerformActivity(fn);
            }
        }

        /// <summary>
        /// Gets the root directory of the file system.
        /// </summary>
        public override DiscDirectoryInfo Root
        {
            get
            {
                return new DiscDirectoryInfo(this, "");
            }
        }

        /// <summary>
        /// Copies an existing file to a new file.
        /// </summary>
        /// <param name="sourceFile">The source file</param>
        /// <param name="destinationFile">The destination file</param>
        public override void CopyFile(string sourceFile, string destinationFile)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                fs.CopyFile(sourceFile, destinationFile);
                return null;
            };

            PerformActivity(fn);
        }

        /// <summary>
        /// Copies an existing file to a new file, allowing overwriting of an existing file.
        /// </summary>
        /// <param name="sourceFile">The source file</param>
        /// <param name="destinationFile">The destination file</param>
        /// <param name="overwrite">Whether to permit over-writing of an existing file.</param>
        public override void CopyFile(string sourceFile, string destinationFile, bool overwrite)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                fs.CopyFile(sourceFile, destinationFile, overwrite);
                return null;
            };

            PerformActivity(fn);
        }

        /// <summary>
        /// Creates a directory.
        /// </summary>
        /// <param name="path">The path of the new directory</param>
        public override void CreateDirectory(string path)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                fs.CreateDirectory(path);
                return 0;
            };

            PerformActivity(fn);
        }

        /// <summary>
        /// Deletes a directory.
        /// </summary>
        /// <param name="path">The path of the directory to delete.</param>
        public override void DeleteDirectory(string path)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                fs.DeleteDirectory(path);
                return null;
            };

            PerformActivity(fn);
        }

        /// <summary>
        /// Deletes a file.
        /// </summary>
        /// <param name="path">The path of the file to delete.</param>
        public override void DeleteFile(string path)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                fs.DeleteFile(path);
                return null;
            };

            PerformActivity(fn);
        }

        /// <summary>
        /// Indicates if a directory exists.
        /// </summary>
        /// <param name="path">The path to test</param>
        /// <returns>true if the directory exists</returns>
        public override bool DirectoryExists(string path)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                return fs.DirectoryExists(path);
            };

            return (bool)PerformActivity(fn);
        }

        /// <summary>
        /// Indicates if a file exists.
        /// </summary>
        /// <param name="path">The path to test</param>
        /// <returns>true if the file exists</returns>
        public override bool FileExists(string path)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                return fs.FileExists(path);
            };

            return (bool)PerformActivity(fn);
        }

        /// <summary>
        /// Indicates if a file or directory exists.
        /// </summary>
        /// <param name="path">The path to test</param>
        /// <returns>true if the file or directory exists</returns>
        public override bool Exists(string path)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                return fs.Exists(path);
            };

            return (bool)PerformActivity(fn);
        }

        /// <summary>
        /// Gets the names of subdirectories in a specified directory.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <returns>Array of directories.</returns>
        public override string[] GetDirectories(string path)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                return fs.GetDirectories(path);
            };

            return (string[])PerformActivity(fn);
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
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                return fs.GetDirectories(path, searchPattern);
            };

            return (string[])PerformActivity(fn);
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
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                return fs.GetDirectories(path, searchPattern, searchOption);
            };

            return (string[])PerformActivity(fn);
        }

        /// <summary>
        /// Gets the names of files in a specified directory.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <returns>Array of files.</returns>
        public override string[] GetFiles(string path)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                return fs.GetFiles(path);
            };

            return (string[])PerformActivity(fn);
        }

        /// <summary>
        /// Gets the names of files in a specified directory.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <param name="searchPattern">The search string to match against.</param>
        /// <returns>Array of files matching the search pattern.</returns>
        public override string[] GetFiles(string path, string searchPattern)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                return fs.GetFiles(path, searchPattern);
            };

            return (string[])PerformActivity(fn);
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
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                return fs.GetFiles(path, searchPattern, searchOption);
            };

            return (string[])PerformActivity(fn);
        }

        /// <summary>
        /// Gets the names of all files and subdirectories in a specified directory.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <returns>Array of files and subdirectories matching the search pattern.</returns>
        public override string[] GetFileSystemEntries(string path)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                return fs.GetFileSystemEntries(path);
            };

            return (string[])PerformActivity(fn);
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
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                return fs.GetFileSystemEntries(path, searchPattern);
            };

            return (string[])PerformActivity(fn);
        }

        /// <summary>
        /// Moves a directory.
        /// </summary>
        /// <param name="sourceDirectoryName">The directory to move.</param>
        /// <param name="destinationDirectoryName">The target directory name.</param>
        public override void MoveDirectory(string sourceDirectoryName, string destinationDirectoryName)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                fs.MoveDirectory(sourceDirectoryName, destinationDirectoryName);
                return null;
            };

            PerformActivity(fn);
        }

        /// <summary>
        /// Moves a file.
        /// </summary>
        /// <param name="sourceName">The file to move.</param>
        /// <param name="destinationName">The target file name.</param>
        public override void MoveFile(string sourceName, string destinationName)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                fs.MoveFile(sourceName, destinationName);
                return null;
            };

            PerformActivity(fn);
        }

        /// <summary>
        /// Moves a file, allowing an existing file to be overwritten.
        /// </summary>
        /// <param name="sourceName">The file to move.</param>
        /// <param name="destinationName">The target file name.</param>
        /// <param name="overwrite">Whether to permit a destination file to be overwritten</param>
        public override void MoveFile(string sourceName, string destinationName, bool overwrite)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                fs.MoveFile(sourceName, destinationName, overwrite);
                return null;
            };

            PerformActivity(fn);
        }

        /// <summary>
        /// Opens the specified file.
        /// </summary>
        /// <param name="path">The full path of the file to open.</param>
        /// <param name="mode">The file mode for the created stream.</param>
        /// <returns>The new stream.</returns>
        public override SparseStream OpenFile(string path, FileMode mode)
        {
            // This delegate can be used at any time the wrapper needs it, if it's in a 'replay' but the real file open isn't.
            StreamOpenFn reopenFn = delegate (TFileSystem fs)
            {
                return fs.OpenFile(path, mode);
            };

            ValidatingFileSystemWrapperStream<TFileSystem, TChecker> wrapper = new ValidatingFileSystemWrapperStream<TFileSystem, TChecker>(this, reopenFn);

            Activity<TFileSystem> activity = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                SparseStream s = fs.OpenFile(path, mode);
                wrapper.SetNativeStream(context, s);
                return s;
            };

            PerformActivity(activity);

            return wrapper;
        }

        /// <summary>
        /// Opens the specified file.
        /// </summary>
        /// <param name="path">The full path of the file to open.</param>
        /// <param name="mode">The file mode for the created stream.</param>
        /// <param name="access">The access permissions for the created stream.</param>
        /// <returns>The new stream.</returns>
        public override SparseStream OpenFile(string path, FileMode mode, FileAccess access)
        {
            // This delegate can be used at any time the wrapper needs it, if it's in a 'replay' but the real file open isn't.
            StreamOpenFn reopenFn = delegate (TFileSystem fs)
            {
                return fs.OpenFile(path, mode, access);
            };

            ValidatingFileSystemWrapperStream<TFileSystem, TChecker> wrapper = new ValidatingFileSystemWrapperStream<TFileSystem, TChecker>(this, reopenFn);

            Activity<TFileSystem> activity = delegate (TFileSystem fs, Dictionary<string, object> context)
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
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                return fs.GetAttributes(path);
            };

            return (FileAttributes)PerformActivity(fn);
        }

        /// <summary>
        /// Sets the attributes of a file or directory.
        /// </summary>
        /// <param name="path">The file or directory to change</param>
        /// <param name="newValue">The new attributes of the file or directory</param>
        public override void SetAttributes(string path, FileAttributes newValue)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                fs.SetAttributes(path, newValue);
                return null;
            };

            PerformActivity(fn);
        }

        /// <summary>
        /// Gets the creation time (in local time) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory</param>
        /// <returns>The creation time.</returns>
        public override DateTime GetCreationTime(string path)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                return fs.GetCreationTime(path);
            };

            return (DateTime)PerformActivity(fn);
        }

        /// <summary>
        /// Sets the creation time (in local time) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <param name="newTime">The new time to set.</param>
        public override void SetCreationTime(string path, DateTime newTime)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                fs.SetCreationTime(path, newTime);
                return null;
            };

            PerformActivity(fn);
        }

        /// <summary>
        /// Gets the creation time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <returns>The creation time.</returns>
        public override DateTime GetCreationTimeUtc(string path)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                return fs.GetCreationTimeUtc(path);
            };

            return (DateTime)PerformActivity(fn);
        }

        /// <summary>
        /// Sets the creation time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <param name="newTime">The new time to set.</param>
        public override void SetCreationTimeUtc(string path, DateTime newTime)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                fs.SetCreationTimeUtc(path, newTime);
                return null;
            };

            PerformActivity(fn);
        }

        /// <summary>
        /// Gets the last access time (in local time) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory</param>
        /// <returns>The last access time</returns>
        public override DateTime GetLastAccessTime(string path)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                return fs.GetLastAccessTime(path);
            };

            return (DateTime)PerformActivity(fn);
        }

        /// <summary>
        /// Sets the last access time (in local time) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <param name="newTime">The new time to set.</param>
        public override void SetLastAccessTime(string path, DateTime newTime)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                fs.SetLastAccessTime(path, newTime);
                return null;
            };

            PerformActivity(fn);
        }

        /// <summary>
        /// Gets the last access time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory</param>
        /// <returns>The last access time</returns>
        public override DateTime GetLastAccessTimeUtc(string path)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                return fs.GetLastAccessTimeUtc(path);
            };

            return (DateTime)PerformActivity(fn);
        }

        /// <summary>
        /// Sets the last access time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <param name="newTime">The new time to set.</param>
        public override void SetLastAccessTimeUtc(string path, DateTime newTime)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                fs.SetLastAccessTimeUtc(path, newTime);
                return null;
            };

            PerformActivity(fn);
        }

        /// <summary>
        /// Gets the last modification time (in local time) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory</param>
        /// <returns>The last write time</returns>
        public override DateTime GetLastWriteTime(string path)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                return fs.GetLastWriteTime(path);
            };

            return (DateTime)PerformActivity(fn);
        }

        /// <summary>
        /// Sets the last modification time (in local time) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <param name="newTime">The new time to set.</param>
        public override void SetLastWriteTime(string path, DateTime newTime)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                fs.SetLastWriteTime(path, newTime);
                return null;
            };

            PerformActivity(fn);
        }

        /// <summary>
        /// Gets the last modification time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory</param>
        /// <returns>The last write time</returns>
        public override DateTime GetLastWriteTimeUtc(string path)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                return fs.GetLastWriteTime(path);
            };

            return (DateTime)PerformActivity(fn);
        }

        /// <summary>
        /// Sets the last modification time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <param name="newTime">The new time to set.</param>
        public override void SetLastWriteTimeUtc(string path, DateTime newTime)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                fs.SetLastWriteTimeUtc(path, newTime);
                return null;
            };

            PerformActivity(fn);
        }

        /// <summary>
        /// Gets the length of a file.
        /// </summary>
        /// <param name="path">The path to the file</param>
        /// <returns>The length in bytes</returns>
        public override long GetFileLength(string path)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                return fs.GetFileLength(path);
            };

            return (long)PerformActivity(fn);
        }

        /// <summary>
        /// Gets an object representing a possible file.
        /// </summary>
        /// <param name="path">The file path</param>
        /// <returns>The representing object</returns>
        /// <remarks>The file does not need to exist</remarks>
        public override DiscFileInfo GetFileInfo(string path)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                return fs.GetFileInfo(path);
            };

            return (DiscFileInfo)PerformActivity(fn);
        }

        /// <summary>
        /// Gets an object representing a possible directory.
        /// </summary>
        /// <param name="path">The directory path</param>
        /// <returns>The representing object</returns>
        /// <remarks>The directory does not need to exist</remarks>
        public override DiscDirectoryInfo GetDirectoryInfo(string path)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                return fs.GetDirectoryInfo(path);
            };

            return (DiscDirectoryInfo)PerformActivity(fn);
        }

        /// <summary>
        /// Gets an object representing a possible file system object (file or directory).
        /// </summary>
        /// <param name="path">The file system path</param>
        /// <returns>The representing object</returns>
        /// <remarks>The file system object does not need to exist</remarks>
        public override DiscFileSystemInfo GetFileSystemInfo(string path)
        {
            Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
            {
                return fs.GetFileSystemInfo(path);
            };

            return (DiscFileSystemInfo)PerformActivity(fn);
        }

        /// <summary>
        /// Gets the Volume Label.
        /// </summary>
        public override string VolumeLabel
        {
            get
            {
                Activity<TFileSystem> fn = delegate (TFileSystem fs, Dictionary<string, object> context)
                {
                    return fs.VolumeLabel;
                };

                return (string)PerformActivity(fn);
            }
        }

        public override long Size => throw new NotImplementedException();

        public override long UsedSpace => throw new NotImplementedException();

        public override long AvailableSpace => throw new NotImplementedException();
        #endregion
    }
}
