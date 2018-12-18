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

namespace DiscUtils.Nfs
{
    internal sealed class RpcClient : IRpcClient
    {
        private uint _nextTransaction;
        private readonly string _serverAddress;
        private Dictionary<int, RpcTcpTransport> _transports = new Dictionary<int, RpcTcpTransport>();

        public RpcClient(string address, RpcCredentials credential)
        {
            _serverAddress = address;
            Credentials = credential;
            _nextTransaction = (uint)new Random().Next();
            _transports[PortMap2.ProgramIdentifier] = new RpcTcpTransport(address, 111);
        }

        public RpcCredentials Credentials { get; }

        public void Dispose()
        {
            if (_transports != null)
            {
                foreach (RpcTcpTransport transport in _transports.Values)
                {
                    transport.Dispose();
                }

                _transports = null;
            }
        }

        public uint NextTransactionId()
        {
            return _nextTransaction++;
        }

        public IRpcTransport GetTransport(int program, int version)
        {
            RpcTcpTransport transport;
            if (!_transports.TryGetValue(program, out transport))
            {
                PortMap2 pm = new PortMap2(this);
                int port = pm.GetPort(program, version, PortMap2Protocol.Tcp);
                transport = new RpcTcpTransport(_serverAddress, port);
                _transports[program] = transport;
            }

            return transport;
        }
    }
}