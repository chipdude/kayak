using System;

namespace Kayak
{
    public interface IKayakServer
    {
        IDisposable Start();
        Action<ISocket> OnConnection { set; }
    }
}
