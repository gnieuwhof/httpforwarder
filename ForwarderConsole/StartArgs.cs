namespace ForwarderConsole
{
    using System;

    public class StartArgs
    {
        private string[] args;


        public StartArgs(string[] args)
        {
            this.args = args;
        }


        public int Port
        {
            get;
            private set;
        }

        public Uri URL
        {
            get;
            private set;
        }


        public EventHandler<string> ErrorHandler;


        public bool Process()
        {
            int port;
            bool portSuccess = this.GetPortFromArgs(this.args, out port);

            Uri url;
            bool urlSuccess = this.GetUrlFromArgs(this.args, out url);

            this.Port = port;
            this.URL = url;

            return (portSuccess && urlSuccess);
        }

        private bool GetPortFromArgs(string[] args, out int result)
        {
            result = 0;

            if (args.Length == 0)
            {
                ErrorHandler?.Invoke(null, "No args found (two expected).");
            }
            else
            {
                if (int.TryParse(args[0], out result))
                {
                    if (result > 0 && result < 65536)
                    {
                        return true;
                    }
                    ErrorHandler?.Invoke(null, "Invalid port found.");
                }
                else
                {
                    ErrorHandler?.Invoke(null, "Could not parse URL.");
                }
            }

            return false;
        }

        private bool GetUrlFromArgs(string[] args, out Uri result)
        {
            result = null;

            if (args.Length == 0 || args.Length == 1)
            {
                ErrorHandler?.Invoke(null, "To few start args found (two expected).");
            }
            else
            {
                if (Uri.TryCreate(args[1], UriKind.Absolute, out result))
                {
                    return true;
                }
                else
                {
                    ErrorHandler?.Invoke(null, "Could not parse URL.");
                }
            }

            return false;
        }
    }
}
