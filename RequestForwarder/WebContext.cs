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

        private IList<byte> GetHeaderValue(string name)
        {
            int start = this.GetHeaderValueStart(name);

            if (start == -1)
            {
                return [];
            }

            int end = this.Bytes.IndexOf((byte)'\r', start);

            return this.Bytes.GetRange(start, end - start);
        }

        // Finds "<name>:" at the start of a header line (case-insensitive)
        // and returns the index of the first byte of its value, or -1.
        private int GetHeaderValueStart(string name)
        {
            int headerLength = this.GetHeaderLength();
            int nameEnd = this.Bytes.GetEndIndex($"\r\n{name.Trim(':', ' ')}:", 0, ignoreCase: true);

            if (nameEnd == -1 || nameEnd > headerLength)
            {
                return -1;
            }

            int start = nameEnd + 1;

            while (this.Bytes[start] == (byte)' ')
            {
                ++start;
            }

            return start;
        }

        private bool ReplaceHeaderValue(string name, string value)
        {
            int start = this.GetHeaderValueStart(name);

            if (start == -1)
            {
                return false;
            }

            int end = this.Bytes.IndexOf((byte)'\r', start);
            IList<byte> result = this.Bytes.Replace(start, end - start, Encoding.ASCII.GetBytes(value));

            this.Bytes.Clear();
            this.Bytes.AddRange(result);

            return true;
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

            if (!this.ReplaceHeaderValue("Host", host))
            {
                this.AddHeader($"Host: {host}");
            }
        }

        public bool ReplaceConnection(string connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            return this.ReplaceHeaderValue("Connection", connection);
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
            int length = this.Bytes.Count - (this.GetHeaderLength() + 1);

            this.ReplaceHeaderValue("Content-Length", length.ToString());
        }
    }
}
