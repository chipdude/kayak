using System;
using Coroutine;
using System.IO;

namespace Kayak
{

    public class EventServer : IKayakServer
    {
        public EventServer()
        {
        }

        public IDisposable Start()
        {
            throw new NotImplementedException();
        }

        public Action<ISocket> OnConnection
        {
            set { throw new NotImplementedException(); }
        }
    }
}