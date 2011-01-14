using System;
using System.Net;
using System.Threading.Tasks;
using Coroutine;

namespace Kayak
{

    public interface ISocket : IDisposable
    {
        // output
        ContinuationState<Unit> Write(ArraySegment<byte> bytes);
        ContinuationState<Unit> Write(string file);
        void End();

        // input
        void Enable();
        void Disable();
        Action<ArraySegment<byte>> OnData { set; }
        Action OnTimeout { set; }

        IPEndPoint RemoteEndPoint { get; }
        int Timeout { get; set; }
        bool NoDelay { get; set; }
    }

    public static partial class Extensions
    {
        public static ContinuationState<ArraySegment<byte>> Read(this ISocket socket)
        {
            return new ContinuationState<ArraySegment<byte>>((r, e) =>
                {
                    socket.OnData = d => r(d);
                    socket.OnTimeout = () => e(new Exception("The connection timed out."));
                });
        }
    }
}
