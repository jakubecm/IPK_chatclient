
namespace IPK24ChatClient
{
    public class AuthHandler : ICommandHandler
    {
        private readonly IChatCommunicator chatCommunicator;
        private readonly ChatClient chatClient;

        public AuthHandler(IChatCommunicator chatCommunicator, ChatClient chatClient)
        {
            this.chatCommunicator = chatCommunicator;
            this.chatClient = chatClient;
        }

        public async Task ExecuteCommandAsync(string[] parameters, CancellationToken cancellationToken)
        {
            if (parameters.Length != 3)
            {
                Console.WriteLine("Usage: /auth {Username} {Secret} {DisplayName}");
                return;
            }

            chatClient.displayName = parameters[2];
            Message authMessage = new Message(MessageType.Auth, username: parameters[0], secret: parameters[1], displayName: parameters[2]);
            await chatCommunicator.SendMessageAsync(authMessage.SerializeToTcp());
        
            chatClient.setLastCommandSent(MessageType.Auth);
        }
    }


}