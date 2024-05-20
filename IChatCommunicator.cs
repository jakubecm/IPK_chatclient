namespace IPK24ChatClient
{
    public interface IChatCommunicator
    {
        Task ConnectAsync(string serverAddress, int serverPort);
        void Disconnect();
        Task SendMessageAsync(string message);
        Task<string?> ReceiveMessageAsync();
        Message ParseMessage(string message);
    }

}

