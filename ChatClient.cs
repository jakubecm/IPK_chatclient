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
        public string? displayName;
        private CancellationTokenSource cts;
        private Dictionary<string, ICommandHandler> commandHandlers;


        public ChatClient(IChatCommunicator chatCommunicator, string serverAddress, int serverPort)
        {
            this.chatCommunicator = chatCommunicator;
            this.serverAddress = serverAddress;
            this.serverPort = serverPort;
            clientState = ClientState.Start;
            cts = new CancellationTokenSource();

            commandHandlers = new Dictionary<string, ICommandHandler>
            {
                ["/auth"] = new AuthHandler(chatCommunicator, this),
                ["/join"] = new JoinHandler(chatCommunicator, this),
                ["/rename"] = new RenameHandler(this),
                ["/help"] = new HelpHandler()
            };
        }

        public async Task RunAsync()
        {
            Console.CancelKeyPress += async (sender, e) =>
            {
                e.Cancel = true; // Prevent the process from terminating.
                Console.WriteLine("Disconnecting...");
                Message byeMsg = new Message(MessageType.Bye);
                await chatCommunicator.SendMessageAsync(byeMsg.SerializeToTcp()); // Send a "BYE" message for a clean disconnect.
                cts.Cancel(); // Signal for cancellation to the HandleUserInputAsync method
            };

            try
            {
                await chatCommunicator.ConnectAsync(serverAddress, serverPort);
                Console.WriteLine("Connected to the server. You can start AUTH.");
                var listeningTask = ListenForMessagesAsync();
                await HandleUserInputAsync(cts.Token);
                await listeningTask; // Wait for listening task to complete (e.g., connection closed)
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

                            if (clientState == ClientState.Auth)
                            {
                                clientState = ClientState.End;
                                // send a "BYE" message for a clean disconnect
                                Message byeMsg = new Message(MessageType.Bye);
                                await chatCommunicator.SendMessageAsync(byeMsg.SerializeToTcp());
                            }
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



        private async Task HandleUserInputAsync(CancellationToken cancelToken)
        {
            Console.WriteLine("You can start typing commands. Type '/help' for available commands.");

            while (clientState != ClientState.End && !cancelToken.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    string? userInput = Console.ReadLine();
                    if (string.IsNullOrEmpty(userInput)) continue;

                    if (!userInput.StartsWith("/"))
                    {
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

                    if (commandHandlers.TryGetValue(command, out var handler))
                    {
                        await handler.ExecuteCommandAsync(inputParts[1..], cancelToken);
                    }
                    else
                    {
                        Console.Error.WriteLine($"Unknown command: {command}. Type '/help' for available commands.");
                    }
                }
                else
                {
                    try
                    {
                        await Task.Delay(100, cancelToken);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        public void setLastCommandSent(MessageType command)
        {
            lastCommandSent = command;
        }

    }
}
