using System;
using System.Collections.Generic;
using Coroutine;
using System.IO;

namespace Kayak
{
    public delegate void
        OwinApplication(IDictionary<string, object> env,
        Action<string, IDictionary<string, IList<string>>, IObservable<object>> completed,
        Action<Exception> faulted); 

    public static partial class Extensions
    {
        public static void Host(this IKayakServer server, OwinApplication application)
        {
            server.Host(application, null);
        }

        public static void Host(this IKayakServer server, OwinApplication application, Action<Action> trampoline)
        {
            server.HostInternal(application, trampoline).AsContinuation<object>(trampoline)
                (_ => { }, e => {
                    Console.WriteLine("Error while hosting application.");
                    Console.Out.WriteException(e);
                });
        }

        static IEnumerable<object> HostInternal(this IKayakServer server, OwinApplication application, Action<Action> trampoline)
        {
            while (true)
            {
                var accept = new ContinuationState<ISocket>((r, e) => server.OnConnection = s => r(s));
                yield return accept;

                if (accept.Result == null)
                    break;

                accept.Result.ProcessSocket(new HttpSupport(), application, trampoline);
            }
        }

        public static void ProcessSocket(this ISocket socket, OwinApplication application, Action<Action> trampoline)
        {
            socket.ProcessSocketInternal(application).AsContinuation<object>(trampoline)
                (_ => { }, e =>
                {
                    Console.WriteLine("Error while processing request.");
                    Console.Out.WriteException(e);
                });
        }

        static void ProcessSocketInternal2(this ISocket socket, OwinApplication application)
        {
            IHttpParser parser = null;
            IHttpParserObserver observer = new ParserObserver(socket, application);

            socket.OnData = d => 
                {
                    parser.Execute(observer, d);
                };

            socket.OnTimeout = () =>
                {
                    socket.End();
                    socket.Dispose();
                };
        }

        class RequestBodyObservable : IObservable<ArraySegment<byte>>
        {

            public RequestBodyObservable(ISocket socket)
            {
            }

            public void OnBodyData(ArraySegment<byte> data)
            {
            }

            public IDisposable Subscribe(IObserver<ArraySegment<byte>> observer)
            {
                throw new NotImplementedException();
            }
        }

        class ParserObserver : IHttpParserObserver
        {
            ISocket socket;
            OwinApplication app;
            Dictionary<string, object> env;
            RequestBodyObservable requestBodyObservable;

            public ParserObserver(ISocket socket, OwinApplication app)
            {
                this.socket = socket;
                this.app = app;
            }

            public void OnMessageBegin()
            {
            }

            public void OnHeadersComplete(string method, string path, string queryString, IDictionary<string, string> headers)
            {
                env = new Dictionary<string, object>();

                env["Owin.RequestMethod"] = method;
                env["Owin.RequestUri"] = path;

                if (!string.IsNullOrEmpty(queryString))
                    env["Owin.RequestUri"] = (env["Owin.RequestUri"] as string) + "?" + queryString;

                env["Owin.RequestHeaders"] = headers;
                env["Owin.BaseUri"] = "";
                env["Owin.RemoteEndPoint"] = socket.RemoteEndPoint;
                env["Owin.RequestBody"] = requestBodyObservable = new RequestBodyObservable(socket);

                // TODO provide better values
                env["Owin.ServerName"] = "";
                env["Owin.ServerPort"] = 0;
                env["Owin.UriScheme"] = "http";

                // do something with env, like call the owin app
            }

            public void OnBody(ArraySegment<byte> data)
            {
                requestBodyObservable.OnBodyData(data);
            }

            public void OnMessageComplete()
            {
                // 0-length means we're done
                requestBodyObservable.OnBodyData(default(ArraySegment<byte>));
            }

            public void OnError(Exception e)
            {
                throw new NotImplementedException();
            }
        }

        static IEnumerable<object> ProcessSocketInternal(this ISocket socket, OwinApplication application)
        {

            var beginRequest = http.BeginRequest(socket);
            yield return beginRequest;

            var request = beginRequest.Result;

            var invoke = new ContinuationState<Tuple<string, IDictionary<string, IList<string>>, IObservable<object>>>
                ((r, e) => 
                    application(request, 
                                (s,h,b) => 
                                    r(new Tuple<string,IDictionary<string,IList<string>>,IObservable<object>>(s, h, b)), 
                                e));

            yield return invoke;

            var response = invoke.Result;

            new ResponseObserver(socket)
            var subscription = response.Item3.Subscribe();
                

            foreach (var obj in response.Item3)
            {
                
            }

            socket.Dispose();
        }

        class ResponseObserver : IObserver<object>
        {
            ISocket socket;
            IDisposable throttle;

            public ResponseObserver(ISocket socket, IObservable<object> body)
            {
                this.socket = socket;
            }

            public void OnCompleted()
            {
            }

            public void OnError(Exception error)
            {
                Console.WriteLine("Error from response observable.");
                Console.Out.WriteException(error);
            }

            public void OnNext(object obj)
            {
                var objectToWrite = obj;

                if (objectToWrite is FileInfo)
                {
                    socket.Write((objectToWrite as FileInfo).Name);
                    return;
                }

                var chunk = default(ArraySegment<byte>);

                if (obj is ArraySegment<byte>)
                    chunk = (ArraySegment<byte>)obj;
                else if (obj is byte[])
                    chunk = new ArraySegment<byte>(obj as byte[]);
                else
                    return;
                //throw new ArgumentException("Invalid object of type " + obj.GetType() + " '" + obj.ToString() + "'");

                socket.Write(chunk);
            }
        }

    }
}
