namespace IPK24ChatClient
{
    public class JoinHandler : ICommandHandler
    {
        private readonly IChatCommunicator chatCommunicator;
        private readonly ChatClient chatClient;

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
                return;
            }

            Message joinMessage = new Message(MessageType.Join, channelId: parameters[0], displayName: chatClient.displayName);
            await chatCommunicator.SendMessageAsync(joinMessage.SerializeToTcp());
            chatClient.setLastCommandSent(MessageType.Join);
        }
    }

}