using DiscUtils.Nfs;
using System.Collections.Generic;
using System.Threading;

namespace LibraryTests.Nfs
{
    internal class MockRpcTransport : IRpcTransport
    {
        private readonly BlockingQueue<byte[]> _input;
        private readonly BlockingQueue<byte[]> _output;

        public MockRpcTransport(BlockingQueue<byte[]> input, BlockingQueue<byte[]> output)
        {
            _input = input;
            _output = output;
        }

        public void Dispose()
        {
        }

        public byte[] Receive()
        {
            return _input.Dequeue();
        }

        public void Send(byte[] message)
        {
            _output.Enqueue(message);
        }

        public byte[] SendAndReceive(byte[] message)
        {
            this.Send(message);
            return this.Receive();
        }
    }
}
