namespace ForwarderConsole
{
    using RequestForwarder;
    using System;

    class Program
    {
        static void Main(string[] args)
        {
            var startArgs = new StartArgs(args);

            startArgs.ErrorHandler += HandleError;

            if (!startArgs.Process())
            {
                Print.Color("Example:", ConsoleColor.Gray);
                Print.Color("ForwarderConsole.exe 8080 https://google.com:443", ConsoleColor.Gray);
                Print.Color("First argument is to port to listen on.", ConsoleColor.Gray);
                Print.Color("Second argument is the forwarding URL (only scheme, host and port are used).", ConsoleColor.Gray);
                Print.Color("Press a key to close...", ConsoleColor.Gray);
                Console.ReadKey();
                return;
            }

            var forwarder = new Forwarder(startArgs.URL);

            forwarder.InfoHandler += HandleInfo;
            forwarder.ErrorHandler += HandleError;
            forwarder.RequestHandler += HandleRequest;
            forwarder.ResponseHandler += HandleResponse;

            forwarder.Start(startArgs.Port);
        }

        private static void HandleInfo(object sender, string info)
        {
            Print.Color(info, ConsoleColor.Gray);
        }
        private static void HandleError(object sender, string error)
        {
            Print.Color(error, ConsoleColor.Red);
        }
        private static void HandleRequest(object sender, byte[] request)
        {
            Print.Request(request);
        }
        private static void HandleResponse(object sender, byte[] response)
        {
            Print.Response(response);
        }
    }
}
