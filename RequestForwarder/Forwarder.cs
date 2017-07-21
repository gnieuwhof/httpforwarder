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
                        HandleClient2(client);
                    }
                    InfoHandler?.Invoke(this, "Connection closed.");
                }
                catch (Exception e)
                {
                    ErrorHandler?.Invoke(this, e.ToString());
                }
            }
        }

        //private void HandleClient(TcpClient client)
        //{
        //    NetworkStream netStream = client.GetStream();

        //    byte[] header = netStream.ReadFromStream(client.ReceiveBufferSize);

        //    var webRequest = new WebRequest(header);

        //    int headerEnd = webRequest.GetHeaderLength();

        //    if (headerEnd == -1)
        //    {
        //        InfoHandler?.Invoke(this, "Client connected but no header found.");

        //        return;
        //    }

        //    int contentLength = webRequest.GetContentLength();
        //    int totalBytes = contentLength + headerEnd + 1;

        //    if(webRequest.Bytes.Length < totalBytes)
        //    {
        //        // We only have the header but there is still content to get.
        //        do
        //        {
        //            byte[] content = netStream.ReadFromStream(client.ReceiveBufferSize, contentLength);
        //            webRequest.AddBytes(content);
        //        }
        //        while (webRequest.Bytes.Length < totalBytes);
        //    }

        //    webRequest.ReplaceHost(this.url.Host);
        //    webRequest.ReplaceConnection("close");

        //    RequestHandler?.Invoke(this, webRequest.Bytes);

        //    byte[] serverResponse = SendToServer(webRequest.Bytes);

        //    int byteCount = serverResponse.Length;
        //    int offset = 0;

        //    while (byteCount > 0)
        //    {
        //        int length = Math.Min(byteCount, client.ReceiveBufferSize);
        //        netStream.Write(serverResponse, offset, length);
        //        offset += client.ReceiveBufferSize;
        //        byteCount -= length;
        //    }
        //}

        private void HandleClient2(TcpClient client)
        {
            NetworkStream netStream = client.GetStream();

            WebContext webRequest = null;

            if (!TryGetHeader(client, netStream, out webRequest))
            {
                InfoHandler?.Invoke(this, "Client connected but no header found.");

                return;
            }

            string transferEncoding = webRequest.GetTransferEncoding();
            int byteCount;
            if (transferEncoding?.Contains("chunked") == true)
            {
                // Get chunks
            }
            else
            {
                int contentLength = webRequest.GetContentLength();
                if (contentLength != -1)
                {
                    byteCount = webRequest.GetHeaderLength() + 1 + contentLength;
                    // Keep reading unit byteCount
                    int bufferSize = client.ReceiveBufferSize;
                    while (byteCount > webRequest.Bytes.Length)
                    {
                        var buffer = new byte[bufferSize];
                        int count = netStream.Read(buffer, 0, bufferSize);

                        var requestPart = new byte[count];
                        Array.Copy(buffer, requestPart, count);

                        webRequest.AddBytes(requestPart);
                    }
                }
            }

            webRequest.ReplaceHost(this.url.Host);
            webRequest.ReplaceConnection("close");

            RequestHandler?.Invoke(this, webRequest.Bytes);

            byte[] serverResponse = SendToServer2(webRequest.Bytes);

            byteCount = serverResponse.Length;
            int offset = 0;

            while (byteCount > 0)
            {
                int length = Math.Min(byteCount, client.ReceiveBufferSize);
                netStream.Write(serverResponse, offset, length);
                offset += client.ReceiveBufferSize;
                byteCount -= length;
            }
        }

        //private byte[] SendToServer(byte[] buffer)
        //{
        //    var byteList = new List<byte>();
        //    byte[] response;
        //    Stream stream;
        //    var client = new TcpClient();

        //    client.Connect(this.url.Host, this.url.Port);
        //    stream = client.GetStream();

        //    if (this.url.Scheme == "https")
        //    {
        //        var sslStream = new SslStream(stream);
        //        sslStream.AuthenticateAsClient(this.url.Host);
        //        stream = sslStream;
        //    }
        //    int byteCount = buffer.Length;
        //    int offset = 0;

        //    while (byteCount > 0)
        //    {
        //        int length = Math.Min(byteCount, client.ReceiveBufferSize);
        //        stream.Write(buffer, offset, length);
        //        offset += client.ReceiveBufferSize;
        //        byteCount -= length;
        //    }

        //    response = stream.ReadFromStream(client.ReceiveBufferSize);

        //    var webResponse = new WebContext(response);

        //    int headerEnd = webResponse.GetHeaderLength();

        //    if (headerEnd != -1)
        //    {
        //        int contentLength = webResponse.GetContentLength();
        //        int totalBytes = contentLength + headerEnd + 1;

        //        if (response.Length < totalBytes)
        //        {
        //            do
        //            {
        //                webResponse.AddBytes(stream.ReadFromStream(client.ReceiveBufferSize));
        //            }
        //            while (webResponse.Bytes.Length < totalBytes);
        //        }
        //    }

        //    ResponseHandler?.Invoke(this, webResponse.Bytes);

        //    return webResponse.Bytes;
        //}

        private byte[] SendToServer2(byte[] buffer)
        {
            var byteList = new List<byte>();
//            byte[] response;
            Stream stream;
            var server = new TcpClient();

            server.Connect(this.url.Host, this.url.Port);
            stream = server.GetStream();

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
                int length = Math.Min(byteCount, server.SendBufferSize);
                stream.Write(buffer, offset, length);
                offset += server.SendBufferSize;
                byteCount -= length;
            }


            WebContext webContext = null;

            if (!TryGetHeader(server, stream, out webContext))
            {
                InfoHandler?.Invoke(this, "Client connected but no header found.");

                return new byte[0];
            }

            string transferEncoding = webContext.GetTransferEncoding();

            int bufferSize = server.ReceiveBufferSize;

            if (transferEncoding?.Contains("chunked") == true)
            {
                var t = webContext.GetHeaderLength() + 1;

                // Get chunks
                for(;;)
                {
                    if(webContext.Bytes.Length <= t)
                    {
                        // Read more...
                        buffer = new byte[bufferSize];
                        int count = stream.Read(buffer, 0, bufferSize);

                        if (count < bufferSize)
                        {
                            byte[] temp = new byte[count];
                            Array.Copy(buffer, temp, count);

                            webContext.AddBytes(temp);
                        }
                        else
                        {
                            webContext.AddBytes(buffer);
                        }
                    }

                    var chSize = webContext.GetChunkSizeBytes(t);
                    
                    string chStr = Encoding.ASCII.GetString(chSize);

                    int schByteCount = Convert.ToInt32(chStr, 16);

                    if(schByteCount == 0)
                    {
                        if (this.ReadUntilBlankLine(server, stream, webContext, t))
                        {
                            // done
                            break;
                        }
                        else
                        {
                            // We've got all the chunkes but geting the final headers or so failed.

                            ErrorHandler?.Invoke(this, "Receiving the headers after the last chunk failed.");

                            ResponseHandler?.Invoke(this, webContext.Bytes);

                            return webContext.Bytes;
                        }
                    }

                    while(t + schByteCount + chSize.Length > webContext.Bytes.Length)
                    {
                        buffer = new byte[bufferSize];
                        int count = stream.Read(buffer, 0, bufferSize);

                        if (count < bufferSize)
                        {
                            byte[] temp = new byte[count];
                            Array.Copy(buffer, temp, count);
                            webContext.AddBytes(temp);
                        }
                        else
                        {
                            webContext.AddBytes(buffer);
                        }
                    }

                    t = t + schByteCount + chSize.Length + 4;
                }
            }
            else
            {
                int contentLength = webContext.GetContentLength();
                if (contentLength != -1)
                {
                    byteCount = webContext.GetHeaderLength() + 1 + contentLength;
                    // Keep reading unit byteCount
                    bufferSize = server.ReceiveBufferSize;
                    while (byteCount > webContext.Bytes.Length)
                    {
                        buffer = new byte[bufferSize];
                        int count = stream.Read(buffer, 0, bufferSize);

                        var requestPart = new byte[count];
                        Array.Copy(buffer, requestPart, count);

                        webContext.AddBytes(requestPart);
                    }
                }
            }


            ResponseHandler?.Invoke(this, webContext.Bytes);


            return webContext.Bytes;



























            //response = stream.ReadFromStream(client.ReceiveBufferSize);

            //var webResponse = new WebContext(response);

            //int headerEnd = webResponse.GetHeaderLength();

            //if (headerEnd != -1)
            //{
            //    int contentLength = webResponse.GetContentLength();
            //    int totalBytes = contentLength + headerEnd + 1;

            //    if (response.Length < totalBytes)
            //    {
            //        do
            //        {
            //            webResponse.AddBytes(stream.ReadFromStream(client.ReceiveBufferSize));
            //        }
            //        while (webResponse.Bytes.Length < totalBytes);
            //    }
            //}

            //ResponseHandler?.Invoke(this, webResponse.Bytes);

            //return webResponse.Bytes;
        }

        private bool TryGetHeader(TcpClient client, Stream netStream, out WebContext result)
        {
            result = null;
            int bufferSize = client.ReceiveBufferSize;

            for (;;)
            {
                var buffer = new byte[bufferSize];
                int count = netStream.Read(buffer, 0, bufferSize);
                
                if (count == 0)
                {
                    return false;
                }

                var received = new byte[count];
                Array.Copy(buffer, received, count);

                if (result == null)
                {
                    result = new WebContext(received);
                }
                else
                {
                    result.AddBytes(received);
                }

                if (result.GetHeaderLength() != -1)
                {
                    // Header received.
                    return true;
                }
            }
        }

        private bool ReadUntilBlankLine(
            TcpClient client,
            Stream stream,
            WebContext webContext,
            int startIndex
            )
        {
            int bufferSize = client.ReceiveBufferSize;

            while (webContext.Bytes.GetEndIndex("\r\n\r\n", startIndex) == -1)
            {
                var buffer = new byte[bufferSize];
                int count = stream.Read(buffer, 0, bufferSize);

                if (count == 0)
                {
                    // Give up...
                    return false;
                }

                var received = new byte[count];
                Array.Copy(buffer, received, count);

                webContext.AddBytes(received);
            }

            return true;
        }
    }
}
