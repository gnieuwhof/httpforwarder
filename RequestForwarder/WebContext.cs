namespace RequestForwarder
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;

    public class WebContext
    {
        public WebContext()
        {
            this.Bytes = new List<byte>();
            this.BC = new BlockingCollection<byte[]>();
        }

        public WebContext(byte[] firstBatch)
            : this()
        {
            if (firstBatch == null)
                throw new ArgumentNullException(nameof(firstBatch));

            this.Bytes.AddRange(firstBatch);
        }


        public List<byte> Bytes { get; }
        public BlockingCollection<byte[]> BC { get; }

#if DEBUG
        public string Content
        {
            get
            {
                return Encoding.ASCII.GetString(this.Bytes.ToArray());
            }
        }
#endif


        public int GetHeaderLength()
        {
            // The header end is the first empty line (CRLF CRLF).
            return this.Bytes.GetEndIndex("\r\n\r\n");
        }

        public int GetContentLength()
        {
            IList<byte> contentLengthBytes = GetHeaderValue("Content-Length");

            if (contentLengthBytes.Count == 0)
            {
                // Content-Length not found.
                return -1;
            }

            string contentLengthString = Encoding.ASCII.GetString(contentLengthBytes.ToArray());

            int result;
            if (int.TryParse(contentLengthString, out result))
            {
                return result;
            }

            return -1;
        }

        public string GetTransferEncoding()
        {
            IList<byte> transferEncodingBytes = this.GetHeaderValue("Transfer-Encoding");

            string result = Encoding.ASCII.GetString(transferEncodingBytes.ToArray());

            return result;
        }

        private IList<byte> GetHeaderValue(string header)
        {
            header = header.Trim(new[] { ':', ' ' });
            header += ": ";

            int headerValueStart = this.Bytes.GetEndIndex(header);

            int headerLength = GetHeaderLength();

            if ((headerValueStart > headerLength) || (headerValueStart == -1))
            {
                // Header not found.
                return new byte[0];
            }

            // At this point the index points to the space after the double colon so, increase.
            ++headerValueStart;

            int headerValueEnd = this.Bytes.IndexOf((byte)'\r', headerValueStart);
            int headerValueLength = (headerValueEnd - headerValueStart);

            List<byte> result = this.Bytes.GetRange(headerValueStart, headerValueLength);

            return result;
        }

        public void AddBytes(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            this.BC.Add(bytes);
            this.Bytes.AddRange(bytes);
        }

        public IList<byte> GetChunkSizeBytes(int position)
        {
            int endPos = this.Bytes.IndexOf((byte)'\r', position);

            int chunkLength = (endPos - position);

            IList<byte> chunkSizeBytes = this.Bytes.GetRange(position, chunkLength);

            return chunkSizeBytes;
        }


        public void ReplaceHost(string host)
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));

            int hostStart = this.Bytes.GetEndIndex("Host: ") + 1;
            int hostEnd = this.Bytes.IndexOf((byte)'\r', hostStart);
            int hostLength = hostEnd - hostStart;

            byte[] hostBytes = Encoding.ASCII.GetBytes(host);

            IList<byte> result = this.Bytes.Replace(hostStart, hostLength, hostBytes);

            this.Bytes.Clear();
            this.Bytes.AddRange(result);
        }

        public bool ReplaceConnection(string connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            int connectionStart = this.Bytes.GetEndIndex("Connection: ") + 1;

            if (connectionStart == 0)
            {
                return false;
            }

            int connectionEnd = this.Bytes.IndexOf((byte)'\r', connectionStart);
            int connectionLength = connectionEnd - connectionStart;

            byte[] connectionBytes = Encoding.ASCII.GetBytes(connection);

            IList<byte> result = this.Bytes.Replace(connectionStart, connectionLength, connectionBytes);

            this.Bytes.Clear();
            this.Bytes.AddRange(result);

            return true;
        }

        public void AddHeader(string line)
        {
            if (line == null)
                throw new ArgumentNullException(nameof(line));

            int headerEnd = this.GetHeaderLength() - 3;

            byte[] newConnection = Encoding.ASCII.GetBytes($"\r\n{line}");
            IList<byte> result = this.Bytes.Replace(headerEnd, 0, newConnection);

            this.Bytes.Clear();
            this.Bytes.AddRange(result);
        }

        public void ReplaceIPAddressWithHost(int listeningPort, string schemeAndHost)
        {
            int startIndex = this.GetHeaderLength();

            while (true)
            {
                int schemaEnd = this.Bytes.GetEndIndex("http://", startIndex);

                if (schemaEnd == -1)
                {
                    break;
                }

                string port = $":{listeningPort}";
                int portEnd = this.Bytes.GetEndIndex(port, schemaEnd);

                if (portEnd != -1)
                {
                    int portStart = (portEnd - port.Length);
                    int hostLength = (portStart - schemaEnd);

                    int schemaStart = schemaEnd - "http://".Length;

                    int length = portEnd - schemaStart;

                    if ((portStart != -1) &&
                        (hostLength < "123.123.123.123".Length)
                        )
                    {
                        byte[] hostBytes = this.Bytes
                            .GetRange(schemaEnd + 1, hostLength)
                            .ToArray();

                        string host = Encoding.ASCII.GetString(hostBytes);

                        if(IPAddress.TryParse(host, out IPAddress address))
                        {
                            hostBytes = Encoding.ASCII.GetBytes(schemeAndHost);

                            IList<byte> result = this.Bytes.Replace(schemaStart + 1, length, hostBytes);

                            this.Bytes.Clear();
                            this.Bytes.AddRange(result);
                        }
                    }

                }

                startIndex = schemaEnd + 1;
            }
        }

        public void SetContentLength()
        {
            int contentStart = (this.GetHeaderLength() + 1);
            
            int length = this.Bytes.Count - contentStart;


            int contentLengthStart = this.Bytes.GetEndIndex("Content-Length: ") + 1;

            if (contentLengthStart == 0)
            {
                return;
            }

            int contentLengthEnd = this.Bytes.IndexOf((byte)'\r', contentLengthStart);
            int contentLegnthLength = contentLengthEnd - contentLengthStart;

            byte[] contentLengthBytes = Encoding.ASCII.GetBytes($"{length}");

            IList<byte> result = this.Bytes.Replace(contentLengthStart, contentLegnthLength, contentLengthBytes);

            this.Bytes.Clear();
            this.Bytes.AddRange(result);
        }
    }
}
