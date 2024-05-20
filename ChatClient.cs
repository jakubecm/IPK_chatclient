// -----------------------------------------------------------------------------
// Project: IPK24ChatClient
// File: ChatClient.cs
// Author: Milan Jakubec (xjakub41)
// Date: 2024-03-26
// License: GPU General Public License v3.0
// Description: This is implementation of chat client logic.
// -----------------------------------------------------------------------------
namespace IPK24ChatClient
{
    /// <summary>
    /// Represents the state of the client.
    /// </summary>
    public enum ClientState
    {
        Start, // Initial state before sending auth
        Auth, // After sending auth and waiting for reply or in case of unsuccessful auth
        Open, // After successful auth or join
        Error, // In case of an error
        End // After sending BYE or receiving BYE
    }
    public class ChatClient
    {
        /// <value>ChatCommunicator instance used for communication with the server.</value>
        private readonly IChatCommunicator chatCommunicator;

        /// <value>Server address.</value>
        private string serverAddress;

        /// <value>Server port.</value>
        private int serverPort;

        /// <value>Protocol used for communication with the server.</value>
        private string protocol;

        /// <value>Current state of the client.</value>
        private ClientState clientState;

        /// <value>Last command sent to the server.</value>
        private MessageType? lastCommandSent;

        /// <value>Display name of the client.</value>
        public string? displayName;

        /// <value>Channel ID of the client.</value>
        public string? channelId;

        /// <value>Cancellation token source for the client used to abort operations.</value>
        private CancellationTokenSource cts;

        /// <value>Dictionary of command handlers for handling user input.</value>
        private Dictionary<string, ICommandHandler> commandHandlers;

        /// <value>Semaphore for ensuring only one command is executed at a time.</value>
        private SemaphoreSlim commandSemaphore = new SemaphoreSlim(1, 1);

        /// <value>Task completion source for waiting for command completion.</value>
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

        /// <summary>
        /// Runs the chat client and its methods.
        /// </summary>
        public async Task RunAsync()
        {
            // Handle Ctrl+C
            Console.CancelKeyPress += async (sender, e) =>
            {
                e.Cancel = true; // Prevent the process from terminating.
                signalSemaphoreToRelease();
                await SendBye();
            };

            try
            {
                await chatCommunicator.ConnectAsync(serverAddress, serverPort);
                var userInputTask = HandleUserInputAsync(cts.Token);
                var listeningTask = ListenForMessagesAsync(cts.Token);

                await Task.WhenAll(listeningTask, userInputTask); // Wait for both tasks to finish
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERR: Run method error - {ex.Message}");
            }
            finally
            {
                if (chatCommunicator != null)
                {
                    chatCommunicator.Disconnect();
                }
                Environment.Exit(0);
            }

        }

        /// <summary>
        /// Listens for messages from the server and handles them accordingly.
        /// </summary>
        /// <param name="cancelToken">cancellation token that activates on ctrl+c press</param>
        private async Task ListenForMessagesAsync(CancellationToken cancelToken)
        {
            try
            {
                while (!cancelToken.IsCancellationRequested)
                {
                    if (this.protocol == "tcp")
                    {
                        Message? message = await chatCommunicator.ReceiveMessageAsync();
                        if (message == null)
                        {
                            clientState = ClientState.End;
                            cts.Cancel(); // Signal everyone to stop
                            break; // Break the loop if the connection is closed or an error occurred
                        }

                        HandleMessageByType(message);
                        continue;

                    }
                    else // UDP listening
                    {
                        var message = await chatCommunicator.ReceiveMessageAsync();

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

        /// <summary>
        /// Handles the message based on its type.
        /// </summary>
        /// <param name="message">Message to be handled</param>
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
                        Console.Error.WriteLine($"ERR: Received unexpected message.");
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
                        await SendErrorMessage("Unexpected BYE message received.");
                        clientState = ClientState.Error;
                        Console.Error.WriteLine($"ERR: Unexpected BYE message received.");
                        await SendBye();
                    }    
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

        /// <summary>
        /// Handles the reply message from the server.
        /// </summary>
        /// <param name="message">Message object to handle reply to</param>
        private void HandleReplyMessage(Message message)
        {
            if (message.ReplySuccess == true)
            {
                Console.Error.WriteLine($"Success: {message.Content}");
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


        /// <summary>
        /// Handles user input from the console.
        /// </summary>
        /// <param name="cancelToken">Cancel token for aborting operation</param>
        private async Task HandleUserInputAsync(CancellationToken cancelToken)
        {
            while (!cancelToken.IsCancellationRequested)
            {
                // Check if input is redirected and there is no more input
                if (Console.IsInputRedirected && Console.In.Peek() == -1)
                {
                    clientState = ClientState.End;
                    await SendBye();
                    break;
                }

                // Check if there is any input available, either from the console or redirected input
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
                        // Check if user input is max 1400 characters long and contains only printable characters with space (0x20 - 0x7E)
                        if (userInput.Length > 1400)
                        {
                            Console.Error.WriteLine("Message is too long. Maximum length is 1400 characters.");
                            continue;
                        }
                        foreach (char c in userInput)
                        {
                            // 0x20 = space, 0x7E = ~
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

                    // Try to find the command handler for the given command
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
                                Console.Error.WriteLine("ERR: Command execution was canceled.");
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
                        Console.Error.WriteLine($"ERR: Unknown command: {command}. Type '/help' for available commands.");
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

        /// <summary>
        /// Sends BYE message to the server and disconnects the client.
        /// </summary>
        public async Task SendBye()
        {
            Console.WriteLine("Disconnecting...");
            Message byeMsg = new Message(MessageType.Bye);
            await chatCommunicator.SendMessageAsync(byeMsg);
            clientState = ClientState.End;
            cts.Cancel(); // Signal to input and listening tasks to stop
            chatCommunicator.Disconnect();
        }

        /// <summary>
        /// Sends an error message to the server.
        /// </summary>
        /// <param name="content">Content of the error</param>
        public async Task SendErrorMessage(string content)
        {
            Message errMsg = new Message(MessageType.Err, displayName: this.displayName, content: content);
            await chatCommunicator.SendMessageAsync(errMsg);
        }

        /// <summary>
        /// Sets the display name of the client.
        /// </summary>
        /// <param name="command">the message type to set the variable to</param>
        public void setLastCommandSent(MessageType command)
        {
            lastCommandSent = command;
        }

        /// <summary>
        /// Sets the display name of the client.
        /// </summary>
        /// <param name="id">the id to set</param>
        public void setChannelId(string id)
        {
            channelId = id;
        }

        /// <summary>
        /// Sets the display name of the client.
        /// </summary>
        /// <param name="state">the state to set</param>
        public void setClientState(ClientState state)
        {
            clientState = state;
        }

        /// <summary>
        /// Getter for the client state.
        /// </summary>
        /// <returns>the current client state</returns>
        public ClientState getClientState()
        {
            return clientState;
        }

        /// <summary>
        /// Signals the semaphore to release the lock.
        /// </summary>
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
