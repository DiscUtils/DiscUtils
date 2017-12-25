//
// Copyright (c) 2017, Quamotion
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

#if !NET20
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System;

namespace DiscUtils.Nfs
{
    public class RpcServer
    {
        public Collection<IRpcProgram> Programs { get; } = new Collection<IRpcProgram>();

        public void Run()
        {
            TcpListener listener = new TcpListener(IPAddress.Any, PortMap2Server.Port);
            listener.Start();

            while (true)
            {
#if NETSTANDARD1_5
                var socket = listener.AcceptSocketAsync().GetAwaiter().GetResult();
#else
                var socket = listener.AcceptSocket();
#endif
                ClientLoop(socket);
            }
        }

        private void ClientLoop(Socket socket)
        {
            using (socket)
            using (NetworkStream stream = new NetworkStream(socket))
            {
                RpcStreamTransport transport = new RpcStreamTransport(stream);
                try
                {
                    ClientLoop(transport);
                }
                catch (EndOfStreamException)
                {
                    Console.WriteLine("> Disconnected");
                    // The client has disconnected.
                }
            }
        }

        public void ClientLoop(IRpcTransport transport)
        {
            while (true)
            {
                byte[] message = transport.Receive();

                // TODO: Portmap Support
                // https://tools.ietf.org/html/rfc1833
                using (MemoryStream input = new MemoryStream(message))
                using (MemoryStream output = new MemoryStream())
                {
                    XdrDataReader reader = new XdrDataReader(input);

                    var transactionId = reader.ReadUInt32();
                    var messageType = (RpcMessageType)reader.ReadInt32();

                    RpcCallHeader header = new RpcCallHeader(reader);
                    IRpcObject response = null;

                    RpcMessageHeader responseHeader = null;

                    Console.WriteLine($"Program {header.Program} version {header.Version}, procedure {header.Proc}");

                    if (header.RpcVersion != Nfs3.RpcVersion)
                    {
                        responseHeader = new RpcMessageHeader()
                        {
                            ReplyHeader = new RpcReplyHeader()
                            {
                                Status = RpcReplyStatus.Denied,
                                RejectedReply = new RpcRejectedReplyHeader()
                                {
                                    AuthenticationStatus = RpcAuthenticationStatus.None,
                                    MismatchInfo = new RpcMismatchInfo()
                                    {
                                        High = 2,
                                        Low = 2
                                    },
                                    Status = RpcRejectedStatus.RpcMismatch
                                }
                            },
                            TransactionId = transactionId
                        };

                        Console.WriteLine("> RPC version mismatch");
                    }
                    else
                    {
                        var program = Programs
                            .SingleOrDefault(
                                p => p.ProgramIdentifier == header.Program
                                && p.ProgramVersion == header.Version);

                        if (program == null)
                        {
                            responseHeader = new RpcMessageHeader()
                            {
                                ReplyHeader = new RpcReplyHeader()
                                {
                                    Status = RpcReplyStatus.Accepted,
                                    AcceptReply = new RpcAcceptedReplyHeader()
                                    {
                                        AcceptStatus = RpcAcceptStatus.ProgramUnavailable,
                                        Verifier = new RpcAuthentication(new RpcUnixCredential(0, 0))
                                    }
                                },
                                TransactionId = transactionId
                            };

                            Console.WriteLine("> Unknown program");
                        }
                        else if (!program.Procedures.Contains((int)header.Proc))
                        {
                            responseHeader = new RpcMessageHeader()
                            {
                                ReplyHeader = new RpcReplyHeader()
                                {
                                    Status = RpcReplyStatus.Accepted,
                                    AcceptReply = new RpcAcceptedReplyHeader()
                                    {
                                        AcceptStatus = RpcAcceptStatus.ProcedureUnavailable,
                                        Verifier = new RpcAuthentication(new RpcUnixCredential(0, 0))
                                    }
                                },
                                TransactionId = transactionId
                            };

                            Console.WriteLine("> Unknown command");
                        }
                        else
                        {
                            responseHeader = RpcMessageHeader.Accepted(transactionId);
                            response = program.Invoke(header, reader);
                        }
                    }

                    XdrDataWriter writer = new XdrDataWriter(output);

                    responseHeader.Write(writer);

                    if (response != null)
                    {
                        response.Write(writer);
                    }

                    transport.Send(output.ToArray());
                }
            }
        }
    }
}
#endif