namespace IPK24ChatClient
{
    public class RenameHandler : ICommandHandler
    {
        private readonly ChatClient chatClient;
        public bool RequiresServerConfirmation => false;
        public RenameHandler(ChatClient chatClient)
        {
            this.chatClient = chatClient;
        }

        public Task ExecuteCommandAsync(string[] parameters, CancellationToken cancellationToken)
        {
            if (parameters.Length != 1)
            {
                Console.WriteLine("Usage: /rename {DisplayName}");
            }

            if (!validateParameters(parameters))
            {
                Console.Error.WriteLine("Invalid display name. Display name must be between 1 and 20 characters long and contain only printable ASCII characters.");
                return Task.CompletedTask;
            }

            chatClient.displayName = parameters[0];
            Console.WriteLine($"You are now known as {parameters[0]}");
            return Task.CompletedTask;
        }

        public bool validateParameters(string[] parameters)
        {
            string displayName = parameters[0];

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