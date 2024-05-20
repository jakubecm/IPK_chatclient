namespace IPK24ChatClient
{
    public interface ICommandHandler{
        bool RequiresServerConfirmation { get; }
        Task ExecuteCommandAsync(string[] parameters, CancellationToken cancelToken);
        bool validateParameters(string[] parameters);
    }

}