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
using System.Diagnostics;
using System.Globalization;

namespace DiscUtils.Diagnostics
{
    /// <summary>
    /// A record of an individual stream activity.
    /// </summary>
    public sealed class StreamTraceRecord
    {
        private int _id;
        private string _fileAction;
        private long _filePosition;
        private long _countArg;
        private long _result;
        private Exception _exThrown;
        private StackTrace _stack;

        internal StreamTraceRecord(int id, string fileAction, long filePosition, StackTrace stack)
        {
            _id = id;
            _fileAction = fileAction;
            _filePosition = filePosition;
            _stack = stack;
        }

        /// <summary>
        /// Unique identity for this record.
        /// </summary>
        public int Id
        {
            get { return _id; }
        }

        /// <summary>
        /// The type of action being performed.
        /// </summary>
        public string FileAction
        {
            get { return _fileAction; }
        }

        /// <summary>
        /// The stream position when the action was performed.
        /// </summary>
        public long FilePosition
        {
            get { return _filePosition; }
        }

        /// <summary>
        /// The count argument (if relevant) when the action was performed.
        /// </summary>
        public long CountArg
        {
            get { return _countArg; }
            internal set { _countArg = value; }
        }

        /// <summary>
        /// The return value (if relevant) when the action was performed.
        /// </summary>
        public long Result
        {
            get { return _result; }
            internal set { _result = value; }
        }

        /// <summary>
        /// The exception thrown during processing of this action.
        /// </summary>
        public Exception ExceptionThrown
        {
            get { return _exThrown; }
            internal set { _exThrown = value; }
        }

        /// <summary>
        /// A full stack trace at the point the action was performed.
        /// </summary>
        public StackTrace Stack
        {
            get { return _stack; }
        }

        /// <summary>
        /// Gets a string representation of the common fields.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:D3}{1,1}:{2,5}  @{3:X10}  [count={4}, result={5}]", _id, _exThrown != null ? "E" : " ", _fileAction, _filePosition, _countArg, _result);
        }
    }
}
