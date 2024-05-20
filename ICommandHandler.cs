namespace IPK24ChatClient
{
    public interface ICommandHandler{
        bool RequiresServerConfirmation { get; }
        Task ExecuteCommandAsync(string[] parameters, CancellationToken cancelToken);
    }

}