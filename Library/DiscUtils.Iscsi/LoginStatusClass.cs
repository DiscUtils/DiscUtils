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
    /// Reasons for iSCSI login sucess or failure.
    /// </summary>
    public enum LoginStatusCode
    {
        /// <summary>
        /// Login succeeded.
        /// </summary>
        Success = 0,

        /// <summary>
        /// The iSCSI target name has moved temporarily to a new address.
        /// </summary>
        TargetMovedTemporarily = 0x0101,

        /// <summary>
        /// The iSCSI target name has moved permanently to a new address.
        /// </summary>
        TargetMovedPermanently = 0x0102,

        /// <summary>
        /// The Initiator was at fault.
        /// </summary>
        InitiatorError = 0x0200,

        /// <summary>
        /// The Initiator could not be authenticated, or the Target doesn't support authentication.
        /// </summary>
        AuthenticationFailure = 0x0201,

        /// <summary>
        /// The Initiator is not permitted to access the given Target.
        /// </summary>
        AuthorizationFailure = 0x0202,

        /// <summary>
        /// The given iSCSI Target Name was not found.
        /// </summary>
        NotFound = 0x0203,

        /// <summary>
        /// The Target has been removed, and no new address provided.
        /// </summary>
        TargetRemoved = 0x0204,

        /// <summary>
        /// The Target does not support this version of the iSCSI protocol.
        /// </summary>
        UnsupportedVersion = 0x0205,

        /// <summary>
        /// Too many connections for this iSCSI session.
        /// </summary>
        TooManyConnections = 0x0206,

        /// <summary>
        /// A required parameter is missing.
        /// </summary>
        MissingParameter = 0x0207,

        /// <summary>
        /// The Target does not support session spanning to this connection (address).
        /// </summary>
        CannotIncludeInSession = 0x0208,

        /// <summary>
        /// The Target does not support this type of session (or not from this Initiator).
        /// </summary>
        SessionTypeNotSupported = 0x0209,

        /// <summary>
        /// Attempt to add a connection to a non-existent session.
        /// </summary>
        SessionDoesNotExist = 0x020A,

        /// <summary>
        /// An invalid request was sent during the login sequence.
        /// </summary>
        InvalidDuringLogin = 0x020B,

        /// <summary>
        /// The Target suffered an unknown hardware or software failure.
        /// </summary>
        TargetError = 0x0300,

        /// <summary>
        /// The iSCSI service or Target is not currently operational.
        /// </summary>
        ServiceUnavailable = 0x0301,

        /// <summary>
        /// The Target is out of resources.
        /// </summary>
        OutOfResources = 0x0302
    }
}