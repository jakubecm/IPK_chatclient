namespace IPK24ChatClient
{
    public class JoinHandler : ICommandHandler
    {
        private readonly IChatCommunicator chatCommunicator;
        private readonly ChatClient chatClient;
        public bool RequiresServerConfirmation => true;

        public JoinHandler(IChatCommunicator chatCommunicator, ChatClient chatClient)
        {
            this.chatCommunicator = chatCommunicator;
            this.chatClient = chatClient;
        }

        public async Task ExecuteCommandAsync(string[] parameters, CancellationToken cancellationToken)
        {
            if (parameters.Length != 1)
            {
                Console.WriteLine("Usage: /join {ChannelName}");
                chatClient.commandCompletionSource?.SetResult(true);
                return;
            }

            if (chatClient.getClientState() != ClientState.Open)
            {
                Console.Error.WriteLine("You must authenticate before joining a channel.");
                chatClient.commandCompletionSource?.SetResult(true);
                return;
            }

            Message joinMessage = new Message(MessageType.Join, channelId: parameters[0], displayName: chatClient.displayName);
            await chatCommunicator.SendMessageAsync(joinMessage);
            chatClient.setLastCommandSent(MessageType.Join);
            chatClient.setChannelId(parameters[0]);
        }
    }

}