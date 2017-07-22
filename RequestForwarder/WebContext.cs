namespace RequestForwarder
{
    using System;
    using System.Text;

    public class WebContext
    {
        public WebContext()
        {
            this.Bytes = new byte[0];
        }

        public WebContext(byte[] firstBatch)
        {
            if (firstBatch == null)
                throw new ArgumentNullException(nameof(firstBatch));

            this.Bytes = firstBatch;
        }


        public byte[] Bytes
        {
            get;
            protected set;
        }

#if DEBUG
        public string Content
        {
            get
            {
                return Encoding.ASCII.GetString(this.Bytes);
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
            byte[] contentLengthBytes = GetHeaderValue("Content-Length");

            if (contentLengthBytes.Length == 0)
            {
                // Content-Length not found.
                return -1;
            }

            string contentLengthString = Encoding.ASCII.GetString(contentLengthBytes);
            
            int result;
            if (int.TryParse(contentLengthString, out result))
            {
                return result;
            }

            return -1;
        }

        public string GetTransferEncoding()
        {
            byte[] transferEncodingBytes = this.GetHeaderValue("Transfer-Encoding");

            string result = Encoding.ASCII.GetString(transferEncodingBytes);

            return result;
        }

        private byte[] GetHeaderValue(string header)
        {
            header = header.Trim(new[] { ':', ' ' });
            header += ": ";

            int headerValueStart = this.Bytes.GetEndIndex(header);

            if(headerValueStart == -1)
            {
                // Header not found.
                return new byte[0];
            }

            // At this point the index points to the space after the double colon so, increase.
            ++headerValueStart;

            int headerValueEnd = Array.IndexOf<byte>(this.Bytes, (byte)'\r', headerValueStart);
            int headerValueLength = (headerValueEnd - headerValueStart);

            byte[] result = new byte[headerValueLength];

            Array.Copy(this.Bytes, headerValueStart, result, 0, headerValueLength);

            return result;
        }

        public void AddBytes(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            this.Bytes = this.Bytes.Append(bytes);
        }
            
        public byte[] GetChunkSizeBytes(int position)
        {
            int endPos = Array.IndexOf<byte>(this.Bytes, (byte)'\r', position);

            int chunkLength = (endPos - position);

            byte[] chunkSizeBytes = new byte[chunkLength];

            Array.Copy(this.Bytes, position, chunkSizeBytes, 0, chunkLength);

            return chunkSizeBytes;
        }


        public void ReplaceHost(string host)
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));

            int hostStart = this.Bytes.GetEndIndex("Host: ") + 1;
            int hostEnd = Array.IndexOf<byte>(this.Bytes, (byte)'\r', hostStart);
            int hostLength = hostEnd - hostStart;

            byte[] hostBytes = Encoding.ASCII.GetBytes(host);

            byte[] result = this.Bytes.Overwrite(hostStart, hostLength, hostBytes);

            this.Bytes = result;
        }

        public void ReplaceConnection(string connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            int connectionStart = this.Bytes.GetEndIndex("Connection: ") + 1;
            int connectionEnd = Array.IndexOf<byte>(this.Bytes, (byte)'\r', connectionStart);
            int connectionLength = connectionEnd - connectionStart;

            byte[] connectionBytes = Encoding.ASCII.GetBytes(connection);

            byte[] result = this.Bytes.Overwrite(connectionStart, connectionLength, connectionBytes);

            this.Bytes = result;
        }
    }
}
