// -----------------------------------------------------------------------------
// Project: IPK24ChatClient
// File: Program.cs
// Author: Milan Jakubec (xjakub41)
// Date: 2024-03-26
// License: GPU General Public License v3.0
// Description: Main entrance point and argument parser for the chat client.
// -----------------------------------------------------------------------------
namespace IPK24ChatClient
{
    class Program
    {
        /// <summary>
        /// Main entrance point for the chat client.
        /// </summary>
        /// <param name="args">arguments to be parsed</param>
        /// <exception cref="ArgumentException">Gets thrown in case an invalid arg is used</exception>
        static async Task Main(string[] args)
        {
            // Default values
            IChatCommunicator chatCommunicator; // Interface for communication
            string? protocol = null;            // Protocol to use (mandatory argument)
            string? serverAddress = null;       // Server address  (mandatory argument)
            int serverPort = 4567;              // Server port
            int udpTimeout = 250;               // retransmission timeout (milliseconds)
            int udpRetries = 3;                 // number of retransmissions

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
            finally
            {
                System.Environment.Exit(0);
            }
        }

        /// <summary>
        /// Print help message for the chat client.
        /// </summary>
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
