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

            chatClient.displayName = parameters[0];
            Console.WriteLine($"You are now known as {parameters[0]}");
            return Task.CompletedTask;
        }
    }
}