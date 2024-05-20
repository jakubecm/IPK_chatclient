using System.Text.RegularExpressions;

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

            if (!validateParameters(parameters))
            {
                Console.Error.WriteLine("Invalid channel ID. Channel ID must be between 1 and 20 characters long and contain only alphanumeric characters and hyphens.");
                chatClient.commandCompletionSource?.SetResult(true);
                return;
            }

            Message joinMessage = new Message(MessageType.Join, channelId: parameters[0], displayName: chatClient.displayName);
            await chatCommunicator.SendMessageAsync(joinMessage);
            chatClient.setLastCommandSent(MessageType.Join);
            chatClient.setChannelId(parameters[0]);
        }

        public bool validateParameters(string[] parameters)
        {
            // regex to match alphanumeric characters and hyphens, 1-20 characters long
            var regex = new Regex("^[a-zA-Z0-9-]{1,20}$");

            return regex.IsMatch(parameters[0]);
        }
    }

}