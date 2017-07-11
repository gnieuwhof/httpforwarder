namespace RequestForwarder
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Text;

    public class Forwarder
    {
        private Uri url;

        public Forwarder(Uri forwardUrl)
        {
            this.url = forwardUrl;
        }

        public EventHandler<string> InfoHandler;
        public EventHandler<string> ErrorHandler;
        public EventHandler<byte[]> RequestHandler;
        public EventHandler<byte[]> ResponseHandler;

        public void Start(int port)
        {
            var listener = new TcpListener(IPAddress.Any, port);

            InfoHandler?.Invoke(this, $"Start listening for connections on port: {port}.");
            InfoHandler?.Invoke(this, $"Forwarding requests to: \"{this.url.ToString()}\" (port: {this.url.Port}).");
            listener.Start();

            // Keep accepting clients forever...
            for (;;)
            {
                try
                {
                    InfoHandler?.Invoke(this, "Waiting for client to connect.");
                    using (TcpClient client = listener.AcceptTcpClient())
                    {
                        InfoHandler?.Invoke(this, $"Client connected ({client.Client.RemoteEndPoint}).");
                        HandleClient(client);
                    }
                    InfoHandler?.Invoke(this, "Connection closed.");
                }
                catch (Exception e)
                {
                    ErrorHandler?.Invoke(this, e.ToString());
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            NetworkStream netStream = client.GetStream();

            byte[] header = netStream.ReadFromStream(client.ReceiveBufferSize);

            var webRequest = new WebRequest(header);

            int headerEnd = webRequest.GetHeaderLength();

            if(headerEnd == -1)
            {
                InfoHandler?.Invoke(this, "Client connected but no header found.");

                return;
            }

            int contentLength = webRequest.GetContentLength();

            if ((header.Length == headerEnd + 1) && (contentLength > 0))
            {
                // We only have the header but there is still content to get.
                do
                {
                    var content = netStream.ReadFromStream(client.ReceiveBufferSize, contentLength);
                    webRequest.AddBytes(content);
                }
                while ((contentLength -= client.ReceiveBufferSize) > 0);
            }

            webRequest.ReplaceHost(this.url.Host);
            webRequest.ReplaceConnection("close");

            RequestHandler?.Invoke(this, webRequest.Bytes);

            byte[] serverResponse = SendToServer(webRequest.Bytes);
            
            int byteCount = serverResponse.Length;
            int offset = 0;

            while (byteCount > 0)
            {
                int length = Math.Min(byteCount, client.ReceiveBufferSize);
                netStream.Write(serverResponse, offset, length);
                offset += client.ReceiveBufferSize;
                byteCount -= length;
            }

            ;
        }

        private byte[] SendToServer(byte[] buffer)
        {
            var byteList = new List<byte>();
            byte[] response;
            Stream stream;
            var client = new TcpClient();

            client.Connect(this.url.Host, this.url.Port);
            stream = client.GetStream();

            if (this.url.Scheme == "https")
            {
                var sslStream = new SslStream(stream);
                sslStream.AuthenticateAsClient(this.url.Host);
                stream = sslStream;
            }
            int byteCount = buffer.Length;
            int offset = 0;

            while (byteCount > 0)
            {
                int length = Math.Min(byteCount, client.ReceiveBufferSize);
                stream.Write(buffer, offset, length);
                offset += client.ReceiveBufferSize;
                byteCount -= length;
            }

            response = stream.ReadFromStream(client.ReceiveBufferSize);

            var webResponse = new WebResponse(response);

            int headerEnd = webResponse.GetHeaderLength();

            if(headerEnd != -1)
            {
                int contentLength = webResponse.GetContentLength();
                int totalBytes = contentLength + headerEnd + 1;

                if (response.Length < totalBytes)
                {
                    do
                    {
                        webResponse.AddBytes(stream.ReadFromStream(client.ReceiveBufferSize));
                    }
                    while (webResponse.Bytes.Length < totalBytes);
                }
            }

            ResponseHandler?.Invoke(this, webResponse.Bytes);

            return webResponse.Bytes;
        }
    }
}
