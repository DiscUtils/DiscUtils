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
using System.Security.Permissions;

namespace DiscUtils.Iscsi
{
    /// <summary>
    /// Exception thrown when a low-level iSCSI failure is detected.
    /// </summary>
    [Serializable]
    public class ScsiCommandException : IscsiException
    {
        private ScsiStatus _status;
        private byte[] _senseData;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public ScsiCommandException()
        {
            _status = ScsiStatus.Good;
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="status">The SCSI status code</param>
        public ScsiCommandException(ScsiStatus status)
        {
            _status = status;
        }

        /// <summary>
        /// Creates a new instance containing a message.
        /// </summary>
        /// <param name="message">The reason for the exception</param>
        public ScsiCommandException(string message)
            : base(message)
        {
            _status = ScsiStatus.Good;
        }

        /// <summary>
        /// Creates a new instance containing a message.
        /// </summary>
        /// <param name="status">The SCSI status code</param>
        /// <param name="message">The reason for the exception</param>
        public ScsiCommandException(ScsiStatus status, string message)
            : base(message)
        {
            _status = status;
        }

        /// <summary>
        /// Creates a new instance containing a message.
        /// </summary>
        /// <param name="status">The SCSI status code</param>
        /// <param name="message">The reason for the exception</param>
        /// <param name="senseData">The SCSI sense data</param>
        public ScsiCommandException(ScsiStatus status, string message, byte[] senseData)
            : base(message)
        {
            _status = status;
            _senseData = senseData;
        }

        /// <summary>
        /// Creates a new instance containing a message and an inner exception.
        /// </summary>
        /// <param name="message">The reason for the exception</param>
        /// <param name="innerException">The inner exception</param>
        public ScsiCommandException(string message, Exception innerException)
            : base(message, innerException)
        {
            _status = ScsiStatus.Good;
        }

        /// <summary>
        /// Creates a new instance containing a message and an inner exception.
        /// </summary>
        /// <param name="status">The SCSI status code</param>
        /// <param name="message">The reason for the exception</param>
        /// <param name="innerException">The inner exception</param>
        public ScsiCommandException(ScsiStatus status, string message, Exception innerException)
            : base(message, innerException)
        {
            _status = status;
        }

        /// <summary>
        /// Creates a new deserialized instance.
        /// </summary>
        /// <param name="info">The serialization info</param>
        /// <param name="context">Ther context</param>
        protected ScsiCommandException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _status = (ScsiStatus)info.GetByte("status");
            _senseData = (byte[])info.GetValue("senseData", typeof(byte[]));
        }

        /// <summary>
        /// Gets the serialized state of this exception.
        /// </summary>
        /// <param name="info">The serialization info</param>
        /// <param name="context">The serialization context</param>
        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("status", (byte)_status);
            info.AddValue("senseData", _senseData);
        }

        /// <summary>
        /// Gets the SCSI status associated with this exception.
        /// </summary>
        public ScsiStatus Status
        {
            get { return _status; }
        }

        /// <summary>
        /// Gets the SCSI sense data (if any) associated with this exception.
        /// </summary>
        /// <returns>The SCSI sense data, or <c>null</c>.</returns>
        public byte[] GetSenseData()
        {
            return _senseData;
        }
    }
}
