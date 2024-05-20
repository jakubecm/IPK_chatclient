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
        private string protocol;
        private ClientState clientState;
        private MessageType? lastCommandSent;
        public string? displayName;
        public string? channelId;
        private CancellationTokenSource cts;
        private Dictionary<string, ICommandHandler> commandHandlers;
        private SemaphoreSlim commandSemaphore = new SemaphoreSlim(1, 1);
        public TaskCompletionSource<bool>? commandCompletionSource;


        public ChatClient(IChatCommunicator chatCommunicator, string serverAddress, int serverPort, string protocol)
        {
            this.chatCommunicator = chatCommunicator;
            this.serverAddress = serverAddress;
            this.serverPort = serverPort;
            this.protocol = protocol;
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
                var userInputTask = HandleUserInputAsync(cts.Token);
                var listeningTask = ListenForMessagesAsync(cts.Token);

                await Task.WhenAll(listeningTask, userInputTask);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in run method: {ex.Message}");
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
                    if (this.protocol == "tcp")
                    {
                        var rawData = await chatCommunicator.ReceiveMessageAsync();
                        if (string.IsNullOrEmpty(rawData))
                        {
                            clientState = ClientState.End;
                            cts.Cancel(); // Signal everyone to stop
                            break; // Break the loop if the connection is closed or an error occurred
                        }

                        // Parse the raw string data into a Message object
                        var message = chatCommunicator.ParseMessage(rawData);
                        if (message != null)
                        {
                            HandleMessageByType(message);
                        }
                        else continue;

                    }
                    else
                    {
                        int udpTimeout = ((UdpChatCommunicator)chatCommunicator).getUdpTimeout();
                        var message = await chatCommunicator.ReceiveMessageAsync(udpTimeout);

                        if (message != null)
                        {
                            HandleMessageByType(message);
                        }
                        else continue;
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
        private async void HandleMessageByType(Message message)
        {
            switch (message?.Type)
            {
                case MessageType.Msg:
                    if (clientState == ClientState.Open)
                    {
                        Console.WriteLine($"{message.DisplayName}: {message.Content}");
                    }
                    else
                    {
                        await SendErrorMessage("Cannot receive messages in the current state.");
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
                        await SendErrorMessage("Unexpected error.");
                        clientState = ClientState.Error;
                        Console.Error.WriteLine($"ERR: Cannot receive messages in the current state.");
                        await SendBye();
                    }
                    break;
                case MessageType.Reply:
                    if (clientState == ClientState.Auth || clientState == ClientState.Open)
                    {
                        HandleReplyMessage(message);
                        signalSemaphoreToRelease();
                    }
                    else
                    {
                        clientState = ClientState.Error;
                        await SendErrorMessage("Unexpected reply.");
                        Console.Error.WriteLine($"ERR: Unexpected reply.");
                        await SendBye();
                    }
                    break;
                case MessageType.Confirm:
                    // add message ID to confirmedSentMessageIds
                    if (message.MessageId.HasValue)
                    {
                        ushort messageId = message.MessageId.Value;
                        ((UdpChatCommunicator)chatCommunicator).addConfirmedSentMessageId(messageId);
                    }
                    break;
                case MessageType.Bye:
                    if (clientState != ClientState.Open)
                    {
                        await SendErrorMessage("Unexpected BYE message.");
                        clientState = ClientState.Error;
                        Console.Error.WriteLine($"ERR: Unexpected BYE message received.");
                        await SendBye();
                    }
                    Console.WriteLine("Disconnected from the server.");
                    signalSemaphoreToRelease();
                    cts.Cancel();
                    clientState = ClientState.End;
                    chatCommunicator.Disconnect();
                    break;

                default:
                    Console.Error.WriteLine($"ERR: Unknown message type recieved");
                    signalSemaphoreToRelease();
                    await SendErrorMessage("Unknown message type");
                    clientState = ClientState.Error;
                    await SendBye();
                    break;
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
            while (!cancelToken.IsCancellationRequested)
            {
                if (Console.IsInputRedirected && Console.In.Peek() == -1)
                {
                    clientState = ClientState.End;
                    await SendBye();
                    break;
                }

                bool inputAvailable = Console.IsInputRedirected ? Console.In.Peek() != -1 : Console.KeyAvailable;

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
                        // TODO: Make a separate method for this?
                        // Check if user input is max 1400 characters long and contains only printable characters with space (0x21 - 0x7E)
                        if (userInput.Length > 1400)
                        {
                            Console.Error.WriteLine("Message is too long. Maximum length is 1400 characters.");
                            continue;
                        }
                        foreach (char c in userInput)
                        {
                            if (c < 0x20 || c > 0x7E)
                            {
                                Console.Error.WriteLine("Message contains non-printable characters and can contain only printable characters and space.");
                                continue;
                            }
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
                    // No input available, so wait a bit before checking again
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

        public async Task SendErrorMessage(string content)
        {
            Message errMsg = new Message(MessageType.Err, displayName: this.displayName, content: content);
            await chatCommunicator.SendMessageAsync(errMsg);
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
