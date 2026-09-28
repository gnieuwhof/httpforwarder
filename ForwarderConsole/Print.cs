namespace ForwarderConsole
{
    using RequestForwarder;
    using System;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Text;

    public static class Print
    {
        private const string HeaderEnd = "\r\n\r\n";

        public static void Error(string error)
        {
            InternalPrint("--- ERROR --", error, ConsoleColor.Red);
        }

        public static void Request(byte[] bytes)
        {
            string request = Encoding.ASCII.GetString(bytes);

            InternalPrint("--- REQUEST ---", request, ConsoleColor.White);
        }

        public static void Response(byte[] bytes)
        {
            InternalPrint("--- RESPONSE ---", ToReadable(bytes), ConsoleColor.Yellow);
        }

        public static void Color(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;

            // Replace the BELL character.
            message = message.Replace('\a', 'B');

            Console.WriteLine(message);
        }
        
        private static void InternalPrint(string header, string message, ConsoleColor color)
        {
            string txt =
                Line(header) +
                Line() +
                Line(message) +
                Line("==================================================");

            Print.Color(txt, color);
        }

        private static string Line(string txt = "")
        {
            return txt + Environment.NewLine;
        }

        private static string ToReadable(byte[] bytes)
        {
            string raw = Encoding.ASCII.GetString(bytes);

            int headerEnd = bytes.GetEndIndex(HeaderEnd);

            if (headerEnd == -1)
            {
                return raw;
            }

            string headers = raw[..(headerEnd + 1 - HeaderEnd.Length)];

            if (!HasHeaderValue(headers, "Content-Encoding", "gzip"))
            {
                return raw;
            }

            try
            {
                byte[] body = bytes[(headerEnd + 1)..];

                if (HasHeaderValue(headers, "Transfer-Encoding", "chunked"))
                {
                    body = Dechunk(body);
                }

                return headers + HeaderEnd + DecompressGzip(body);
            }
            catch (Exception e)
            {
                return raw + Environment.NewLine + $"[gzip body could not be decoded: {e.Message}]";
            }
        }

        private static bool HasHeaderValue(string headers, string name, string value)
        {
            foreach (string line in headers.Split("\r\n"))
            {
                int colon = line.IndexOf(':');

                if (colon == -1)
                {
                    continue;
                }

                if (line.AsSpan(0, colon).Trim().Equals(name, StringComparison.OrdinalIgnoreCase) &&
                    line.AsSpan(colon + 1).Contains(value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static byte[] Dechunk(ReadOnlySpan<byte> body)
        {
            using var result = new MemoryStream();
            int index = 0;

            while (true)
            {
                int lineEnd = body[index..].IndexOf("\r\n"u8);

                if (lineEnd == -1)
                {
                    throw new InvalidDataException("Chunk size line is not terminated.");
                }

                ReadOnlySpan<byte> sizeLine = body.Slice(index, lineEnd);
                int extension = sizeLine.IndexOf((byte)';');

                if (extension != -1)
                {
                    sizeLine = sizeLine[..extension];
                }

                int chunkSize = int.Parse(Encoding.ASCII.GetString(sizeLine).Trim(), NumberStyles.HexNumber);
                index += lineEnd + 2;

                if (chunkSize == 0)
                {
                    break;
                }

                result.Write(body.Slice(index, chunkSize));
                index += chunkSize + 2;
            }

            return result.ToArray();
        }

        private static string DecompressGzip(byte[] gzipData)
        {
            using var compressed = new MemoryStream(gzipData);
            using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
            using var result = new MemoryStream();
            gzip.CopyTo(result);
            return Encoding.UTF8.GetString(result.GetBuffer(), 0, (int)result.Length);
        }
    }
}
