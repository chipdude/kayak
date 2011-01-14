using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Http
{
    public class Parser
    {
        public string Method;
        public string Path;
        public string QueryString;
        public string Fragment;
        public Dictionary<string, string> Headers;
        Action<ArraySegment<byte>> onData;

        public Parser(ISocket socket, Action<ArraySegment<byte>> onData)
        {
            // will be called whenever the parser encounters HTTP request data.
            this.onData = onData;

            var parser = new HttpParser(ParserType.HTTP_REQUEST);
            Headers = new Dictionary<string, string>();

            // terrible! why not provide an interface IParserEventHandler?
            var settings = new ParserSettings();
            settings.OnMessageBegin = OnMessageBegin;
            settings.OnPath = OnPath;
            settings.OnQueryString = OnQueryString;
            settings.OnFragment = OnFragment;
            settings.OnHeaderField = OnHeaderField;
            settings.OnHeaderValue = OnHeaderValue;
            settings.OnHeadersComplete = OnHeadersComplete;
            settings.OnBody = OnBody;
            settings.OnMessageComplete = OnMessageComplete;
            settings.OnError = OnError;

            // this gets called whenever data is available on the socket--we feed the data to the parser
            socket.OnData = d => parser.Execute(settings, new ByteBuffer(d.Array, d.Offset, d.Count));
        }

        public int OnMessageBegin(HttpParser parser)
        {
            // possibly push a data structure which collects the data we parse into a queue for handling by the app, 
            // save a reference to it, etc.
            return 0;
        }

        StringBuilder path = new StringBuilder();
        public int OnPath(HttpParser parser, ByteBuffer data, int pos, int len)
        {
            path.Append(Encoding.ASCII.GetString(data.Bytes, pos, len));
            return 0;
        }

        StringBuilder queryString = new StringBuilder();
        public int OnQueryString(HttpParser parser, ByteBuffer data, int pos, int len)
        {
            queryString.Append(Encoding.ASCII.GetString(data.Bytes, pos, len));
            return 0;
        }

        StringBuilder fragment = new StringBuilder();
        public int OnFragment(HttpParser parser, ByteBuffer data, int pos, int len)
        {
            fragment.Append(Encoding.ASCII.GetString(data.Bytes, pos, len));
            return 0;
        }

        StringBuilder headerField = new StringBuilder();
        public int OnHeaderField(HttpParser parser, ByteBuffer data, int pos, int len)
        {
            if (headerValue.Length != 0)
                CommitHeader();

            headerField.Append(Encoding.ASCII.GetString(data.Bytes, pos, len));
            return 0;

        }

        StringBuilder headerValue = new StringBuilder();
        public int OnHeaderValue(HttpParser parser, ByteBuffer data, int pos, int len)
        {
            if (headerField.Length == 0)
                throw new HttpException("Got header value without field name.");

            headerValue.Append(Encoding.ASCII.GetString(data.Bytes, pos, len));
            return 0;
        }

        public int OnHeadersComplete(HttpParser parser)
        {
            if (headerField.Length > 0)
                CommitHeader();

            Method = parser.HttpMethod.ToString().Replace("HTTP_", "");
            Path = path.ToString();
            QueryString = queryString.ToString();
            Fragment = fragment.ToString();



            return 0;
        }

        void CommitHeader()
        {
            var name = headerField.ToString();
            var value = headerValue.ToString();

            if (Headers.ContainsKey(name))
                Headers[name] += ", " + value;
            else
                Headers[name] = value;

            headerField.Length = 0; headerValue.Length = 0;
        }

        public int OnBody(HttpParser parser, ByteBuffer data, int pos, int len)
        {
            onData(new ArraySegment<byte>(data.Bytes, pos, len));
            return 0;
        }

        public int OnMessageComplete(HttpParser parser)
        {
            // parser will automatically restart if more data is recieved on the socket
            return 0;
        }

        public void OnError(HttpParser parser, string message, ByteBuffer buffer, int initial_position)
        {
            // who cares?

            // TODO: handle error D:
        }
    }
}
