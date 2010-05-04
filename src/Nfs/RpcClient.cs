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
using System.Collections.Generic;

namespace DiscUtils.Nfs
{
    internal sealed class RpcClient : IDisposable
    {
        private string _serverAddress;
        private RpcCredentials _credential;
        private uint _nextTransaction = 0;
        private Dictionary<int, RpcTcpTransport> _transports = new Dictionary<int, RpcTcpTransport>();

        public RpcClient(string address, RpcCredentials credential)
        {
            _serverAddress = address;
            _credential = credential;
            _nextTransaction = (uint)new Random().Next();
            _transports[PortMapper.ProgramIdentifier] = new RpcTcpTransport(address, 111);
        }

        public void Dispose()
        {
            if (_transports != null)
            {
                foreach (var transport in _transports.Values)
                {
                    transport.Dispose();
                }
                _transports = null;
            }
        }

        internal uint NextTransactionId()
        {
            return _nextTransaction++;
        }

        internal RpcCredentials Credentials
        {
            get { return _credential; }
        }

        internal RpcTcpTransport GetTransport(int program, int version)
        {
            RpcTcpTransport transport;
            if (!_transports.TryGetValue(program, out transport))
            {
                PortMapper pm = new PortMapper(this);
                int port = pm.GetPort(program, version, PortMapperProtocol.Tcp);
                transport = new RpcTcpTransport(_serverAddress, port);
                _transports[program] = transport;
            }
            return transport;
        }

    }
}
