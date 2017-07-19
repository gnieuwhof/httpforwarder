namespace RequestForwarder
{
    using System;
    using System.Text;

    public class WebContext
    {
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
    }
}
