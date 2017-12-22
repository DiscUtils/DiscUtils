using System.IO;

namespace DiscUtils.Nfs
{
    public class RpcStreamTransport : IRpcTransport
    {
        private readonly Stream _stream;

        public RpcStreamTransport(Stream stream)
        {
            this._stream = stream;
        }

        public void Dispose()
        {
        }

        public byte[] SendAndReceive(byte[] message)
        {
            throw new System.NotImplementedException();
        }

        public byte[] Receive()
        {
            return RpcTcpTransport.Receive(_stream);
        }

        public void Send(byte[] message)
        {
            RpcTcpTransport.Send(_stream, message);
        }
    }
}
