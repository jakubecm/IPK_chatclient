using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace IPK24ChatClient
{
    public enum ClientState
    {
        Start,
        Auth,
        Open,
        Error,
        End
    }
    public class ChatClient
    {
        private TcpClient? tcpClient;
        private NetworkStream? stream;
        private string serverAddress;
        private int serverPort;
        private ClientState clientState;
        private MessageType? lastCommandSent;


        public ChatClient(string serverAddress, int serverPort)
        {
            this.serverAddress = serverAddress;
            this.serverPort = serverPort;
            clientState = ClientState.Start;
        }

        public async Task RunAsync()
        {
            Console.CancelKeyPress += async (sender, e) =>
            {
                e.Cancel = true; // Prevent the process from terminating.
                Console.WriteLine("Disconnecting...");
                Message byeMsg = new Message(MessageType.Bye);
                await SendMessageAsync(byeMsg.SerializeToTcp()); // Send a "BYE" message for a clean disconnect.

            };
            try
            {
                await ConnectToServerAsync();
                Console.WriteLine("Connected to the server. You can start sending messages.");
                var listeningTask = ListenForMessagesAsync();
                await HandleUserInputAsync();
                await listeningTask; // Wait for listening task to complete (e.g., connection closed)
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                Disconnect();
            }
        }

        private async Task ConnectToServerAsync()
        {
            tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(serverAddress, serverPort);
            stream = tcpClient.GetStream();
            clientState = ClientState.Auth;
        }

        private async Task ListenForMessagesAsync()
        {
            var buffer = new byte[4096];
            if (stream == null || tcpClient == null) throw new InvalidOperationException("The client is not connected to the server.");

            try
            {
                while (tcpClient.Connected && clientState != ClientState.End)
                {
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break; // Server closed connection

                    var messageData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    var message = Message.ParseFromTcp(messageData); // Deserialize the message

                    // Handle the message based on its type
                    switch (message.Type)
                    {
                        case MessageType.Msg:
                            Console.WriteLine($"{message.DisplayName}: {message.Content}");
                            break;
                        case MessageType.Err:
                            Console.Error.WriteLine($"ERR FROM {message.DisplayName}: {message.Content}");
                            break;
                        case MessageType.Reply:
                            if (message.ReplySuccess == true)
                            {
                                Console.Error.WriteLine($"Success: {message.Content}");

                                switch(lastCommandSent){
                                    case MessageType.Auth:
                                        clientState = ClientState.Open;
                                        lastCommandSent = null;
                                        break;
                                    case MessageType.Join:
                                        clientState = ClientState.Open;
                                        lastCommandSent = null;
                                        break;
                                }
                            }
                            else
                            {
                                Console.Error.WriteLine($"Failure: {message.Content}");

                                switch(lastCommandSent){
                                    case MessageType.Auth:
                                        clientState = ClientState.Auth;
                                        lastCommandSent = null;
                                        break;
                                    case MessageType.Join:
                                        clientState = ClientState.Open;
                                        lastCommandSent = null;
                                        break;
                                }
                            }
                            break;

                        case MessageType.Bye:
                            Console.WriteLine("Disconnected from the server.");
                            clientState = ClientState.End;
                            break;

                        default:
                            // No other message types should trigger program output according to the specs
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERR: {ex.Message}");
                clientState = ClientState.Error;
            }
        }


        private async Task HandleUserInputAsync()
        {
            string? localDisplayName = null; // Keep track of local DisplayName set by /auth or /rename

            Console.WriteLine("You can start typing commands. Type '/help' for available commands.");
            while (true && clientState != ClientState.End)
            {
                string? userInput = Console.ReadLine();
                if (string.IsNullOrEmpty(userInput)) continue;

                if (!userInput.StartsWith("/")){
                    if (clientState != ClientState.Open)
                    {
                        Console.Error.WriteLine("You must authenticate and join a channel before sending messages.");
                        continue;
                    }

                    Message chatMessage = new Message(MessageType.Msg, displayName: localDisplayName, content: userInput);
                    await SendMessageAsync(chatMessage.SerializeToTcp());
                    continue;
                }

                // Split the userInput into command and parameters
                var inputParts = userInput.Split(' ');
                var command = inputParts[0].ToLower();

                switch (command)
                {
                    case "/auth":
                        lastCommandSent = MessageType.Auth;

                        if (inputParts.Length != 4)
                        {
                            Console.WriteLine("Usage: /auth {Username} {Secret} {DisplayName}");
                            break;
                        }

                        localDisplayName = inputParts[3]; // Update local display name
                        Message authMessage = new Message(MessageType.Auth, username: inputParts[1], secret: inputParts[2], displayName: inputParts[3]);
                        await SendMessageAsync(authMessage.SerializeToTcp());
                        break;

                    case "/join":
                        lastCommandSent = MessageType.Join;

                        if (inputParts.Length != 2)
                        {
                            Console.WriteLine("Usage: /join {ChannelID}");
                            break;
                        }
                        Message joinMessage = new Message(MessageType.Join, channelId: inputParts[1], displayName: localDisplayName);
                        await SendMessageAsync(joinMessage.SerializeToTcp());
                        break;

                    case "/rename":

                        if (inputParts.Length != 2)
                        {
                            Console.WriteLine("Usage: /rename {DisplayName}");
                            break;
                        }
                        localDisplayName = inputParts[1];
                        Console.WriteLine($"Display name changed to: {localDisplayName}");
                        break;

                    case "/help":
                        PrintHelpCommands();
                        break;

                    default:
                        Console.WriteLine("Unknown command. Type '/help' for available commands.");
                        break;
                }
            }
        }

        private void PrintHelpCommands()
        {
            Console.WriteLine("/auth {Username} {Secret} {DisplayName} - Authenticate with the server.");
            Console.WriteLine("/join {ChannelID} - Join a chat channel.");
            Console.WriteLine("/rename {DisplayName} - Change your display name locally.");
            Console.WriteLine("/help - Show this help message.");
        }


        private async Task SendMessageAsync(string message)
        {
            // Convert the string message to byte array and send
            var byteMessage = Encoding.UTF8.GetBytes(message);

            if (stream == null)
            {
                throw new InvalidOperationException("The client is not connected to the server.");
            }

            await stream.WriteAsync(byteMessage, 0, byteMessage.Length);
        }

        private void Disconnect()
        {
            stream?.Close();
            tcpClient?.Close();
        }
    }
}
