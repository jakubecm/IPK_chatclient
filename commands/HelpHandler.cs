namespace IPK24ChatClient
{
    public class HelpHandler : ICommandHandler
    {
        public bool RequiresServerConfirmation => false;
        public Task ExecuteCommandAsync(string[] parameters, CancellationToken cancellationToken)
        {
            Console.WriteLine("/auth {Username} {Secret} {DisplayName} - Authenticate with the server.");
            Console.WriteLine("/join {ChannelID} - Join a chat channel.");
            Console.WriteLine("/rename {DisplayName} - Change your display name locally.");
            Console.WriteLine("/help - Show this help message.");
            return Task.CompletedTask;
        }

        public bool validateParameters(string[] parameters)
        {
            return true;
        }
    }
}