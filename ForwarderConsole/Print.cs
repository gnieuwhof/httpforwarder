namespace ForwarderConsole
{
    using System;
    using System.Text;

    public static class Print
    {
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
            string response = Encoding.ASCII.GetString(bytes);

            InternalPrint("--- RESPONSE ---", response, ConsoleColor.Yellow);
        }

        public static void Color(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;

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
    }
}
