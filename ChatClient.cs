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
        public string? channelId;
        private CancellationTokenSource cts;
        private Dictionary<string, ICommandHandler> commandHandlers;
        private SemaphoreSlim commandSemaphore = new SemaphoreSlim(1, 1);
        public TaskCompletionSource<bool>? commandCompletionSource;


        public ChatClient(IChatCommunicator chatCommunicator, string serverAddress, int serverPort)
        {
            this.chatCommunicator = chatCommunicator;
            this.serverAddress = serverAddress;
            this.serverPort = serverPort;
            clientState = ClientState.Start;
            lastCommandSent = null;
            channelId = null;
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
                signalSemaphoreToRelease();
                await SendBye();
            };

            try
            {
                await chatCommunicator.ConnectAsync(serverAddress, serverPort);
                Console.WriteLine("Connected to the server. You can start AUTH.");
                var listeningTask = ListenForMessagesAsync(cts.Token);
                await HandleUserInputAsync(cts.Token);
                await listeningTask; // Wait for listening task to complete (e.g., connection closed)
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("Shutting down...");

                if (chatCommunicator != null)
                {
                    chatCommunicator.Disconnect();
                }
                Environment.Exit(0);
            }

        }

        private async Task ListenForMessagesAsync(CancellationToken cancelToken)
        {
            try
            {
                while (!cancelToken.IsCancellationRequested)
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
                    var message = chatCommunicator.ParseMessage(rawData);

                    // Handle the message based on its type
                    switch (message.Type)
                    {
                        case MessageType.Msg:
                            if (clientState == ClientState.Open)
                            {
                                Console.WriteLine($"{message.DisplayName}: {message.Content}");
                            }
                            else
                            {
                                clientState = ClientState.Error;
                                Console.Error.WriteLine($"ERR: Cannot receive messages in the current state.");
                                await SendBye();
                            }
                            break;
                        case MessageType.Err:
                            if (clientState == ClientState.Auth || clientState == ClientState.Open)
                            {
                                Console.Error.WriteLine($"ERR FROM {message.DisplayName}: {message.Content}");
                                signalSemaphoreToRelease();
                                await SendBye();
                            }
                            else
                            {
                                clientState = ClientState.Error;
                                Console.Error.WriteLine($"ERR: Cannot receive messages in the current state.");
                                await SendBye();
                            }
                            break;
                        case MessageType.Reply:
                            HandleReplyMessage(message);
                            signalSemaphoreToRelease();
                            break;
                        case MessageType.Bye:
                            if (clientState != ClientState.Open)
                            {
                                clientState = ClientState.Error;
                                Console.Error.WriteLine($"ERR: Unexpected BYE message.");
                                await SendBye();
                            }
                            Console.WriteLine("Disconnected from the server.");
                            signalSemaphoreToRelease();
                            cts.Cancel();
                            clientState = ClientState.End;
                            chatCommunicator.Disconnect();
                            break;

                        default:
                            Console.Error.WriteLine($"ERR: Unknown message type");
                            signalSemaphoreToRelease();
                            clientState = ClientState.Error;
                            await SendBye();
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                clientState = ClientState.Error;
                Console.Error.WriteLine($"ERR: {ex.Message}");
                await SendBye();
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

            while (!cancelToken.IsCancellationRequested)
            {
                if(Console.IsInputRedirected && Console.In.Peek() == -1)
                {
                    clientState = ClientState.End;
                    await SendBye();
                    break;
                }

                bool inputAvailable = Console.IsInputRedirected? Console.In.Peek() != -1 : Console.KeyAvailable;

                if (inputAvailable)
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

                        Message chatMessage = new Message(MessageType.Msg, channelId: channelId, displayName: this.displayName, content: userInput);
                        await chatCommunicator.SendMessageAsync(chatMessage);
                        continue;
                    }

                    // Split the userInput into command and parameters
                    var inputParts = userInput.Split(' ');
                    var command = inputParts[0].ToLower();

                    if (commandHandlers.TryGetValue(command, out var handler))
                    {
                        if (handler.RequiresServerConfirmation)
                        {
                            await commandSemaphore.WaitAsync(cancelToken);
                            try
                            {
                                commandCompletionSource = new TaskCompletionSource<bool>();
                                await handler.ExecuteCommandAsync(inputParts[1..], cancelToken);
                                await commandCompletionSource.Task;
                            }
                            catch (OperationCanceledException)
                            {
                                Console.Error.WriteLine("Command execution was canceled.");
                            }
                            finally
                            {
                                commandSemaphore.Release();
                            }
                        }
                        else
                        {
                            await handler.ExecuteCommandAsync(inputParts[1..], cancelToken);
                        }
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

        public async Task SendBye()
        {
            Console.WriteLine("Disconnecting...");
            Message byeMsg = new Message(MessageType.Bye);
            await chatCommunicator.SendMessageAsync(byeMsg);
            clientState = ClientState.End;
            cts.Cancel();
            chatCommunicator.Disconnect();
        }

        public void setLastCommandSent(MessageType command)
        {
            lastCommandSent = command;
        }

        public void setChannelId(string id)
        {
            channelId = id;
        }

        public void setClientState(ClientState state)
        {
            clientState = state;
        }

        public ClientState getClientState()
        {
            return clientState;
        }

        public void signalSemaphoreToRelease()
        {
            // If something is waiting for command completion source, unlock it
            if (commandCompletionSource?.Task.IsCompleted == false)
            {
                commandCompletionSource?.SetResult(true);
            }
        }

    }
}
