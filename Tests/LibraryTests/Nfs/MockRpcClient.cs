using DiscUtils.Nfs;
using System;
using System.Collections.Generic;

namespace LibraryTests.Nfs
{
    internal class MockRpcClient : IRpcClient
    {
        private readonly Random random = new Random();
        private readonly BlockingQueue<byte[]> _input;
        private readonly BlockingQueue<byte[]> _output;

        public MockRpcClient(BlockingQueue<byte[]> input, BlockingQueue<byte[]> output)
        {
            _input = input;
            _output = output;
        }

        public RpcCredentials Credentials
        {
            get;
            set;
        }

        public void Dispose()
        {
        }

        public IRpcTransport GetTransport(int program, int version)
        {
            return new MockRpcTransport(_input, _output);
        }

        public uint NextTransactionId()
        {
            return (uint)random.Next();
        }
    }
}
