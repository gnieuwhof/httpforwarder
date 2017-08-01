namespace RequestForwarder
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
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

            if (headerValueStart == -1)
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

        public void ReplaceConnection(string connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            int connectionStart = this.Bytes.GetEndIndex("Connection: ") + 1;
            int connectionEnd = this.Bytes.IndexOf((byte)'\r', connectionStart);
            int connectionLength = connectionEnd - connectionStart;

            byte[] connectionBytes = Encoding.ASCII.GetBytes(connection);

            IList<byte> result = this.Bytes.Replace(connectionStart, connectionLength, connectionBytes);

            this.Bytes.Clear();
            this.Bytes.AddRange(result);
        }
    }
}
