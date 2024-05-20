
namespace IPK24ChatClient
{
    public class AuthHandler : ICommandHandler
    {
        private readonly IChatCommunicator chatCommunicator;
        private readonly ChatClient chatClient;
        public bool RequiresServerConfirmation => true;

        public AuthHandler(IChatCommunicator chatCommunicator, ChatClient chatClient)
        {
            this.chatCommunicator = chatCommunicator;
            this.chatClient = chatClient;
        }

        public async Task ExecuteCommandAsync(string[] parameters, CancellationToken cancellationToken)
        {
            if (parameters.Length != 3 || parameters.Any(string.IsNullOrWhiteSpace))
            {
                Console.WriteLine("Usage: /auth {Username} {Secret} {DisplayName}");
                Console.WriteLine("Note: Display name cannot be empty.");
                chatClient.commandCompletionSource?.SetResult(true);
                return;
            }
            if (chatClient.getClientState() == ClientState.Open ||
                chatClient.getClientState() == ClientState.Error ||
                chatClient.getClientState() == ClientState.End)
            {
                Console.Error.WriteLine("You are already authenticated.");
                return;
            }

            chatClient.displayName = parameters[2];
            Message authMessage = new Message(MessageType.Auth, username: parameters[0], secret: parameters[1], displayName: parameters[2]);
            await chatCommunicator.SendMessageAsync(authMessage);

            chatClient.setLastCommandSent(MessageType.Auth);
            chatClient.setClientState(ClientState.Auth);
        }
    }


}