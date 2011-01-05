using System;
using System.Net;
using System.Threading.Tasks;
using Coroutine;

namespace Kayak
{
    /// <summary>
    /// Represents a socket which supports asynchronous IO operations.
    /// </summary>
    public interface ISocket : IDisposable
    {
        /// <summary>
        /// The IP end point of the connected peer.
        /// </summary>
        IPEndPoint RemoteEndPoint { get; }

        /// <summary>
        /// Returns an observable which, upon subscription, begins an asynchronous write
        /// operation. When the operation completes, the observable yields the number of
        /// bytes written and completes.
        /// </summary>
        void Write(byte[] buffer, int offset, int count, Action<int> bytesWritten, Action<Exception> exception);

        /// <summary>
        /// Returns an observable which, upon subscription, begins copying a file
        /// to the socket. When the copy operation completes, the observable completes.
        /// </summary>
        void WriteFile(string file, Action completed, Action<Exception> exception);

        /// <summary>
        /// Returns an observable which, upon subscription, begins an asynchronous read
        /// operation. When the operation completes, the observable yields the number of
        /// bytes read and completes.
        /// </summary>
        void Read(byte[] buffer, int offset, int count, Action<int> bytesRead, Action<Exception> exception);
    }

}
