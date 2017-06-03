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
using System.IO;
using System.Threading;
using DiscUtils.Streams;

namespace DiscUtils.Diagnostics
{
    internal sealed class ValidatingFileSystemWrapperStream<Tfs, Tc> : SparseStream
        where Tfs : DiscFileSystem, IDiagnosticTraceable
        where Tc : DiscFileSystemChecker
    {
        private ValidatingFileSystem<Tfs, Tc> _fileSystem;
        private ValidatingFileSystem<Tfs, Tc>.StreamOpenFn _openFn;
        private long _replayHandle;

        private static long _nextReplayHandle;

        private long _shadowPosition;
        private bool _disposed;


        public ValidatingFileSystemWrapperStream(ValidatingFileSystem<Tfs, Tc> fileSystem, ValidatingFileSystem<Tfs, Tc>.StreamOpenFn openFn)
        {
            _fileSystem = fileSystem;
            _openFn = openFn;

            _replayHandle = Interlocked.Increment(ref _nextReplayHandle);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed && !_fileSystem.InLockdown)
            {
                long pos = _shadowPosition;
                Activity<Tfs> fn = delegate(Tfs fs, Dictionary<string, object> context)
                {
                    GetNativeStream(fs, context, pos).Dispose();
                    _disposed = true;
                    ForgetNativeStream(context);
                    return 0;
                };

                _fileSystem.PerformActivity(fn);

            }

            // Don't call base.Dispose because it calls close 
            base.Dispose(disposing);
        }

        public override bool CanRead
        {
            get
            {
                long pos = _shadowPosition;

                Activity<Tfs> fn = delegate(Tfs fs, Dictionary<string, object> context)
                {
                    return GetNativeStream(fs, context, pos).CanRead;
                };

                return (bool)_fileSystem.PerformActivity(fn);
            }
        }

        public override bool CanSeek
        {
            get
            {
                long pos = _shadowPosition;

                Activity<Tfs> fn = delegate(Tfs fs, Dictionary<string, object> context)
                {
                    return GetNativeStream(fs, context, pos).CanSeek;
                };

                return (bool)_fileSystem.PerformActivity(fn);
            }
        }

        public override bool CanWrite
        {
            get
            {
                long pos = _shadowPosition;

                Activity<Tfs> fn = delegate(Tfs fs, Dictionary<string, object> context)
                {
                    return GetNativeStream(fs, context, pos).CanWrite;
                };

                return (bool)_fileSystem.PerformActivity(fn);
            }
        }

        public override void Flush()
        {
            long pos = _shadowPosition;

            Activity<Tfs> fn = delegate(Tfs fs, Dictionary<string, object> context)
            {
                GetNativeStream(fs, context, pos).Flush();
                return 0;
            };

            _fileSystem.PerformActivity(fn);
        }

        public override long Length
        {
            get
            {
                long pos = _shadowPosition;

                Activity<Tfs> fn = delegate(Tfs fs, Dictionary<string, object> context)
                {
                    return GetNativeStream(fs, context, pos).Length;
                };

                return (long)_fileSystem.PerformActivity(fn);
            }
        }

        public override long Position
        {
            get
            {
                long pos = _shadowPosition;

                Activity<Tfs> fn = delegate(Tfs fs, Dictionary<string, object> context)
                {
                    return GetNativeStream(fs, context, pos).Position;
                };

                return (long)_fileSystem.PerformActivity(fn);
            }
            set
            {
                long pos = _shadowPosition;

                Activity<Tfs> fn = delegate(Tfs fs, Dictionary<string, object> context)
                {
                    GetNativeStream(fs, context, pos).Position = value;
                    return 0;
                };

                _fileSystem.PerformActivity(fn);

                _shadowPosition = value;
            }
        }

        public override IEnumerable<StreamExtent> Extents
        {
            get
            {
                long pos = _shadowPosition;

                Activity<Tfs> fn = delegate(Tfs fs, Dictionary<string, object> context)
                {
                    return GetNativeStream(fs, context, pos).Extents;
                };

                return (IEnumerable<StreamExtent>)_fileSystem.PerformActivity(fn);
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            long pos = _shadowPosition;

            // Avoid stomping on buffers we know nothing about by ditching the writes into gash buffer.
            byte[] tempBuffer = new byte[buffer.Length];

            Activity<Tfs> fn = delegate(Tfs fs, Dictionary<string, object> context)
            {
                return GetNativeStream(fs, context, pos).Read(tempBuffer, offset, count);
            };

            int numRead = (int)_fileSystem.PerformActivity(fn);

            Array.Copy(tempBuffer, buffer, numRead);

            _shadowPosition += numRead;

            return numRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long pos = _shadowPosition;

            Activity<Tfs> fn = delegate(Tfs fs, Dictionary<string, object> context)
            {
                return GetNativeStream(fs, context, pos).Seek(offset, origin);
            };

            _shadowPosition = (long)_fileSystem.PerformActivity(fn);

            return _shadowPosition;
        }

        public override void SetLength(long value)
        {
            long pos = _shadowPosition;

            Activity<Tfs> fn = delegate(Tfs fs, Dictionary<string, object> context)
            {
                GetNativeStream(fs, context, pos).SetLength(value);
                return 0;
            };

            _fileSystem.PerformActivity(fn);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            long pos = _shadowPosition;

            // Take a copy of the buffer - otherwise who knows what we're messing with.
            byte[] tempBuffer = new byte[buffer.Length];
            Array.Copy(buffer, tempBuffer, buffer.Length);

            Activity<Tfs> fn = delegate(Tfs fs, Dictionary<string, object> context)
            {
                GetNativeStream(fs, context, pos).Write(tempBuffer, offset, count);
                return 0;
            };

            _fileSystem.PerformActivity(fn);

            _shadowPosition += count;
        }


        internal void SetNativeStream(Dictionary<string, object> context, Stream s)
        {
            string streamKey = "WrapStream#" + _replayHandle + "_Stream";

            context[streamKey] = s;
        }

        private SparseStream GetNativeStream(Tfs fs, Dictionary<string, object> context, long shadowPosition)
        {
            string streamKey = "WrapStream#" + _replayHandle + "_Stream";

            Object streamObj;
            SparseStream s;

            if (context.TryGetValue(streamKey, out streamObj))
            {
                s = (SparseStream)streamObj;
            }
            else
            {
                // The native stream isn't in the context.  This means we're replaying
                // but the stream open isn't part of the sequence being replayed.  We
                // do our best to re-create it...
                s = _openFn(fs);
                context[streamKey] = s;
            }

            if (shadowPosition != s.Position)
            {
                s.Position = shadowPosition;
            }

            return s;
        }

        private void ForgetNativeStream(Dictionary<string, object> context)
        {
            string streamKey = "WrapStream#" + _replayHandle + "_Stream";
            context.Remove(streamKey);
        }
    }
}
