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

using DiscUtils.Nfs;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace NfsServer
{
    public class Program
    {
        public static int Main(string[] args)
        {
            Console.WriteLine("Starting NFS server");

            Console.WriteLine($"To connect, use sudo mount -t nfs -o proto=tcp,port=111,nfsvers=3 {GetLocalIPAddress()}:home /mnt/test or a similar command");

            var fileServer = new Nfs3FileServer(new Dictionary<string, string>()
            {
                { "home", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) },
                { "docs", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) }
            });

            RpcServer server = new RpcServer();
            server.Programs.Add(fileServer);

            server.Programs.Add(
                new Nfs3MountFileServer(fileServer));

            server.Programs.Add(
                new PortMap2Server(server.Programs));

            server.Run();

            return 0;
        }

        private static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }

            return "127.0.0.1";
        }
    }
}
