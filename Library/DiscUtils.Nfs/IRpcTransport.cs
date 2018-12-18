using System;
using System.Collections.Generic;
using System.Text;

namespace DiscUtils.Nfs
{
    public interface IRpcTransport : IDisposable
    {
        void Send(byte[] message);
        byte[] SendAndReceive(byte[] message);
        byte[] Receive();
    }
}
