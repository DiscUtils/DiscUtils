//
// Copyright (c) 2008-2010, Kenneth Bell
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
using System.Runtime.Serialization;

namespace DiscUtils.Iscsi
{
    /// <summary>
    /// Exception thrown when a low-level iSCSI failure is detected.
    /// </summary>
    [Serializable]
    public class InvalidProtocolException : IscsiException
    {
        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public InvalidProtocolException()
        {
        }

        /// <summary>
        /// Creates a new instance containing a message.
        /// </summary>
        /// <param name="message">The reason for the exception</param>
        public InvalidProtocolException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Creates a new instance containing a message and an inner exception.
        /// </summary>
        /// <param name="message">The reason for the exception</param>
        /// <param name="innerException">The inner exception</param>
        public InvalidProtocolException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Creates a new deserialized instance.
        /// </summary>
        /// <param name="info">The serialization info</param>
        /// <param name="context">Ther context</param>
        protected InvalidProtocolException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
