namespace RequestForwarder
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;

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
                        HandleRequest(client);
                    }
                    InfoHandler?.Invoke(this, "Connection closed.");
                }
                catch (Exception e)
                {
                    ErrorHandler?.Invoke(this, e.ToString());
                }
            }
        }

        private void HandleRequest(TcpClient client)
        {
            NetworkStream netStream = client.GetStream();
            int bufferSize = client.ReceiveBufferSize;
            WebContext webContext = null;

            if (!TryGetHeader(netStream, bufferSize, out webContext))
            {
                InfoHandler?.Invoke(this, "Client connected but no header found.");
                return;
            }

            webContext.ReplaceHost(this.url.Host);
            webContext.ReplaceConnection("close");

            string transferEncoding = webContext.GetTransferEncoding();

            if (transferEncoding?.Contains("chunked") == true)
            {
                bool success = this.ReadChunked(netStream, bufferSize, webContext);

                if (!success)
                {
                    InfoHandler?.Invoke(this, "Receiving headers after the last chunk failed.");
                }
            }
            else
            {
                int contentLength = webContext.GetContentLength();
                if (contentLength != -1)
                {
                    this.ReadFromStream(netStream, bufferSize, webContext, contentLength);
                }
            }

            RequestHandler?.Invoke(this, webContext.Bytes.ToArray());

            HandleResponse(webContext.Bytes.ToArray(), netStream, client.SendBufferSize);
        }

        private void HandleResponse(byte[] buffer, Stream clientStream, int clientSendBufferSize)
        {
            using (var server = new TcpClient())
            {
                server.Connect(this.url.Host, this.url.Port);
                Stream stream = server.GetStream();

                if (this.url.Scheme == "https")
                {
                    var sslStream = new SslStream(stream);
                    sslStream.AuthenticateAsClient(this.url.Host);
                    stream = sslStream;
                }

                this.WriteToStream(stream, server.SendBufferSize, buffer);

                WebContext webContext = null;
                int receiveBufferSize = server.ReceiveBufferSize;

                if (!TryGetHeader(stream, receiveBufferSize, out webContext))
                {
                    InfoHandler?.Invoke(this, "Client connected but no header found.");
                    return;
                }

                var bc = new BlockingCollection<byte[]>();

                bc.Add(webContext.Bytes.ToArray());


                var consumer = Task.Factory.StartNew(() =>
                {
                    foreach (var item in bc.GetConsumingEnumerable())
                    {
                        this.WriteToStream(clientStream, clientSendBufferSize, item);
                    }
                });


                string transferEncoding = webContext.GetTransferEncoding();
                if (transferEncoding?.Contains("chunked") == true)
                {
                    ReadChunked(stream, server.ReceiveBufferSize, webContext, bc);
                }
                else
                {
                    int contentLength = webContext.GetContentLength();
                    if (contentLength != -1)
                    {
                        int bytesToRead = webContext.GetHeaderLength() + 1 + contentLength;
                        ReadFromStream(stream, server.ReceiveBufferSize, bc, bytesToRead, webContext);
                    }
                }

                ResponseHandler?.Invoke(this, webContext.Bytes.ToArray());

                bc.CompleteAdding();

                consumer.Wait();
            }
        }

        private int ReadFromStream(
            Stream stream,
            int bufferSize,
            BlockingCollection<byte[]> bc,
            int bytesToRead,
            WebContext webContext
            )
        {
            int bytesRead = 0;

            // Keep reading until all the bytes are read
            // or when we receive nothing, give up.

            while (webContext.Bytes.Count < bytesToRead)
            {
                var buffer = new byte[bufferSize];
                int count = stream.Read(buffer, 0, bufferSize);

                if (count == 0)
                {
                    // Give up.
                    break;
                }

                bytesRead += count;

                buffer = buffer.Take(count);

                bc.Add(buffer);
                webContext.AddBytes(buffer);
            }

            return bytesRead;
        }

        private void ReadChunked(
            Stream stream,
            int bufferSize,
            WebContext webContext,
            BlockingCollection<byte[]> bc
            )
        {
            int index = webContext.GetHeaderLength() + 1;
            byte[] buffer = new byte[bufferSize];
            int bytesRead = webContext.Bytes.Count;

            if (bytesRead == index)
            {
                // Read more...
                this.ReadFromStream(stream, bufferSize, webContext);

                int count = (webContext.Bytes.Count - bytesRead);
                byte[] temp = webContext.Bytes.GetRange(bytesRead, count).ToArray();

                bc.Add(temp);
            }

            // Get chunks
            for (;;)
            {
                byte[] chunkSizeBytes = webContext.GetChunkSizeBytes(index).ToArray();
                string chunkSizeString = Encoding.ASCII.GetString(chunkSizeBytes);
                int chunkSize = Convert.ToInt32(chunkSizeString, 16);

                if (chunkSize == 0)
                {
                    int endPos = webContext.Bytes.GetEndIndex("\r\n\r\n", index);

                    if (endPos != -1)
                    {
                        // Done
                        break;
                    }

                    int readSoFar = webContext.Bytes.Count;
                    // It could be that the client has some additional headers for us.
                    // Let's find out...
                    if (this.ReadUntilBlankLineReceived(stream, bufferSize, bc))
                    {
                        // Addition headers received, if any.
                        break;
                    }
                    else
                    {
                        // We've got all the chunkes but getting the final headers, or so, failed.
                        ErrorHandler?.Invoke(this, "Receiving the headers after the last chunk failed.");
                        ResponseHandler?.Invoke(this, webContext.Bytes.ToArray());

                        return;
                    }
                }

                int read = this.ReadFromStream(
                    stream,
                    bufferSize,
                    bc,
                    index + chunkSize + chunkSizeBytes.Length,
                    webContext
                    );

                index = index + chunkSize + chunkSizeBytes.Length + 4;
            }

            return;
        }

        private bool ReadUntilBlankLineReceived(
            Stream stream,
            int bufferSize,
            BlockingCollection<byte[]> bc
            )
        {
            return ReadFromStream(
                stream,
                bufferSize,
                bc,
                (byte[] buffer) => { return (buffer.GetEndIndex("\r\n\r\n") != -1); }
                );
        }

        private bool ReadFromStream(
            Stream stream,
            int bufferSize,
            BlockingCollection<byte[]> bc,
            Func<byte[], bool> isDone
            )
        {
            var buffer = new byte[bufferSize];

            while (!isDone(buffer))
            {
                int count = stream.Read(buffer, 0, bufferSize);

                if (count == 0)
                {
                    // Give up.
                    return false;
                }

                buffer = buffer.Take(count);

                ResponseHandler?.Invoke(this, buffer);
                bc.Add(buffer);
            }

            return true;
        }

        private int ReadFromStream(
            Stream stream,
            int bufferSize,
            WebContext webContext,
            int contentLength = -1
            )
        {
            bool readOnce = (contentLength < 0);
            int bytesReceived = 0;

            int byteCount = readOnce ?
                0 :
                webContext.GetHeaderLength() + 1 + contentLength;

            // Keep reading until byteCount
            // or just once if no content length is passed.
            while ((byteCount > webContext.Bytes.Count) || readOnce)
            {
                var buffer = new byte[bufferSize];
                int count = stream.Read(buffer, 0, bufferSize);
                bytesReceived += count;

                buffer = buffer.Take(count);

                webContext.AddBytes(buffer);

                if ((count == 0) || readOnce)
                {
                    break;
                }
            }

            return bytesReceived;
        }

        private void WriteToStream(Stream stream, int bufferSize, byte[] content)
        {
            int byteCount = content.Length;
            int offset = 0;

            while (byteCount > 0)
            {
                int length = Math.Min(byteCount, bufferSize);
                stream.Write(content, offset, length);
                offset += bufferSize;
                byteCount -= length;
            }
        }

        private bool ReadChunked(Stream stream, int bufferSize, WebContext webContext)
        {
            int index = webContext.GetHeaderLength() + 1;
            byte[] buffer = new byte[bufferSize];

            // Get chunks
            for (;;)
            {
                if (webContext.Bytes.Count <= index)
                {
                    // Read more...
                    this.ReadFromStream(stream, bufferSize, webContext);
                }

                byte[] chunkSizeBytes = webContext.GetChunkSizeBytes(index).ToArray();
                string chunkSizeString = Encoding.ASCII.GetString(chunkSizeBytes);
                int chunkSize = Convert.ToInt32(chunkSizeString, 16);

                if (chunkSize == 0)
                {
                    // It could be that the client has some additional headers for us.
                    // Let's find out...
                    if (this.ReadUntilBlankLineReceived(stream, bufferSize, webContext, index))
                    {
                        // Addition headers receiver, if any.
                        break;
                    }
                    else
                    {
                        // We've got all the chunkes but getting the final headers, or so, failed.
                        ErrorHandler?.Invoke(this, "Receiving the headers after the last chunk failed.");
                        ResponseHandler?.Invoke(this, webContext.Bytes.ToArray());

                        return false;
                    }
                }

                while ((index + chunkSize + chunkSizeBytes.Length) > webContext.Bytes.Count)
                {
                    this.ReadFromStream(stream, bufferSize, webContext);
                }

                index = index + chunkSize + chunkSizeBytes.Length + 4;
            }

            return true;
        }

        private bool TryGetHeader(Stream stream, int bufferSize, out WebContext result)
        {
            result = new WebContext();

            for (;;)
            {
                int count = this.ReadFromStream(stream, bufferSize, result);

                if (count == 0)
                {
                    // Give up...
                    return false;
                }

                if (result.GetHeaderLength() != -1)
                {
                    // Header received.
                    return true;
                }
            }
        }

        private bool ReadUntilBlankLineReceived(
            Stream stream,
            int bufferSize,
            WebContext webContext,
            int startIndex
            )
        {
            while (webContext.Bytes.GetEndIndex("\r\n\r\n", startIndex) == -1)
            {
                int count = this.ReadFromStream(stream, bufferSize, webContext);

                if (count == 0)
                {
                    // Give up...
                    return false;
                }
            }

            return true;
        }
    }
}
