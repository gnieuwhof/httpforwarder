namespace RequestForwarder
{
    using System;
    using System.Text;

    public class WebRequest : WebContext
    {
        public WebRequest(byte[] firstBytes)
            : base(firstBytes)
        {
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
