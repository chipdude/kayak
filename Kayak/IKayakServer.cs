using System;

namespace Kayak
{
    public interface IKayakServer
    {
        IDisposable Start();
        void GetConnection(Action<ISocket> socket);
    }
}
