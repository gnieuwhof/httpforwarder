namespace RequestForwarder
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    public static class Extensions
    {
        public static byte[] ReadFromStream(this Stream stream, int bufferSize, int byteCount = -1)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize),
                    "Buffer size must be greater than zero.");

            var requestBuffer = new List<byte>();

            for (;;)
            {
                var buffer = new byte[bufferSize];
                int count = stream.Read(buffer, 0, bufferSize);

                var requestPart = new byte[count];
                Array.Copy(buffer, requestPart, count);
                requestBuffer.AddRange(requestPart);

                byteCount -= count;

                if ((count < bufferSize) || (byteCount == 0))
                {
                    // Done reading.
                    break;
                }
            }

            return requestBuffer.ToArray();
        }

        public static int GetEndIndex(this byte[] haystack, string needle)
        {
            if (haystack == null)
                throw new ArgumentNullException(nameof(haystack));
            if (needle == null)
                throw new ArgumentNullException(nameof(needle));

            byte[] needleBytes = Encoding.ASCII.GetBytes(needle);

            return haystack.GetEndIndex(needleBytes);
        }

        public static int GetEndIndex(this byte[] haystack, byte[] needle)
        {
            if (needle == null)
                throw new ArgumentNullException(nameof(needle));
            if (haystack == null)
                throw new ArgumentNullException(nameof(haystack));

            if (needle.Length <= haystack.Length)
            {
                int needleIndex = 0;
                for (int i = 0; i < haystack.Length; ++i)
                {
                    if (needle[needleIndex] == haystack[i])
                    {
                        ++needleIndex;
                    }
                    else
                    {
                        // Reset
                        needleIndex = 0;
                    }

                    if (needleIndex == needle.Length)
                    {
                        // Done
                        return i;
                    }
                }
            }

            return -1;
        }
    }
}
