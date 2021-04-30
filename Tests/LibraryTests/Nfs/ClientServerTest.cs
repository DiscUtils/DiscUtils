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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace LibraryTests.Nfs
{
    public class ClientServerTest
    {
        /*
        [Fact]
        public void LoginTest()
        {
            BlockingQueue<byte[]> serverToClient = new BlockingQueue<byte[]>();
            BlockingQueue<byte[]> clientToServer = new BlockingQueue<byte[]>();

            var rpcClient = new MockRpcClient(serverToClient, clientToServer);
            var rpcServer = new MockRpcClient(clientToServer, serverToClient);

            Nfs3Server server = new Nfs3FileServer(
                new Dictionary<string, string>()
                {
                    { "share", Path.GetFullPath("Nfs/Share")}
                });

            var serverLoop = Task.Run(() => server.ClientLoop(rpcServer.GetTransport(0, 0)));

            // Test: Mount a share
            Nfs3Client client = new Nfs3Client(rpcClient, "share");

            // Test: Read a directory
            var entries = client.ReadDirectory(client.RootHandle, silentFail: false).ToArray();
            Assert.Equal(2, entries.Length);
            Assert.Equal("Child", entries[0].Name);
            Assert.Equal("MyFile.txt", entries[1].Name);

            // Test: Read a file
            var readResult = client.Read(entries[1].FileHandle, 0, (int)entries[1].FileAttributes.Size);
            Assert.Equal(Nfs3Status.Ok, readResult.Status);
            Assert.Equal("Hello, World!", Encoding.UTF8.GetString(readResult.Data));

            // Test: Create a directory in a child node
            if (Directory.Exists(@"Nfs/Share/Child/NewDir"))
            {
                Directory.Delete(@"Nfs/Share/Child/NewDir");
            }

            var makeDirectoryResult = client.MakeDirectory(entries[0].FileHandle, "NewDir", new Nfs3SetAttributes()
            {
                SetAccessTime = Nfs3SetTimeMethod.ServerTime,
                SetModifyTime = Nfs3SetTimeMethod.ServerTime,
                SetMode = false,
                SetSize = false,
                SetGid = false,
                SetUid = false,
            });

            Assert.True(Directory.Exists(@"Nfs/Share/Child/NewDir"));

            // Test: Delete a directory
            client.RemoveDirectory(entries[0].FileHandle, "NewDir");
            Assert.False(Directory.Exists(@"Nfs/Share/Child/NewDir"));

            // Test: Create, Update, Read, Delete a file
            if (File.Exists(@"Nfs/Share/Child/NewFile"))
            {
                File.Delete(@"Nfs/Share/Child/NewFile");
            }

            var createResult = client.Create(entries[0].FileHandle, "NewFile", true, new Nfs3SetAttributes()
            {
                SetAccessTime = Nfs3SetTimeMethod.ServerTime,
                SetModifyTime = Nfs3SetTimeMethod.ServerTime,
                SetMode = false,
                SetSize = false,
                SetGid = false,
                SetUid = false,
            });

            Assert.True(File.Exists(@"Nfs/Share/Child/NewFile"));

            byte[] dataToWrite = Encoding.UTF8.GetBytes("Alors on dance");
            client.Write(createResult, 0, dataToWrite, 0, dataToWrite.Length);

            Assert.Equal("Alors on dance", File.ReadAllText(@"Nfs/Share/Child/NewFile"));

            readResult = client.Read(createResult, 0, dataToWrite.Length);
            Assert.Equal("Alors on dance", Encoding.UTF8.GetString(readResult.Data));

            client.Remove(entries[0].FileHandle, "NewFile");
            Assert.False(File.Exists(@"Nfs/Share/Child/NewFile"));

            // Lookup test
            var lookupResult = client.Lookup(client.RootHandle, "Child");
            Assert.Equal(entries[0].FileHandle, lookupResult);

            // Get attributes
            var getAttributes = client.GetAttributes(lookupResult);
        }*/
    }
}
