using System.Text.RegularExpressions;

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
                chatClient.commandCompletionSource?.SetResult(true);
                return;
            }

            if (!validateParameters(parameters))
            {
                Console.Error.WriteLine("Invalid parameters.");
                Console.Error.WriteLine("Username can only contain alphanumeric characters and be 1-20 characters long.");
                Console.Error.WriteLine("Secret can only contain alphanumeric characters and be 1-128 characters long.");
                Console.Error.WriteLine("Display name can only contain printable ASCII characters and be 1-20 characters long.");
                chatClient.commandCompletionSource?.SetResult(true);
                return;
            }

            chatClient.displayName = parameters[2];
            Message authMessage = new Message(MessageType.Auth, username: parameters[0], secret: parameters[1], displayName: parameters[2]);
            chatClient.setClientState(ClientState.Auth);
            chatClient.setLastCommandSent(MessageType.Auth);

            await chatCommunicator.SendMessageAsync(authMessage);

        }

        public bool validateParameters(string[] parameters)
        {
            string username = parameters[0];
            string secret = parameters[1];
            string displayName = parameters[2];

            var usernameRegex = new Regex("^[a-zA-Z0-9-]{1,20}$");
            var secretRegex = new Regex("^[a-zA-Z0-9-]{1,128}$");

            if (!usernameRegex.IsMatch(username) || !secretRegex.IsMatch(secret))
            {
                return false;
            }

            if (displayName.Length <= 0 || displayName.Length > 20)
            {
                return false;
            }

            foreach (char c in displayName)
            {
                // ASCII printable characters, excluding space
                // 0x21 = '!', 0x7E = '~'
                if (c < '!' || c > '~')
                {
                    return false;
                }
            }

            return true;
        }
    }


}