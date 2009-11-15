//
// Copyright (c) 2008-2009, Kenneth Bell
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

using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace DiscUtils.Nfs
{
    internal class RpcTcpTransport
    {
        private string _address;
        private int _port;
        private int _localPort;
        private Socket _socket;
        private Stream _tcpStream;

        public RpcTcpTransport(string address, int port)
            :this(address, port, 0)
        {
        }

        public RpcTcpTransport(string address, int port, int localPort)
        {
            _address = address;
            _port = port;
            _localPort = localPort;
        }

        public void Send(byte[] message)
        {
            bool sent = false;
            while (!sent)
            {
                while (_socket == null || !_socket.Connected)
                {
                    try
                    {
                        if (_socket != null)
                        {
                            _socket.Close();
                        }

                        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                        if (_localPort != 0)
                        {
                            _socket.Bind(new IPEndPoint(0, _localPort));
                        }
                        _socket.Connect(_address, _port);
                        _tcpStream = new NetworkStream(_socket, true);
                    }
                    catch
                    {
                        Thread.Sleep(1000);
                    }
                }

                try
                {
                    byte[] header = new byte[4];
                    Utilities.WriteBytesBigEndian((uint)(0x80000000 | (uint)message.Length), header, 0);
                    _tcpStream.Write(header, 0, 4);
                    _tcpStream.Write(message, 0, message.Length);
                    _tcpStream.Flush();
                    sent = true;
                }
                catch
                {
                }
            }
        }

        public byte[] Receive()
        {
            MemoryStream ms = null;
            bool lastFragFound = false;

            while (!lastFragFound)
            {
                byte[] header = Utilities.ReadFully(_tcpStream, 4);
                uint headerVal = Utilities.ToUInt32BigEndian(header, 0);

                lastFragFound = ((headerVal & 0x80000000) != 0);
                byte[] frag = Utilities.ReadFully(_tcpStream, (int)(headerVal & 0x7FFFFFFF));

                if(ms != null)
                {
                    ms.Write(frag, 0, frag.Length);
                }
                else if (!lastFragFound)
                {
                    ms = new MemoryStream();
                    ms.Write(frag, 0, frag.Length);
                }
                else
                {
                    return frag;
                }
            }

            return ms.ToArray();
        }
    }
}
