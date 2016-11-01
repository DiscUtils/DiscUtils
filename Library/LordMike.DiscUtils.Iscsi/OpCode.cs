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
    internal enum OpCode : byte
    {
        //
        // Initiator op-codes.
        //
        NopOut = 0x00,
        ScsiCommand = 0x01,
        ScsiTaskManagementRequest = 0x02,
        LoginRequest = 0x03,
        TextRequest = 0x04,
        ScsiDataOut = 0x05,
        LogoutRequest = 0x06,
        SnackRequest = 0x10,

        //
        // Target op-codes.
        //
        NopIn = 0x20,
        ScsiResponse = 0x21,
        ScsiTaskManagementResponse = 0x22,
        LoginResponse = 0x23,
        TextResponse = 0x24,
        ScsiDataIn = 0x25,
        LogoutResponse = 0x26,
        ReadyToTransfer = 0x31,
        AsynchronousMessage = 0x32,
        Reject = 0x3f
    }
}