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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace LibraryTests.Nfs
{
    public class ClientServerTest
    {
        [Fact]
        public void LoginTest()
        {
            BlockingQueue<byte[]> serverToClient = new BlockingQueue<byte[]>();
            BlockingQueue<byte[]> clientToServer = new BlockingQueue<byte[]>();

            var rpcClient = new MockRpcClient(serverToClient, clientToServer);
            var rpcServer = new MockRpcClient(clientToServer, serverToClient);

            Nfs3Server server = new Nfs3Server();
            var serverLoop = Task.Run(() => server.ClientLoop(rpcServer.GetTransport(0, 0)));

            Nfs3Client client = new Nfs3Client(rpcClient, "/mnt");

            var entries = client.ReadDirectory(client.RootHandle, silentFail: false).ToArray();
            var result = client.Read(entries[0].FileHandle, 0, (int)entries[0].FileAttributes.Size);
        }
    }
}
