﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Kayak.Http
{

    class Response : IOutputStream, IOutputStreamDelegate, IHttpServerResponse
    {
        Version version;
        ResponseState state;

        public bool KeepAlive { get { return state.keepAlive; } }

        string status;
        IDictionary<string, string> headers;

        IOutputStream output;
        IOutputStreamDelegate del;

        public Response(IOutputStreamDelegate del, IHttpServerRequest request, bool shouldKeepAlive)
        {
            this.del = del;
            this.version = request.Version;
            output = new AttachableStream(this);
            state = ResponseState.Create(request, shouldKeepAlive);
        }

        #region IOutputStream

        void IOutputStream.Attach(ISocket socket)
        {
            output.Attach(socket);
        }
        
        void IOutputStream.Detach(ISocket socket)
        {
            output.Detach(socket);
        }

        bool IOutputStream.Write(ArraySegment<byte> data, Action continuation)
        {
            throw new InvalidOperationException("You must use one of the IHttpServerResponse.Write* methods.");
        }

        void IOutputStream.End()
        {
            throw new InvalidOperationException("You must use the IHttpServerResponse.End method.");
        }

        public void OnFlushed(IOutputStream stream)
        {
            del.OnFlushed(this);
        }

        #endregion

        bool Write(ArraySegment<byte> data, Action continuation)
        {
            return output.Write(data, continuation);
        }

        void IHttpServerResponse.WriteContinue()
        {
            state.OnWriteContinue();
            Write(new ArraySegment<byte>(Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n")), null);
        }

        void IHttpServerResponse.WriteHeaders(string status, IDictionary<string, string> headers)
        {
            state.EnsureWriteHeaders();

            if (string.IsNullOrEmpty(status))
                throw new ArgumentException("status");

            var spaceSplit = status.Split(' ');
            bool prohibitBody = false;
            int statusCode = 0;
            if (spaceSplit.Length > 1)
                if (int.TryParse(spaceSplit[0], out statusCode))
                {
                    if (statusCode == 204 || statusCode == 304 || (100 <= statusCode && statusCode <= 199))
                        prohibitBody = true;
                }

            state.OnWriteHeaders(prohibitBody);

            this.status = status;
            this.headers = headers;
        }

        bool IHttpServerResponse.WriteBody(ArraySegment<byte> data, Action continuation)
        {
            bool renderHeaders;
            state.EnsureWriteBody(out renderHeaders);
            if (renderHeaders)
            {
                // want to make sure these go out in same packet
                // XXX can we do this better?

                var head = RenderHeaders();
                var headPlusBody = new byte[head.Length + data.Count];
                System.Buffer.BlockCopy(head, 0, headPlusBody, 0, head.Length);
                System.Buffer.BlockCopy(data.Array, data.Offset, headPlusBody, head.Length, data.Count);

                return Write(new ArraySegment<byte>(headPlusBody), continuation);
            }
            else
                return Write(data, continuation);
        }

        void IHttpServerResponse.End()
        {
            bool renderHeaders = false;
            state.OnEnd(out renderHeaders);

            if (renderHeaders)
                Write(new ArraySegment<byte>(RenderHeaders()), null);

            output.End();
        }

        // XXX probably could be optimized
        byte[] RenderHeaders()
        {
            // XXX don't reallocate every time
            var sb = new StringBuilder();

            sb.AppendFormat("HTTP/{0}.{1} {2}\r\n", version.Major, version.Minor, status);

            if (headers == null)
                headers = new Dictionary<string, string>();

            if (!headers.ContainsKey("Server"))
                headers["Server"] = "Kayak";

            if (!headers.ContainsKey("Date"))
                headers["Date"] = DateTime.UtcNow.ToString();

            bool indicateConnection;
            bool indicateConnectionClose;

            state.OnRenderHeaders(
                headers.ContainsKey("Connection"),
                headers["Connection"] == "close",
                out indicateConnection,
                out indicateConnectionClose);

            if (indicateConnection)
                headers["Connection"] = indicateConnectionClose ? "close" : "keep-alive";

            foreach (var pair in headers)
                foreach (var line in pair.Value.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
                    sb.AppendFormat("{0}: {1}\r\n", pair.Key, line);

            sb.Append("\r\n");

            return Encoding.ASCII.GetBytes(sb.ToString());
        }
    }
}
