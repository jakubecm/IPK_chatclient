using System;
using System.Collections;
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
        private readonly IChatCommunicator chatCommunicator;
        private string serverAddress;
        private int serverPort;
        private ClientState clientState;
        private MessageType? lastCommandSent;
        private string? displayName;


        public ChatClient(IChatCommunicator chatCommunicator, string serverAddress, int serverPort)
        {
            this.chatCommunicator = chatCommunicator;
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
                await chatCommunicator.SendMessageAsync(byeMsg.SerializeToTcp()); // Send a "BYE" message for a clean disconnect.
            };

            try
            {
                await chatCommunicator.ConnectAsync(serverAddress, serverPort);
                Console.WriteLine("Connected to the server. You can start AUTH.");
                var listeningTask = ListenForMessagesAsync();
                await HandleUserInputAsync();
                await listeningTask; // Wait for listening task to complete (e.g., connection closed)
                Console.WriteLine("Listen task over.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                chatCommunicator.Disconnect();
                Environment.Exit(0);
            }
        }

        private async Task ListenForMessagesAsync()
        {
            try
            {
                while (clientState != ClientState.End)
                {
                    // Receive a complete message as a string
                    var rawData = await chatCommunicator.ReceiveMessageAsync();
                    if (string.IsNullOrEmpty(rawData))
                    {
                        Console.WriteLine("No more data from server, or connection closed.");
                        clientState = ClientState.End;
                        break; // Break the loop if the connection is closed or an error occurred
                    }

                    // Parse the raw string data into a Message object
                    var message = Message.ParseFromTcp(rawData);

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
                            HandleReplyMessage(message);
                            break;
                        case MessageType.Bye:
                            Console.WriteLine("Disconnected from the server.");
                            clientState = ClientState.End;
                            break;
                        default:
                            Console.Error.WriteLine($"Unknown message type: {message.Type}");
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

        private void HandleReplyMessage(Message message)
        {
            if (message.ReplySuccess == true)
            {
                Console.WriteLine($"Success: {message.Content}");
                switch (lastCommandSent)
                {
                    case MessageType.Auth:
                        clientState = ClientState.Open;
                        break;
                    case MessageType.Join:
                        clientState = ClientState.Open;
                        break;
                }
            }
            else
            {
                Console.Error.WriteLine($"Failure: {message.Content}");
                switch (lastCommandSent)
                {
                    case MessageType.Auth:
                        clientState = ClientState.Auth;
                        break;
                    case MessageType.Join:
                        clientState = ClientState.Open;
                        break;
                }
            }
            lastCommandSent = null; // Reset after handling
        }



        private async Task HandleUserInputAsync()
        {
            Console.WriteLine("You can start typing commands. Type '/help' for available commands.");

            while (clientState != ClientState.End)
            {
                string? userInput = Console.ReadLine();
                if (string.IsNullOrEmpty(userInput)) continue;

                if (!userInput.StartsWith("/")){

                    if (clientState != ClientState.Open)
                    {
                        Console.Error.WriteLine("You must authenticate and join a channel before sending messages.");
                        continue;
                    }

                    Message chatMessage = new Message(MessageType.Msg, displayName: this.displayName, content: userInput);
                    await chatCommunicator.SendMessageAsync(chatMessage.SerializeToTcp());
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

                        this.displayName = inputParts[3]; // Update local display name
                        Message authMessage = new Message(MessageType.Auth, username: inputParts[1], secret: inputParts[2], displayName: inputParts[3]);
                        await chatCommunicator.SendMessageAsync(authMessage.SerializeToTcp());
                        break;

                    case "/join":
                        lastCommandSent = MessageType.Join;

                        if (inputParts.Length != 2)
                        {
                            Console.WriteLine("Usage: /join {ChannelID}");
                            break;
                        }
                        Message joinMessage = new Message(MessageType.Join, channelId: inputParts[1], displayName: this.displayName);
                        await chatCommunicator.SendMessageAsync(joinMessage.SerializeToTcp());
                        break;

                    case "/rename":

                        if (inputParts.Length != 2)
                        {
                            Console.WriteLine("Usage: /rename {DisplayName}");
                            break;
                        }
                        this.displayName = inputParts[1];
                        Console.WriteLine($"Display name changed to: {this.displayName}");
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

    }
}
