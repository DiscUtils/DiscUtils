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

namespace DiscUtils.Iscsi
{
    /// <summary>
    /// Enumeration of SCSI command status codes.
    /// </summary>
    public enum ScsiStatus : byte
    {
        /// <summary>
        /// Indicates that the command completed without error.
        /// </summary>
        Good = 0x00,

        /// <summary>
        /// An unsupported condition occured.
        /// </summary>
        CheckCondition = 0x02,

        /// <summary>
        /// For some commands only - indicates the specified condition was met.
        /// </summary>
        ConditionMet = 0x04,

        /// <summary>
        /// The device is busy.
        /// </summary>
        Busy = 0x08,

        /// <summary>
        /// Delivered command conflicts with an existing reservation.
        /// </summary>
        ReservationConflict = 0x18,

        /// <summary>
        /// The buffer of outstanding commands is full.
        /// </summary>
        TaskSetFull = 0x28,

        /// <summary>
        /// An ACA condition exists.
        /// </summary>
        AcaActive = 0x30,

        /// <summary>
        /// The command was aborted.
        /// </summary>
        TaskAborted = 0x40
    }
}