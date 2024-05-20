namespace IPK24ChatClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Default values
            IChatCommunicator chatCommunicator;
            string? protocol = null;
            string? serverAddress = null;
            int serverPort = 4567;
            int udpTimeout = 250; // milliseconds
            int udpRetries = 3;

            // Parse command-line arguments
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-t":
                        protocol = args[++i].ToLower();
                        break;
                    case "-s":
                        serverAddress = args[++i];
                        break;
                    case "-p":
                        serverPort = int.Parse(args[++i]);
                        break;
                    case "-d":
                        udpTimeout = int.Parse(args[++i]);
                        break;
                    case "-r":
                        udpRetries = int.Parse(args[++i]);
                        break;
                    case "-h":
                        PrintHelp();
                        return;
                    default:
                        throw new ArgumentException($"Invalid argument: {args[i]}");
                }
            }

            // Validate required arguments
            if (protocol == null || serverAddress == null)
            {
                Console.Error.WriteLine("Error: Missing required arguments. Usage: IPK24ChatClient -t [tcp/udp] -s [server address]");
                return;
            }

            // Validate protocol option
            if (protocol != "tcp" && protocol != "udp")
            {
                Console.Error.WriteLine("Error: Invalid protocol specified. Use 'tcp' or 'udp'.");
                return;
            }

            if (protocol == "tcp")
            {
                chatCommunicator = new TcpChatCommunicator();
            }
            else
            {
                chatCommunicator = new UdpChatCommunicator();
                ((UdpChatCommunicator)chatCommunicator).udpTimeout = udpTimeout;
                ((UdpChatCommunicator)chatCommunicator).udpRetries = udpRetries;
            }

            // Initialize and start the chat client
            try
            {
                ChatClient client = new ChatClient(chatCommunicator, serverAddress, serverPort, protocol);
                await client.RunAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error starting chat client: {ex.Message}");
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine("IPK24ChatClient Help");
            Console.WriteLine("Usage: IPK24ChatClient -t [tcp/udp] -s [server address] -p [port] -d [UDP timeout] -r [UDP retries]");
            Console.WriteLine("-t tcp|udp    : Specify the transport protocol to use (TCP or UDP).");
            Console.WriteLine("-s address    : Specify the server IP address or hostname.");
            Console.WriteLine("-p port       : Specify the server port.");
            Console.WriteLine("-d timeout    : Specify the UDP confirmation timeout in milliseconds.");
            Console.WriteLine("-r retries    : Specify the maximum number of UDP retransmissions.");
            Console.WriteLine("-h            : Print this help message.");
        }
    }
}
