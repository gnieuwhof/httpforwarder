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


        public int GetHeaderLength()
        {
            // The header end is the first empty line (CRLF CRLF).
            return this.Bytes.GetEndIndex("\r\n\r\n");
        }

        public int GetContentLength()
        {
            int contentLengthStart = this.Bytes.GetEndIndex("Content-Length: ");

            if (contentLengthStart == -1)
            {
                // There is no Content-Length found.
                return -1;
            }

            // At this point the index points to the space after the double colon so, increase.
            ++contentLengthStart;

            int contentLengthEnd = Array.IndexOf<byte>(this.Bytes, (byte)'\r', contentLengthStart);
            int contentLengthByteCount = contentLengthEnd - contentLengthStart;

            string contentLengthString = Encoding.ASCII.GetString(
                this.Bytes, contentLengthStart, contentLengthByteCount);

            int result;
            if (int.TryParse(contentLengthString, out result))
            {
                return result;
            }

            return -1;
        }

        public void AddBytes(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            this.Bytes = this.Bytes.Append(bytes);
        }
    }
}
