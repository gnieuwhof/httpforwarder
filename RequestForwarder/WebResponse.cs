namespace RequestForwarder
{
    using System;
    using System.Text;

    public class WebResponse
    {
        public WebResponse(byte[] header)
        {
            if (header == null)
                throw new ArgumentNullException(nameof(header));

            this.Bytes = header;
        }


        public byte[] Bytes
        {
            get;
            private set;
        }


        public int GetHeaderLength()
        {
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

            var temp = new byte[this.Bytes.Length + bytes.Length];

            Array.Copy(this.Bytes, temp, this.Bytes.Length);
            Array.Copy(bytes, 0, temp, this.Bytes.Length, bytes.Length);

            this.Bytes = temp;
        }
    }
}
