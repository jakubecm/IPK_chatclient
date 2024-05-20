namespace IPK24ChatClient
{
    public class HelpHandler : ICommandHandler
    {
        public Task ExecuteCommandAsync(string[] parameters, CancellationToken cancellationToken)
        {
            Console.WriteLine("Usage: IPK24ChatClient [options]");
            Console.WriteLine("Options:");
            Console.WriteLine("  -t <protocol>  Protocol to use (tcp or udp)");
            Console.WriteLine("  -s <address>   Server address");
            Console.WriteLine("  -p <port>      Server port");
            Console.WriteLine("  -d <timeout>   UDP timeout in milliseconds");
            Console.WriteLine("  -r <retries>   Number of UDP retries");
            Console.WriteLine("  -h             Display this help message");

            return Task.CompletedTask;
        }
    }
}