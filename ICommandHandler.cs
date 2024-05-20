namespace IPK24ChatClient
{
    public interface ICommandHandler{
        Task ExecuteCommandAsync(string[] parameters, CancellationToken cancelToken);
    }

}